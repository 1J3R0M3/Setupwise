using System.Net.Http.Json;
using System.Text.Json.Serialization;

namespace Setupwise.Core.Updates;

public sealed record AppRelease(AppVersion Version, Uri PageUrl);

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
    /// <param name="includePreReleases">Also offer alpha/beta versions (GitHub pre-releases).</param>
    public async Task<AppRelease?> FindNewerAsync(AppVersion current, bool includePreReleases = false, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(current);
        try
        {
            // "releases/latest" never returns pre-releases, so the list is needed for them.
            var url = includePreReleases
                ? $"https://api.github.com/repos/{_repository}/releases?per_page=20"
                : $"https://api.github.com/repos/{_repository}/releases/latest";
            using var request = new HttpRequestMessage(HttpMethod.Get, new Uri(url));
            request.Headers.Accept.ParseAdd("application/vnd.github+json");
            using var response = await _http.SendAsync(request, cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode) return null;

            IReadOnlyList<GitHubRelease>? releases = includePreReleases
                ? await response.Content.ReadFromJsonAsync<List<GitHubRelease>>(cancellationToken).ConfigureAwait(false)
                : await response.Content.ReadFromJsonAsync<GitHubRelease>(cancellationToken).ConfigureAwait(false) is { } latest ? [latest] : null;

            return Newest(releases ?? [], includePreReleases) is { } newest && newest.Version > current ? newest : null;
        }
        catch (HttpRequestException) { return null; }
        catch (System.Text.Json.JsonException) { return null; }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested) { return null; }
    }

    private static AppRelease? Newest(IEnumerable<GitHubRelease> releases, bool includePreReleases)
    {
        AppRelease? newest = null;
        foreach (var release in releases)
        {
            if (release.Draft || (release.Prerelease && !includePreReleases)) continue;
            if (!AppVersion.TryParse(release.TagName, out var version) || !Uri.TryCreate(release.HtmlUrl, UriKind.Absolute, out var page))
                continue;
            if (newest is null || version > newest.Version) newest = new AppRelease(version, page);
        }
        return newest;
    }

    private sealed class GitHubRelease
    {
        [JsonPropertyName("tag_name")] public string? TagName { get; set; }
        [JsonPropertyName("html_url")] public string? HtmlUrl { get; set; }
        [JsonPropertyName("draft")] public bool Draft { get; set; }
        [JsonPropertyName("prerelease")] public bool Prerelease { get; set; }
    }
}
