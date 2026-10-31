using System.Globalization;
using System.Text;
using System.Text.Json;
using Setupwise.Core.Catalog;

namespace Setupwise.Core.Tweaks;

/// <summary>Windows tweaks (R-19) from catalog/tweaks.json – only shipped with the installer/zip build.</summary>
public sealed class TweakCatalog
{
    public int SchemaVersion { get; init; } = 1;
    public IReadOnlyList<CatalogCategory> Categories { get; init; } = [];
    public IReadOnlyList<Tweak> Tweaks { get; init; } = [];
}

public sealed class Tweak
{
    public required string Id { get; init; }
    public required string Category { get; init; }
    public required LocalizedText Name { get; init; }
    public LocalizedText? Description { get; init; }

    /// <summary>The WinUtil entry the values come from.</summary>
    public string? Source { get; init; }

    public IReadOnlyList<RegistryChange> Registry { get; init; } = [];

    /// <summary>Explorer only picks the change up after a restart (e.g. file extensions).</summary>
    public bool RestartExplorer { get; init; }

    public bool NeedsAdministrator => Registry.Any(r => r.NeedsAdministrator);
}

/// <summary>One registry value: <see cref="Apply"/> turns the tweak on, <see cref="Undo"/> back. Null = delete the value.</summary>
public sealed class RegistryChange
{
    /// <summary>"HKCU\…", "HKLM\…" or "HKU\…".</summary>
    public required string Path { get; init; }
    public required string Name { get; init; }

    /// <summary>DWord, QWord, String or ExpandString.</summary>
    public required string Type { get; init; }
    public string? Apply { get; init; }
    public string? Undo { get; init; }

    public bool NeedsAdministrator => !Path.StartsWith(@"HKCU\", StringComparison.OrdinalIgnoreCase);

    public string? ValueFor(bool undo) => undo ? Undo : Apply;
}

public enum TweakState { Off, On, Partly }

public static class TweakLoader
{
    private static readonly string[] Hives = [@"HKCU\", @"HKLM\", @"HKU\"];
    private static readonly string[] Types = ["DWord", "QWord", "String", "ExpandString"];

    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    public static TweakCatalog Load(Stream json) =>
        JsonSerializer.Deserialize<TweakCatalog>(json, Options) ?? throw new JsonException("The tweak file is empty.");

    public static TweakCatalog Load(string json) =>
        JsonSerializer.Deserialize<TweakCatalog>(json, Options) ?? throw new JsonException("The tweak file is empty.");

    /// <summary>Checks the file for mistakes. Empty list = valid.</summary>
    public static IReadOnlyList<string> Validate(TweakCatalog catalog)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        var errors = new List<string>();
        var categories = catalog.Categories.Select(c => c.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var tweak in catalog.Tweaks)
        {
            if (string.IsNullOrWhiteSpace(tweak.Id) || !ids.Add(tweak.Id)) errors.Add($"Tweak id '{tweak.Id}' is empty or used twice.");
            if (!categories.Contains(tweak.Category ?? string.Empty)) errors.Add($"Tweak '{tweak.Id}' uses unknown category '{tweak.Category}'.");
            if (tweak.Name?.Values.ContainsKey(LocalizedText.FallbackLanguage) != true) errors.Add($"Tweak '{tweak.Id}' needs an English name.");
            if (tweak.Registry.Count == 0) errors.Add($"Tweak '{tweak.Id}' changes nothing.");
            foreach (var change in tweak.Registry)
            {
                if (!Hives.Any(h => change.Path?.StartsWith(h, StringComparison.OrdinalIgnoreCase) == true))
                    errors.Add($"Tweak '{tweak.Id}': unknown registry hive in '{change.Path}'.");
                if (!Types.Contains(change.Type, StringComparer.OrdinalIgnoreCase))
                    errors.Add($"Tweak '{tweak.Id}': unknown value type '{change.Type}'.");
                if (string.IsNullOrWhiteSpace(change.Name)) errors.Add($"Tweak '{tweak.Id}': a value has no name.");
                if (change.Apply == change.Undo) errors.Add($"Tweak '{tweak.Id}': applying and undoing '{change.Name}' do the same.");
                foreach (var value in new[] { change.Apply, change.Undo })
                {
                    if (value is not null && change.Type is "DWord" or "QWord" && !long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out _))
                        errors.Add($"Tweak '{tweak.Id}': '{value}' is not a number for {change.Name}.");
                }
            }
        }
        return errors;
    }
}

