using System.Text;

namespace Setupwise.Core.Icons;

/// <summary>
/// Downloads app icons from the vendor's homepage and caches them on disk.
/// Only the vendor's own website is contacted, never a third-party icon service.
/// </summary>
public sealed class IconService
{
    private const int MaxImageBytes = 1024 * 1024;
    private const int MaxHtmlBytes = 768 * 1024;
    private static readonly TimeSpan MissRetryAfter = TimeSpan.FromDays(3);

    private readonly HttpClient _http;
    private readonly string _cacheDirectory;
    private readonly TimeProvider _time;

    public IconService(HttpClient http, string cacheDirectory, TimeProvider? time = null)
    {
        _http = http;
        _cacheDirectory = cacheDirectory;
        _time = time ?? TimeProvider.System;
        Directory.CreateDirectory(cacheDirectory);
    }

    /// <summary>Cached icon file, or null.</summary>
    public string? TryGetCached(string packageId)
    {
        foreach (var ext in ImageFormat.Extensions)
        {
            var file = PathFor(packageId, ext);
            if (File.Exists(file)) return file;
        }
        return null;
    }

    /// <summary>True if a recent lookup found nothing, so we should not try again yet.</summary>
    public bool IsKnownMissing(string packageId)
    {
        var marker = PathFor(packageId, "none");
        return File.Exists(marker) && _time.GetUtcNow() - File.GetLastWriteTimeUtc(marker) < MissRetryAfter;
    }

    public void MarkMissing(string packageId)
    {
        try { File.WriteAllText(PathFor(packageId, "none"), _time.GetUtcNow().ToString("O")); }
        catch (IOException) { /* cache is best effort */ }
        catch (UnauthorizedAccessException) { }
    }

    public void Forget(string packageId)
    {
        foreach (var ext in ImageFormat.Extensions.Append("none"))
        {
            try { File.Delete(PathFor(packageId, ext)); }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
    }

    /// <summary>Downloads the best icon for the homepage. Returns the cached file or null.</summary>
    public async Task<string?> DownloadAsync(string packageId, Uri homepage, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(homepage);
        var candidates = new List<Uri>();
        var direct = IconCandidateFinder.DirectIcon(homepage);
        if (direct is not null)
        {
            candidates.Add(direct);
        }
        else
        {
            var page = await TryGetAsync(homepage, MaxHtmlBytes, cancellationToken).ConfigureAwait(false);
            var html = page is null ? string.Empty : Encoding.UTF8.GetString(page.Value.Bytes);
            candidates.AddRange(IconCandidateFinder.FromHtml(page?.FinalUri ?? homepage, html));
        }

        foreach (var url in candidates)
        {
            var image = await TryGetAsync(url, MaxImageBytes, cancellationToken).ConfigureAwait(false);
            if (image is null) continue;
            var ext = ImageFormat.Detect(image.Value.Bytes);
            if (ext is null) continue;

            var file = PathFor(packageId, ext);
            await File.WriteAllBytesAsync(file, image.Value.Bytes, cancellationToken).ConfigureAwait(false);
            return file;
        }
        return null;
    }

    private async Task<(byte[] Bytes, Uri FinalUri)?> TryGetAsync(Uri url, int maxBytes, CancellationToken cancellationToken)
    {
        try
        {
            using var response = await _http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode) return null;

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
            using var buffer = new MemoryStream();
            var chunk = new byte[64 * 1024];
            int read;
            while ((read = await stream.ReadAsync(chunk, cancellationToken).ConfigureAwait(false)) > 0)
            {
                buffer.Write(chunk, 0, read);
                if (buffer.Length > maxBytes) break;
            }
            return (buffer.ToArray(), response.RequestMessage?.RequestUri ?? url);
        }
        catch (HttpRequestException) { return null; }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested) { return null; } // timeout
    }

    private string PathFor(string packageId, string extension)
    {
        var safe = string.Concat(packageId.Select(ch => Path.GetInvalidFileNameChars().Contains(ch) ? '_' : ch));
        return Path.Combine(_cacheDirectory, $"{safe}.{extension}");
    }
}
