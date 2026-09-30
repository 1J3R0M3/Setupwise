using System.ComponentModel;
using System.Globalization;
using System.Reflection;
using System.Text.Json;

namespace Setupwise.App.Localization;

/// <summary>
/// UI strings from Localization/Strings/{language}.json (embedded). Missing keys fall back to English.
/// XAML binds to the indexer via <see cref="TrExtension"/>.
/// </summary>
public sealed class Loc : INotifyPropertyChanged
{
    public static Loc Instance { get; } = new();

    /// <summary>Languages that ship with the app (code, native name).</summary>
    public static IReadOnlyList<(string Code, string Name)> Available { get; } = [("en", "English"), ("de", "Deutsch")];

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    private Dictionary<string, string> _strings = new();
    private Dictionary<string, string> _fallback = new();

    public event PropertyChangedEventHandler? PropertyChanged;

    public CultureInfo Culture { get; private set; } = CultureInfo.GetCultureInfo("en");

    public string this[string key] =>
        _strings.TryGetValue(key, out var value) ? value
        : _fallback.TryGetValue(key, out var fallback) ? fallback
        : $"[{key}]";

    /// <param name="language">"system" or a language code.</param>
    public void Load(string language)
    {
        var culture = language == "system" ? CultureInfo.CurrentUICulture : CultureInfo.GetCultureInfo(language);
        var code = Available.Any(a => a.Code == culture.TwoLetterISOLanguageName) ? culture.TwoLetterISOLanguageName : "en";

        _fallback = Read("en");
        _strings = code == "en" ? _fallback : Read(code);
        Culture = CultureInfo.GetCultureInfo(code);
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("Item[]"));
    }

    public static string T(string key) => Instance[key];

    public static string F(string key, params object?[] args) =>
        string.Format(Instance.Culture, Instance[key], args);

    private static Dictionary<string, string> Read(string code)
    {
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream($"Setupwise.Strings.{code}.json");
        if (stream is null) return new Dictionary<string, string>();
        return JsonSerializer.Deserialize<Dictionary<string, string>>(stream, JsonOptions) ?? new Dictionary<string, string>();
    }
}
