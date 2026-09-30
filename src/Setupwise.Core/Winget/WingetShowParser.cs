using System.Text.RegularExpressions;

namespace Setupwise.Core.Winget;

/// <summary>Reads details from the output of "winget show". Labels depend on the Windows language.</summary>
public static partial class WingetShowParser
{
    public static Uri? GetHomepage(IEnumerable<string> lines)
    {
        ArgumentNullException.ThrowIfNull(lines);
        var urls = new List<(string Label, Uri Url)>();
        foreach (var line in lines)
        {
            var m = LabeledUrl().Match(line);
            if (m.Success && Uri.TryCreate(m.Groups[2].Value, UriKind.Absolute, out var uri))
                urls.Add((m.Groups[1].Value.Trim(), uri));
        }

        return Pick(urls, HomepageLabel())
               ?? Pick(urls, PublisherUrlLabel())
               ?? urls.Where(u => !IgnoredLabel().IsMatch(u.Label)).Select(u => u.Url).FirstOrDefault();
    }

    private static Uri? Pick(List<(string Label, Uri Url)> urls, Regex label) =>
        urls.Where(u => label.IsMatch(u.Label)).Select(u => u.Url).FirstOrDefault();

    [GeneratedRegex(@"^\s*([^:]+?)\s*:\s*(https?://\S+)")]
    private static partial Regex LabeledUrl();

    [GeneratedRegex(@"^(Homepage|Home page|Startseite|Page d'accueil|Página principal|Pagina iniziale)$", RegexOptions.IgnoreCase)]
    private static partial Regex HomepageLabel();

    [GeneratedRegex(@"^(Publisher Url|Herausgeber-URL|URL de l'éditeur)$", RegexOptions.IgnoreCase)]
    private static partial Regex PublisherUrlLabel();

    [GeneratedRegex(@"Install|Lizenz|Licen|Support|Privacy|Datenschutz|Release|Version|Kauf|Purchase|Doku|Documentation", RegexOptions.IgnoreCase)]
    private static partial Regex IgnoredLabel();
}
