using System.Globalization;
using Setupwise.Core.Catalog;

namespace Setupwise.Core.Tests;

public class CatalogTests
{
    [Fact]
    public void Shipped_catalog_is_valid()
    {
        using var stream = File.OpenRead(Path.Combine(AppContext.BaseDirectory, "catalog.json"));
        var catalog = CatalogLoader.Load(stream);

        Assert.Empty(CatalogLoader.Validate(catalog));
        Assert.NotEmpty(catalog.Categories);
        Assert.NotEmpty(catalog.Profiles);
        Assert.All(catalog.Categories, c => Assert.NotEmpty(catalog.AppsIn(c.Id)));
    }

    [Fact]
    public void Shipped_catalog_is_translated_to_german()
    {
        using var stream = File.OpenRead(Path.Combine(AppContext.BaseDirectory, "catalog.json"));
        var catalog = CatalogLoader.Load(stream);

        var missing = catalog.Categories.Where(c => !c.Name.Values.ContainsKey("de")).Select(c => c.Id)
            .Concat(catalog.Profiles.Where(p => !p.Name.Values.ContainsKey("de")).Select(p => p.Id))
            .Concat(catalog.Apps.Where(a => a.Description is not null && !a.Description.Values.ContainsKey("de")).Select(a => a.Id));
        Assert.Empty(missing);
    }

    [Fact]
    public void Validation_reports_broken_references()
    {
        var catalog = CatalogLoader.Load("""
            {
              "categories": [ { "id": "tools", "name": "Tools" } ],
              "apps": [
                { "id": "Vendor.App", "name": "App", "category": "tools" },
                { "id": "vendor.app", "name": "Duplicate", "category": "tools" },
                { "id": "Bad Id", "name": "Bad", "category": "missing" },
              ],
              "profiles": [ { "id": "p", "name": "P", "apps": [ "Vendor.App", "Nope.Nope" ] } ]
            }
            """);

        var errors = CatalogLoader.Validate(catalog);

        Assert.Contains(errors, e => e.Contains("Duplicate app id", StringComparison.Ordinal));
        Assert.Contains(errors, e => e.Contains("invalid winget id", StringComparison.Ordinal));
        Assert.Contains(errors, e => e.Contains("unknown category 'missing'", StringComparison.Ordinal));
        Assert.Contains(errors, e => e.Contains("unknown app 'Nope.Nope'", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("de-DE", "Werkzeuge")]
    [InlineData("de-AT", "Werkzeuge")]
    [InlineData("fr-FR", "Tools")]
    [InlineData("en-US", "Tools")]
    public void Localized_text_falls_back_sensibly(string culture, string expected)
    {
        var text = new LocalizedText(new Dictionary<string, string> { ["en"] = "Tools", ["de"] = "Werkzeuge" });
        Assert.Equal(expected, text.Get(CultureInfo.GetCultureInfo(culture)));
    }
}
