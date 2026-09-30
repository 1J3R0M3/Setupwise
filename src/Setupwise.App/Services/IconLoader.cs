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
/// looks at right now is served first. Runs on the UI thread; I/O is awaited.
/// </summary>
public sealed class IconLoader
{
    private const int Parallelism = 3;

    private readonly IconService _icons;
    private readonly IPackageManager? _packages;
    private readonly PackageStore _store;
    private readonly Stack<string> _pending = new();
    private readonly HashSet<string> _done = new(StringComparer.OrdinalIgnoreCase);
    private int _workers;

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
            if (TryLoadCached(item)) continue;
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

    private bool TryLoadCached(PackageItem item)
    {
        var file = _icons.TryGetCached(item.Id);
        if (file is not null)
        {
            var image = Decode(file);
            if (image is not null) { Apply(item.Id, image); return true; }
            _icons.Forget(item.Id); // broken file: download again
        }
        if (_icons.IsKnownMissing(item.Id)) { _done.Add(item.Id); return true; }
        return false;
    }

    private async Task WorkAsync()
    {
        try
        {
            while (_pending.Count > 0)
            {
                var id = _pending.Pop();
                if (!_done.Add(id)) continue;
                await LoadAsync(id);
            }
        }
        finally
        {
            _workers--;
        }
    }

    private async Task LoadAsync(string id)
    {
        if (_packages is null) return;
        try
        {
            var homepage = await _packages.GetHomepageAsync(id);
            var file = homepage is null ? null : await _icons.DownloadAsync(id, homepage, CancellationToken.None);
            var image = file is null ? null : Decode(file);
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

    private void Apply(string id, ImageSource image)
    {
        _done.Add(id);
        if (_store.Find(id) is { } item) item.Icon = image;
    }

    /// <summary>Decodes PNG/ICO/JPG/…; for .ico files the largest frame is used.</summary>
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
