using System.Net.Http.Json;
using System.Text.Json.Serialization;

namespace Setupwise.Core.Updates;

public sealed record AppRelease(Version Version, Uri PageUrl);

/// <summary>Checks GitHub Releases for a newer version of Setupwise.</summary>
public sealed class AppUpdateChecker
{
    private readonly HttpClient _http;
    private readonly string _repository;

    /// <param name="repository">"owner/name" on GitHub.</param>
    public AppUpdateChecker(HttpClient http, string repository)
    {
        _http = http;
        _repository = repository;
    }

    /// <summary>The newer release, or null if up to date or the check failed.</summary>
    public async Task<AppRelease?> FindNewerAsync(Version current, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(current);
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, new Uri($"https://api.github.com/repos/{_repository}/releases/latest"));
            request.Headers.Accept.ParseAdd("application/vnd.github+json");
            using var response = await _http.SendAsync(request, cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode) return null;

            var release = await response.Content.ReadFromJsonAsync<GitHubRelease>(cancellationToken).ConfigureAwait(false);
            if (release is null || release.Draft || release.Prerelease) return null;
            if (!TryParseVersion(release.TagName, out var latest) || !Uri.TryCreate(release.HtmlUrl, UriKind.Absolute, out var page))
                return null;

            return Normalize(latest) > Normalize(current) ? new AppRelease(latest, page) : null;
        }
        catch (HttpRequestException) { return null; }
        catch (System.Text.Json.JsonException) { return null; }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested) { return null; }
    }

    /// <summary>Parses "v1.2.3", "1.2", "1.2.3-beta" (pre-release suffix is ignored).</summary>
    public static bool TryParseVersion(string? tag, out Version version)
    {
        version = new Version(0, 0);
        if (string.IsNullOrWhiteSpace(tag)) return false;
        var text = tag.Trim().TrimStart('v', 'V');
        var dash = text.IndexOfAny(['-', '+']);
        if (dash >= 0) text = text[..dash];
        if (!Version.TryParse(text, out var parsed)) return false;
        version = parsed;
        return true;
    }

    private static Version Normalize(Version v) =>
        new(v.Major, v.Minor, Math.Max(v.Build, 0), Math.Max(v.Revision, 0));

    private sealed class GitHubRelease
    {
        [JsonPropertyName("tag_name")] public string? TagName { get; set; }
        [JsonPropertyName("html_url")] public string? HtmlUrl { get; set; }
        [JsonPropertyName("draft")] public bool Draft { get; set; }
        [JsonPropertyName("prerelease")] public bool Prerelease { get; set; }
    }
}