public static class TweakStates
{
    /// <param name="read">Reads a registry value as text: (path like "HKCU\…", value name) → value, null if missing.</param>
    public static TweakState Evaluate(Tweak tweak, Func<string, string, string?> read)
    {
        ArgumentNullException.ThrowIfNull(tweak);
        ArgumentNullException.ThrowIfNull(read);
        var on = tweak.Registry.Count(r => string.Equals(read(r.Path, r.Name), r.Apply, StringComparison.OrdinalIgnoreCase));
        return on == tweak.Registry.Count ? TweakState.On : on == 0 ? TweakState.Off : TweakState.Partly;
    }
}

/// <summary>
/// Builds the PowerShell script (Windows PowerShell 5.1) that applies or undoes tweaks. Every value is set on its own
/// and logged, so one failing value does not stop the rest. Pure function, so the script is unit-tested.
/// </summary>
public static class TweakScript
{
    public static string Build(IEnumerable<Tweak> tweaks, bool undo, bool restorePoint)
    {
        ArgumentNullException.ThrowIfNull(tweaks);
        var list = tweaks.ToList();
        var sb = new StringBuilder();
        sb.AppendLine("param([Parameter(Mandatory = $true)][string]$Log)");
        sb.AppendLine("$ErrorActionPreference = 'Stop'");
        sb.AppendLine("function Write-Log([string]$Text) { Add-Content -LiteralPath $Log -Value $Text -Encoding UTF8 }");
        sb.AppendLine("$failed = 0");

        if (restorePoint)
        {
            sb.AppendLine("try { Checkpoint-Computer -Description 'Setupwise' -RestorePointType MODIFY_SETTINGS; Write-Log 'Restore point created' }");
            sb.AppendLine("catch { Write-Log \"Restore point not created: $($_.Exception.Message)\" }");
        }

        foreach (var tweak in list)
        {
            sb.AppendLine(CultureInfo.InvariantCulture, $"# {tweak.Id}");
            foreach (var change in tweak.Registry)
            {
                var path = Quote("Registry::" + ExpandHive(change.Path));
                var name = Quote(change.Name);
                var value = change.ValueFor(undo);
                var what = Quote($"{tweak.Id}: {change.Path}\\{change.Name} = {value ?? "(deleted)"}");
                sb.Append("try { ");
                if (value is null)
                {
                    sb.Append(CultureInfo.InvariantCulture, $"if (Test-Path -LiteralPath {path}) {{ Remove-ItemProperty -LiteralPath {path} -Name {name} -ErrorAction SilentlyContinue }}; ");
                }
                else
                {
                    var typed = change.Type is "DWord" or "QWord" ? value : Quote(value);
                    sb.Append(CultureInfo.InvariantCulture, $"if (-not (Test-Path -LiteralPath {path})) {{ New-Item -Path {path} -Force | Out-Null }}; ");
                    sb.Append(CultureInfo.InvariantCulture, $"New-ItemProperty -LiteralPath {path} -Name {name} -PropertyType {change.Type} -Value {typed} -Force | Out-Null; ");
                }
                sb.AppendLine(CultureInfo.InvariantCulture, $"Write-Log ('OK     ' + {what}) }} catch {{ $failed++; Write-Log ('FAILED ' + {what} + ': ' + $_.Exception.Message) }}");
            }
        }

        if (list.Any(t => t.RestartExplorer))
        {
            // Explorer starts again by itself.
            sb.AppendLine("try { Stop-Process -Name explorer -Force; Write-Log 'Explorer restarted' } catch { Write-Log \"Explorer not restarted: $($_.Exception.Message)\" }");
        }
        sb.AppendLine("exit $failed");
        return sb.ToString();
    }

    /// <summary>"HKCU\x" → "HKEY_CURRENT_USER\x" for the Registry:: provider path.</summary>
    public static string ExpandHive(string path)
    {
        ArgumentNullException.ThrowIfNull(path);
        var index = path.IndexOf('\\', StringComparison.Ordinal);
        var hive = index < 0 ? path : path[..index];
        var rest = index < 0 ? string.Empty : path[index..];
        return hive.ToUpperInvariant() switch
        {
            "HKCU" => "HKEY_CURRENT_USER",
            "HKLM" => "HKEY_LOCAL_MACHINE",
            "HKU" => "HKEY_USERS",
            _ => throw new ArgumentException($"Unknown registry hive in '{path}'.", nameof(path)),
        } + rest;
    }

    /// <summary>PowerShell single-quoted string literal.</summary>
    private static string Quote(string text) => "'" + text.Replace("'", "''", StringComparison.Ordinal) + "'";
}
