using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Setupwise.Core.Catalog;

/// <summary>
/// A text with translations, keyed by language code ("en", "de", "pt-BR", ...).
/// In JSON it is either a plain string (same text for every language) or an object.
/// </summary>
[JsonConverter(typeof(LocalizedTextJsonConverter))]
public sealed class LocalizedText
{
    public const string FallbackLanguage = "en";

    private readonly Dictionary<string, string> _values;

    public LocalizedText(IDictionary<string, string> values)
    {
        _values = new Dictionary<string, string>(values, StringComparer.OrdinalIgnoreCase);
    }

    public LocalizedText(string value) : this(new Dictionary<string, string> { [FallbackLanguage] = value })
    {
    }

    public IReadOnlyDictionary<string, string> Values => _values;

    /// <summary>
    /// Returns the best match for the culture: exact name ("de-AT"), then the neutral
    /// language ("de"), then English, then whatever exists.
    /// </summary>
    public string Get(CultureInfo culture)
    {
        ArgumentNullException.ThrowIfNull(culture);
        if (_values.TryGetValue(culture.Name, out var exact)) return exact;
        if (_values.TryGetValue(culture.TwoLetterISOLanguageName, out var neutral)) return neutral;
        if (_values.TryGetValue(FallbackLanguage, out var fallback)) return fallback;
        return _values.Values.FirstOrDefault() ?? string.Empty;
    }

    public override string ToString() => Get(CultureInfo.CurrentUICulture);
}

internal sealed class LocalizedTextJsonConverter : JsonConverter<LocalizedText>
{
    public override LocalizedText Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.String)
        {
            return new LocalizedText(reader.GetString() ?? string.Empty);
        }

        var values = JsonSerializer.Deserialize<Dictionary<string, string>>(ref reader, options)
                     ?? throw new JsonException("Localized text must be a string or an object.");
        return new LocalizedText(values);
    }

    public override void Write(Utf8JsonWriter writer, LocalizedText value, JsonSerializerOptions options)
    {
        JsonSerializer.Serialize(writer, value.Values, options);
    }
}
