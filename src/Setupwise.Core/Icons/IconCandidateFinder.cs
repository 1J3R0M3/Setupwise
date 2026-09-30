using System.Net;
using System.Text.RegularExpressions;

namespace Setupwise.Core.Icons;

/// <summary>Finds URLs of app icons on a project's homepage.</summary>
public static partial class IconCandidateFinder
{
    private const int GoodSize = 48;

    /// <summary>Icons that do not need the homepage HTML (e.g. GitHub avatars).</summary>
    public static Uri? DirectIcon(Uri homepage)
    {
        ArgumentNullException.ThrowIfNull(homepage);
        // For projects hosted on GitHub the owner's avatar is far better than the GitHub logo.
        if (homepage.Host.Equals("github.com", StringComparison.OrdinalIgnoreCase) && homepage.Segments.Length >= 2)
        {
            var owner = homepage.Segments[1].Trim('/');
            if (owner.Length > 0) return new Uri($"https://github.com/{owner}.png?size=128");
        }
        return null;
    }

    /// <summary>
    /// Ordered candidates: large declared icons, then /apple-touch-icon.png, then small declared
    /// icons, then /favicon.ico. SVG and data: URIs are skipped (WPF cannot render them natively).
    /// </summary>
    public static IReadOnlyList<Uri> FromHtml(Uri pageUri, string html)
    {
        ArgumentNullException.ThrowIfNull(pageUri);
        ArgumentNullException.ThrowIfNull(html);

        var declared = new List<(Uri Url, int Size)>();
        foreach (Match tag in LinkTag().Matches(html))
        {
            var rel = Attribute(tag.Value, "rel")?.ToLowerInvariant();
            if (rel is null || !rel.Contains("icon", StringComparison.Ordinal)) continue;

            var href = Attribute(tag.Value, "href");
            if (string.IsNullOrWhiteSpace(href)) continue;
            href = WebUtility.HtmlDecode(href);
            if (href.StartsWith("data:", StringComparison.OrdinalIgnoreCase)) continue;
            if (!Uri.TryCreate(pageUri, href, out var url) || url.Scheme is not ("http" or "https")) continue;
            if (url.AbsolutePath.EndsWith(".svg", StringComparison.OrdinalIgnoreCase)) continue;

            var size = rel.Contains("apple-touch", StringComparison.Ordinal) ? 180 : 16;
            var sizes = SizesValue().Match(Attribute(tag.Value, "sizes") ?? string.Empty);
            if (sizes.Success) size = int.Parse(sizes.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture);
            declared.Add((url, size));
        }

        var origin = new Uri(pageUri.GetLeftPart(UriPartial.Authority));
        var ordered = declared.OrderByDescending(d => d.Size).ToList();
        var result = new List<Uri>();
        result.AddRange(ordered.Where(d => d.Size >= GoodSize).Select(d => d.Url));
        result.Add(new Uri(origin, "/apple-touch-icon.png"));
        result.AddRange(ordered.Where(d => d.Size < GoodSize).Select(d => d.Url));
        result.Add(new Uri(origin, "/favicon.ico"));
        return result.Distinct().ToList();
    }

    private static string? Attribute(string tag, string name)
    {
        var m = Regex.Match(tag, $@"\b{name}\s*=\s*(?:""([^""]*)""|'([^']*)'|([^\s>]+))", RegexOptions.IgnoreCase);
        if (!m.Success) return null;
        return m.Groups[1].Success ? m.Groups[1].Value : m.Groups[2].Success ? m.Groups[2].Value : m.Groups[3].Value;
    }

    [GeneratedRegex(@"<link\b[^>]*>", RegexOptions.IgnoreCase)]
    private static partial Regex LinkTag();

    [GeneratedRegex(@"^(\d+)x\d+")]
    private static partial Regex SizesValue();
}
