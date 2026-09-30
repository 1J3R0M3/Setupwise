using System.Collections.ObjectModel;
using System.ComponentModel;
using Setupwise.Core.Packages;

namespace Setupwise.App.Models;

/// <summary>
/// All packages known to the app. Guarantees a single <see cref="PackageItem"/> per id, so a
/// package checked in the search is also checked in its category and in the selection.
/// </summary>
public sealed class PackageStore
{
    private readonly Dictionary<string, PackageItem> _items = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Selected packages in the order they were selected.</summary>
    public ObservableCollection<PackageItem> Selected { get; } = [];

    /// <summary>Installed packages with an available update.</summary>
    public ObservableCollection<PackageItem> Updates { get; } = [];

    public event EventHandler? SelectionChanged;

    /// <summary>Raised for every package that is newly created, e.g. to request its icon.</summary>
    public event EventHandler<PackageItem>? ItemCreated;

    public bool InstalledStateKnown { get; private set; }

    public IEnumerable<PackageItem> All => _items.Values;

    public PackageItem? Find(string id) => _items.GetValueOrDefault(id);

    public PackageItem GetOrCreate(string id, string name)
    {
        if (_items.TryGetValue(id, out var existing)) return existing;

        var item = new PackageItem(id, name);
        item.PropertyChanged += OnItemPropertyChanged;
        _items.Add(id, item);
        ItemCreated?.Invoke(this, item);
        return item;
    }

    /// <summary>Applies the result of "winget list" / "winget upgrade".</summary>
    public void ApplyInstalled(IReadOnlyList<InstalledPackage> installed, IReadOnlyList<InstalledPackage> upgrades)
    {
        var upgradeById = upgrades.ToDictionary(u => u.Id, StringComparer.OrdinalIgnoreCase);
        var installedIds = new HashSet<string>(installed.Select(i => i.Id), StringComparer.OrdinalIgnoreCase);
        installedIds.UnionWith(upgradeById.Keys);

        foreach (var item in _items.Values)
        {
            item.IsInstalled = installedIds.Contains(item.Id);
            if (!item.IsInstalled) item.AvailableVersion = null;
        }

        foreach (var package in installed.Concat(upgrades))
        {
            // Only create items for installed packages that we need to show (updates).
            var item = upgradeById.ContainsKey(package.Id) ? GetOrCreate(package.Id, package.Name) : Find(package.Id);
            if (item is null) continue;
            item.IsInstalled = true;
            item.Version = package.Version;
            item.AvailableVersion = upgradeById.TryGetValue(package.Id, out var upgrade) ? upgrade.AvailableVersion : null;
        }

        Updates.Clear();
        foreach (var item in _items.Values.Where(i => i.HasUpdate).OrderBy(i => i.Name, StringComparer.CurrentCultureIgnoreCase))
            Updates.Add(item);

        InstalledStateKnown = true;
    }

    /// <summary>Called after a successful install/upgrade.</summary>
    public void MarkInstalled(PackageItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        item.IsInstalled = true;
        if (item.HasUpdate)
        {
            item.Version = item.AvailableVersion;
            item.AvailableVersion = null;
        }
        Updates.Remove(item);
    }

    public void ClearSelection()
    {
        foreach (var item in Selected.ToList()) item.IsSelected = false;
    }

    private void OnItemPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(PackageItem.IsSelected) || sender is not PackageItem item) return;

        if (item.IsSelected && !Selected.Contains(item)) Selected.Add(item);
        else if (!item.IsSelected) Selected.Remove(item);
        SelectionChanged?.Invoke(this, EventArgs.Empty);
    }
}
