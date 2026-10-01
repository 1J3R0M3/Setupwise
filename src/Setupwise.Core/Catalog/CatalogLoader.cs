using System.Text.Json;
using System.Text.RegularExpressions;

namespace Setupwise.Core.Catalog;

public static partial class CatalogLoader
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    public static AppCatalog Load(Stream json)
    {
        return JsonSerializer.Deserialize<AppCatalog>(json, Options)
               ?? throw new JsonException("The catalog file is empty.");
    }

    public static AppCatalog Load(string json)
    {
        return JsonSerializer.Deserialize<AppCatalog>(json, Options)
               ?? throw new JsonException("The catalog file is empty.");
    }

    /// <summary>Checks the catalog for mistakes that would break the app. Empty list = valid.</summary>
    public static IReadOnlyList<string> Validate(AppCatalog catalog)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        var errors = new List<string>();

        var categoryIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var category in catalog.Categories)
        {
            if (string.IsNullOrWhiteSpace(category.Id)) errors.Add("A category has no id.");
            else if (!categoryIds.Add(category.Id)) errors.Add($"Duplicate category id '{category.Id}'.");
            RequireEnglish(category.Name, $"category '{category.Id}' name", errors);
        }

        var appIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var app in catalog.Apps)
        {
            var id = app.Id ?? string.Empty; // may be null when the JSON is malformed
            if (!WingetIdPattern().IsMatch(id))
                errors.Add($"App '{app.Name?.Get(System.Globalization.CultureInfo.InvariantCulture)}' has an invalid winget id '{id}'.");
            else if (!appIds.Add(id))
                errors.Add($"Duplicate app id '{app.Id}'.");

            RequireEnglish(app.Name, $"app '{app.Id}' name", errors);
            if (!categoryIds.Contains(app.Category ?? string.Empty))
                errors.Add($"App '{app.Id}' uses unknown category '{app.Category}'.");
            if (app.Description is not null) RequireEnglish(app.Description, $"app '{app.Id}' description", errors);
        }

        var profileIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var profile in catalog.Profiles)
        {
            if (string.IsNullOrWhiteSpace(profile.Id)) errors.Add("A profile has no id.");
            else if (!profileIds.Add(profile.Id)) errors.Add($"Duplicate profile id '{profile.Id}'.");
            RequireEnglish(profile.Name, $"profile '{profile.Id}' name", errors);
            if (profile.Apps.Count == 0) errors.Add($"Profile '{profile.Id}' contains no apps.");
            foreach (var appId in profile.Apps.Where(id => !appIds.Contains(id)))
                errors.Add($"Profile '{profile.Id}' references unknown app '{appId}'.");
        }

        return errors;
    }

    private static void RequireEnglish(LocalizedText? text, string what, List<string> errors)
    {
        if (text is null || !text.Values.TryGetValue(LocalizedText.FallbackLanguage, out var en) || string.IsNullOrWhiteSpace(en))
            errors.Add($"The {what} needs an English (\"en\") text.");
    }

    [GeneratedRegex(@"^\w[\w.+-]*$")]
    internal static partial Regex WingetIdPattern();
}
