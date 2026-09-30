using System.Text.Json;
using System.Text.RegularExpressions;

namespace Setupwise.Core.Tests;

/// <summary>
/// Checks the UI translations of the app (src/Setupwise.App/Localization/Strings).
/// Lives here because the app itself only builds for Windows.
/// </summary>
public partial class LocalizationTests
{
    private static readonly string AppDirectory = Path.Combine(RepositoryRoot(), "src", "Setupwise.App");
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    private static readonly string StringsDirectory = Path.Combine(AppDirectory, "Localization", "Strings");

    [Fact]
    public void Every_key_used_by_the_app_exists_in_English()
    {
        var english = Load("en");
        var missing = UsedKeys().Where(k => !english.ContainsKey(k)).Order().ToList();
        Assert.Empty(missing);
    }

    [Fact]
    public void Translations_only_contain_known_keys_with_matching_placeholders()
    {
        var english = Load("en");
        var problems = new List<string>();

        foreach (var file in Directory.GetFiles(StringsDirectory, "*.json"))
        {
            var language = Path.GetFileNameWithoutExtension(file);
            if (language == "en") continue;

            foreach (var (key, text) in Load(language))
            {
                if (!english.TryGetValue(key, out var reference))
                    problems.Add($"{language}: unknown key '{key}'");
                else if (!Placeholders(text).SetEquals(Placeholders(reference)))
                    problems.Add($"{language}: placeholders of '{key}' differ from English");
            }
        }

        Assert.Empty(problems);
    }

    [Fact]
    public void German_translation_is_complete()
    {
        var missing = Load("en").Keys.Except(Load("de").Keys).Order().ToList();
        Assert.Empty(missing);
    }

    private static Dictionary<string, string> Load(string language)
    {
        var json = File.ReadAllText(Path.Combine(StringsDirectory, $"{language}.json"));
        return JsonSerializer.Deserialize<Dictionary<string, string>>(json, JsonOptions) ?? [];
    }

    private static HashSet<string> UsedKeys()
    {
        var keys = new HashSet<string>(StringComparer.Ordinal);
        foreach (var file in Directory.EnumerateFiles(AppDirectory, "*.*", SearchOption.AllDirectories)
                     .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
                              && !f.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal)))
        {
            var text = File.ReadAllText(file);
            var pattern = file.EndsWith(".xaml", StringComparison.Ordinal) ? XamlKey() : file.EndsWith(".cs", StringComparison.Ordinal) ? CodeKey() : null;
            if (pattern is null) continue;
            foreach (Match m in pattern.Matches(text))
            {
                foreach (var group in m.Groups.Values.Skip(1).Where(g => g.Success)) keys.Add(group.Value);
            }
        }
        return keys;
    }

    private static HashSet<string> Placeholders(string text) =>
        Placeholder().Matches(text).Select(m => m.Value).ToHashSet();

    private static string RepositoryRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "Setupwise.slnx"))) dir = dir.Parent;
        return dir?.FullName ?? throw new InvalidOperationException("Repository root (Setupwise.slnx) not found.");
    }

    [GeneratedRegex(@"\{loc:Tr (\w+)\}")]
    private static partial Regex XamlKey();

    // Loc.T("Key"), Loc.F("Key", ...) and Loc.T(condition ? "A" : "B")
    [GeneratedRegex(@"Loc\.[TF]\((?:[^;""]*?\?\s*""(\w+)""\s*:\s*""(\w+)""|""(\w+)"")")]
    private static partial Regex CodeKey();

    [GeneratedRegex(@"\{\d+(?:[,:][^}]*)?\}")]
    private static partial Regex Placeholder();
}
