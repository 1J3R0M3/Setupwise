using Setupwise.Core.Catalog;
using Setupwise.Core.Tweaks;

namespace Setupwise.Core.Tests;

/// <summary>Windows tweaks for the installer/zip build (R-19).</summary>
public class TweakTests
{
    private static TweakCatalog Shipped()
    {
        using var stream = File.OpenRead(Path.Combine(AppContext.BaseDirectory, "tweaks.json"));
        return TweakLoader.Load(stream);
    }

    [Fact]
    public void Shipped_tweaks_are_valid_and_translated()
    {
        var catalog = Shipped();

        Assert.Empty(TweakLoader.Validate(catalog));
        Assert.NotEmpty(catalog.Tweaks);
        Assert.All(catalog.Tweaks, t => Assert.True(t.Name.Values.ContainsKey("de") && t.Description?.Values.ContainsKey("de") == true, t.Id));
        Assert.All(catalog.Categories, c => Assert.Contains(catalog.Tweaks, t => t.Category == c.Id));
    }

    [Fact]
    public void Nothing_that_weakens_security_is_shipped()
    {
        // R-19: no Defender, SmartScreen, UAC or Windows Update switches.
        var paths = Shipped().Tweaks.SelectMany(t => t.Registry).Select(r => r.Path + "\\" + r.Name).ToList();
        Assert.DoesNotContain(paths, p => p.Contains("Defender", StringComparison.OrdinalIgnoreCase)
                                          || p.Contains("SmartScreen", StringComparison.OrdinalIgnoreCase)
                                          || p.Contains("EnableLUA", StringComparison.OrdinalIgnoreCase)
                                          || p.Contains("WindowsUpdate", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Validation_finds_broken_entries()
    {
        var catalog = TweakLoader.Load("""
            {
              "categories": [ { "id": "c", "name": "C" } ],
              "tweaks": [
                { "id": "a", "category": "c", "name": "A", "registry": [ { "path": "HKXX\\Software", "name": "V", "type": "DWord", "apply": "x", "undo": "1" } ] },
                { "id": "a", "category": "missing", "name": "B", "registry": [] }
              ]
            }
            """);

        var errors = string.Join("\n", TweakLoader.Validate(catalog));

        Assert.Contains("unknown registry hive", errors, StringComparison.Ordinal);
        Assert.Contains("is not a number", errors, StringComparison.Ordinal);
        Assert.Contains("used twice", errors, StringComparison.Ordinal);
        Assert.Contains("unknown category", errors, StringComparison.Ordinal);
        Assert.Contains("changes nothing", errors, StringComparison.Ordinal);
    }

    private static Tweak Sample(bool restartExplorer = false) => new()
    {
        Id = "demo",
        Category = "c",
        Name = new LocalizedText(new Dictionary<string, string> { ["en"] = "Demo" }),
        RestartExplorer = restartExplorer,
        Registry =
        [
            new RegistryChange { Path = @"HKCU\Software\Demo", Name = "Count", Type = "DWord", Apply = "0", Undo = "1" },
            new RegistryChange { Path = @"HKLM\SOFTWARE\Policies\Demo", Name = "Text", Type = "String", Apply = "it's on", Undo = null },
        ],
    };

    [Fact]
    public void Apply_script_sets_every_value_and_quotes_safely()
    {
        var script = TweakScript.Build([Sample()], undo: false, restorePoint: true);

        Assert.Contains("Checkpoint-Computer", script, StringComparison.Ordinal);
        Assert.Contains(@"'Registry::HKEY_CURRENT_USER\Software\Demo'", script, StringComparison.Ordinal);
        Assert.Contains("-Name 'Count' -PropertyType DWord -Value 0 ", script, StringComparison.Ordinal);
        Assert.Contains("-Value 'it''s on'", script, StringComparison.Ordinal); // single quote doubled
        Assert.DoesNotContain("Remove-ItemProperty", script, StringComparison.Ordinal);
        Assert.DoesNotContain("explorer", script, StringComparison.Ordinal);
        Assert.EndsWith("exit $failed" + Environment.NewLine, script, StringComparison.Ordinal);
    }

    [Fact]
    public void Undo_script_restores_or_deletes_values()
    {
        var script = TweakScript.Build([Sample(restartExplorer: true)], undo: true, restorePoint: false);

        Assert.Contains("-Name 'Count' -PropertyType DWord -Value 1 ", script, StringComparison.Ordinal);
        Assert.Contains(@"Remove-ItemProperty -LiteralPath 'Registry::HKEY_LOCAL_MACHINE\SOFTWARE\Policies\Demo' -Name 'Text'", script, StringComparison.Ordinal);
        Assert.DoesNotContain("Checkpoint-Computer", script, StringComparison.Ordinal);
        Assert.Contains("Stop-Process -Name explorer", script, StringComparison.Ordinal);
    }

    [Fact]
    public void Only_current_user_values_work_without_admin_rights()
    {
        Assert.True(Sample().NeedsAdministrator);
        Assert.False(Sample().Registry[0].NeedsAdministrator);
        Assert.Equal(@"HKEY_USERS\.Default\Control Panel", TweakScript.ExpandHive(@"HKU\.Default\Control Panel"));
    }

    [Theory]
    [InlineData("0", "it's on", TweakState.On)]
    [InlineData("1", null, TweakState.Off)]
    [InlineData("0", null, TweakState.Partly)]
    public void State_is_read_from_the_registry(string? count, string? text, TweakState expected)
    {
        string? Read(string path, string name) => name == "Count" ? count : text;
        Assert.Equal(expected, TweakStates.Evaluate(Sample(), Read));
    }
}
