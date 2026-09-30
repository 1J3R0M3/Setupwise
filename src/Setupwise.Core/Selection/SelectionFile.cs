using System.Text.Json;
using System.Text.Json.Serialization;

namespace Setupwise.Core.Selection;

/// <summary>
/// A saved selection of packages ("*.setupwise"), e.g. to set up the next PC the same way.
/// </summary>
public static class SelectionFile
{
    public const string Extension = ".setupwise";
    private const string FormatName = "setupwise-selection";
    private const int CurrentVersion = 1;

    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    public static async Task SaveAsync(Stream stream, IEnumerable<string> packageIds, DateTimeOffset created, CancellationToken cancellationToken = default)
    {
        var document = new SelectionDocument
        {
            Format = FormatName,
            Version = CurrentVersion,
            Created = created,
            Packages = packageIds.Distinct(StringComparer.OrdinalIgnoreCase).Order(StringComparer.OrdinalIgnoreCase).ToList(),
        };
        await JsonSerializer.SerializeAsync(stream, document, Options, cancellationToken).ConfigureAwait(false);
    }

    /// <exception cref="InvalidDataException">The file is not a Setupwise selection.</exception>
    public static async Task<IReadOnlyList<string>> LoadAsync(Stream stream, CancellationToken cancellationToken = default)
    {
        SelectionDocument? document;
        try
        {
            document = await JsonSerializer.DeserializeAsync<SelectionDocument>(stream, Options, cancellationToken).ConfigureAwait(false);
        }
        catch (JsonException ex)
        {
            throw new InvalidDataException("The file is not a valid Setupwise selection.", ex);
        }

        if (document is null || document.Format != FormatName)
            throw new InvalidDataException("The file is not a Setupwise selection.");
        if (document.Version > CurrentVersion)
            throw new InvalidDataException("The selection was created by a newer version of Setupwise.");

        return document.Packages
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Select(id => id.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private sealed class SelectionDocument
    {
        [JsonPropertyOrder(0)] public string Format { get; set; } = string.Empty;
        [JsonPropertyOrder(1)] public int Version { get; set; }
        [JsonPropertyOrder(2)] public DateTimeOffset Created { get; set; }
        [JsonPropertyOrder(3)] public List<string> Packages { get; set; } = [];
    }
}
