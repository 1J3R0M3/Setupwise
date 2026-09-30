using System.Text.Json;

namespace Setupwise.Core.Catalog;

/// <summary>A category the user created, e.g. "Tools for grandma's PC".</summary>
public sealed class UserCategory
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = string.Empty;
    public List<UserCategoryEntry> Packages { get; set; } = [];
}

/// <summary>The name is stored too, so apps found via search show a proper name after a restart.</summary>
public sealed record UserCategoryEntry(string Id, string Name);

/// <summary>Reads and writes the user's own categories (JSON file in the roaming profile).</summary>
public static class UserCategoriesFile
{
    private const int CurrentVersion = 1;

    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        WriteIndented = true,
    };

    /// <summary>
    /// Loads the categories. A missing file gives an empty list; a damaged file is renamed
    /// to *.broken so the user's data is not overwritten silently.
    /// </summary>
    public static List<UserCategory> Load(string path)
    {
        if (!File.Exists(path)) return [];
        try
        {
            var document = JsonSerializer.Deserialize<Document>(File.ReadAllText(path), Options);
            return document?.Categories
                       .Where(c => !string.IsNullOrWhiteSpace(c.Name))
                       .Select(Normalize)
                       .ToList()
                   ?? [];
        }
        catch (JsonException)
        {
            File.Move(path, path + ".broken", overwrite: true);
            return [];
        }
    }

    public static void Save(string path, IEnumerable<UserCategory> categories)
    {
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);

        // Write to a temp file first so a crash never leaves a half-written file behind.
        var temp = path + ".tmp";
        File.WriteAllText(temp, JsonSerializer.Serialize(new Document { Version = CurrentVersion, Categories = categories.ToList() }, Options));
        File.Move(temp, path, overwrite: true);
    }

    private static UserCategory Normalize(UserCategory category)
    {
        category.Name = category.Name.Trim();
        category.Packages = category.Packages
            .Where(p => !string.IsNullOrWhiteSpace(p.Id))
            .DistinctBy(p => p.Id, StringComparer.OrdinalIgnoreCase)
            .ToList();
        if (string.IsNullOrWhiteSpace(category.Id)) category.Id = Guid.NewGuid().ToString("N");
        return category;
    }

    private sealed class Document
    {
        public int Version { get; set; }
        public List<UserCategory> Categories { get; set; } = [];
    }
}
