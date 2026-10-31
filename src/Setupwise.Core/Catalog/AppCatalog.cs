using Setupwise.Core.Distribution;

namespace Setupwise.Core.Catalog;

/// <summary>The curated list of apps, categories and profiles shipped with Setupwise.</summary>
public sealed class AppCatalog
{
    public int SchemaVersion { get; init; } = 1;
    public IReadOnlyList<CatalogCategory> Categories { get; init; } = [];
    public IReadOnlyList<CatalogApp> Apps { get; init; } = [];
    public IReadOnlyList<CatalogProfile> Profiles { get; init; } = [];

    public IEnumerable<CatalogApp> AppsIn(string categoryId) =>
        Apps.Where(a => string.Equals(a.Category, categoryId, StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// The catalog as this build shows it: the Store build leaves out apps marked <see cref="CatalogApp.HideInStore"/>,
    /// drops them from profiles, and drops categories and profiles that end up empty.
    /// </summary>
    public AppCatalog For(BuildFlavor flavor)
    {
        if (flavor != BuildFlavor.Store) return this;

        var apps = Apps.Where(a => !a.HideInStore).ToList();
        var ids = apps.Select(a => a.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var profiles = Profiles
            .Select(p => new CatalogProfile { Id = p.Id, Name = p.Name, Description = p.Description, Icon = p.Icon, Apps = p.Apps.Where(ids.Contains).ToList() })
            .Where(p => p.Apps.Count > 0)
            .ToList();
        return new AppCatalog
        {
            SchemaVersion = SchemaVersion,
            Categories = Categories.Where(c => apps.Any(a => string.Equals(a.Category, c.Id, StringComparison.OrdinalIgnoreCase))).ToList(),
            Apps = apps,
            Profiles = profiles,
        };
    }
}

public sealed class CatalogCategory
{
    public required string Id { get; init; }
    public required LocalizedText Name { get; init; }

    /// <summary>Name of a Fluent System Icon, e.g. "Globe24".</summary>
    public string Icon { get; init; } = "Apps24";
}

public sealed class CatalogApp
{
    /// <summary>The winget package identifier, e.g. "Mozilla.Firefox".</summary>
    public required string Id { get; init; }
    /// <summary>Usually a plain string; translated where the name itself differs, e.g. "(German)".</summary>
    public required LocalizedText Name { get; init; }
    public required string Category { get; init; }
    public LocalizedText? Description { get; init; }

    /// <summary>Leave the app out of the Microsoft Store build, e.g. tools that change Windows itself.</summary>
    public bool HideInStore { get; init; }
}

public sealed class CatalogProfile
{
    public required string Id { get; init; }
    public required LocalizedText Name { get; init; }
    public LocalizedText? Description { get; init; }
    public string Icon { get; init; } = "Box24";
    public IReadOnlyList<string> Apps { get; init; } = [];
}
