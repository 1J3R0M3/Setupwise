using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Setupwise.App.Infrastructure;
using Setupwise.App.Models;
using Setupwise.Core.Icons;
using Setupwise.Core.Packages;

namespace Setupwise.App.Services;

/// <summary>
/// Loads package icons in the background. Requests are a stack, so the page the user
/// looks at right now is served first. Bookkeeping runs on the UI thread; reading and
/// decoding files and all network I/O run on the thread pool.
/// </summary>
public sealed class IconLoader
{
    // Downloads from vendor sites are cheap to run side by side; "winget show" (only needed for apps without a
    // homepage in the catalog) starts a process each time, so fewer of those run at once.
    private const int Parallelism = 8;
    private const int WingetParallelism = 3;

    private readonly IconService _icons;
    private readonly IPackageManager? _packages;
    private readonly PackageStore _store;
    private readonly Stack<string> _pending = new();
    private readonly HashSet<string> _done = new(StringComparer.OrdinalIgnoreCase);
    private readonly Queue<TaskCompletionSource> _wingetWaiting = new();
    private int _workers;
    private int _wingetRunning;

    public IconLoader(IconService icons, IPackageManager? packages, PackageStore store)
    {
        _icons = icons;
        _packages = packages;
        _store = store;
    }

    public bool Enabled { get; set; } = true;

    public void Request(IEnumerable<PackageItem> items)
    {
        if (!Enabled) return;
        // Push in reverse so the first item of the list is loaded first.
        foreach (var item in items.Reverse())
        {
            if (item.Icon is not null || _done.Contains(item.Id)) continue;
            _pending.Push(item.Id);
        }

        while (_workers < Parallelism && _pending.Count > 0)
        {
            _workers++;
            _ = WorkAsync();
        }
    }

    public void ClearCache()
    {
        foreach (var item in _store.All)
        {
            _icons.Forget(item.Id);
            item.Icon = null;
        }
        _done.Clear();
    }

    private async Task WorkAsync()
    {
        try
        {
            while (_pending.Count > 0)
            {
                var id = _pending.Pop();
                if (!_done.Add(id)) continue;
                await LoadAsync(id, _store.Find(id)?.Homepage);
            }
        }
        finally
        {
            _workers--;
        }
    }

    private async Task LoadAsync(string id, Uri? knownHomepage)
    {
        try
        {
            // Cache first: no network, and the file is read and decoded off the UI thread.
            var cached = await Task.Run(() => LoadCached(id));
            if (cached.Image is not null)
            {
                Apply(id, cached.Image);
                return;
            }
            if (cached.KnownMissing || _packages is null) return;

            var homepage = knownHomepage ?? await GetHomepageAsync(id);
            var image = homepage is null ? null : await Task.Run(async () =>
            {
                var file = await _icons.DownloadAsync(id, homepage, CancellationToken.None).ConfigureAwait(false);
                return file is null ? null : Decode(file);
            });
            if (image is null)
            {
                _icons.Forget(id);
                _icons.MarkMissing(id);
                return;
            }
            Apply(id, image);
        }
        catch (Exception ex)
        {
            AppLog.Write($"Icon for {id} could not be loaded: {ex.Message}");
        }
    }

    /// <summary>At most <see cref="WingetParallelism"/> "winget show" at once (UI thread only, so no lock).</summary>
    private async Task<Uri?> GetHomepageAsync(string id)
    {
        if (_wingetRunning >= WingetParallelism)
        {
            var turn = new TaskCompletionSource();
            _wingetWaiting.Enqueue(turn);
            await turn.Task;
        }
        else
        {
            _wingetRunning++;
        }
        try
        {
            return await _packages!.GetHomepageAsync(id);
        }
        finally
        {
            // Hand the slot to the next waiting request, or free it.
            if (_wingetWaiting.TryDequeue(out var next)) next.SetResult();
            else _wingetRunning--;
        }
    }

    private (ImageSource? Image, bool KnownMissing) LoadCached(string id)
    {
        var file = _icons.TryGetCached(id);
        if (file is not null)
        {
            var image = Decode(file);
            if (image is not null) return (image, false);
            _icons.Forget(id); // broken file: download again
        }
        return (null, _icons.IsKnownMissing(id));
    }

    private void Apply(string id, ImageSource image)
    {
        _done.Add(id);
        if (_store.Find(id) is { } item) item.Icon = image;
    }

    /// <summary>Decodes PNG/ICO/JPG/…; for .ico files the largest frame is used. Frozen, so any thread may decode.</summary>
    private static BitmapFrame? Decode(string file)
    {
        try
        {
            using var stream = new MemoryStream(File.ReadAllBytes(file));
            var decoder = BitmapDecoder.Create(stream, BitmapCreateOptions.None, BitmapCacheOption.OnLoad);
            var frame = decoder.Frames.MaxBy(f => f.PixelWidth);
            frame?.Freeze();
            return frame;
        }
        catch (Exception ex) when (ex is IOException or NotSupportedException or FileFormatException or ArgumentException or InvalidOperationException)
        {
            return null;
        }
    }
}
