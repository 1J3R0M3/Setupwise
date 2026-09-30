using System.Collections.ObjectModel;
using System.Collections.Specialized;
using CommunityToolkit.Mvvm.ComponentModel;
using Setupwise.App.Localization;
using Setupwise.App.Models;
using Wpf.Ui.Controls;

namespace Setupwise.App.ViewModels;

public abstract partial class PageViewModel : ObservableObject
{
    public abstract string Title { get; }
    public abstract SymbolRegular Symbol { get; }
    public virtual string? Subtitle => null;

    /// <summary>Packages visible on this page (used to prioritize icon loading).</summary>
    public virtual IEnumerable<PackageItem> VisiblePackages => [];
}

/// <summary>Base for every page that shows a list of package cards.</summary>
public abstract partial class PackageListPageViewModel : PageViewModel
{
    private bool _updatingAllSelected;

    protected PackageListPageViewModel(PackageStore store, ObservableCollection<PackageItem> items)
    {
        ArgumentNullException.ThrowIfNull(store);
        Items = items;
        items.CollectionChanged += OnItemsChanged;
        store.SelectionChanged += (_, _) => OnPropertyChanged(nameof(AllSelected));
    }

    public ObservableCollection<PackageItem> Items { get; }

    public override IEnumerable<PackageItem> VisiblePackages => Items;

    public bool HasItems => Items.Count > 0;

    public string CountText => Items.Count == 0 ? string.Empty : Loc.F("List_Count", Items.Count);

    public abstract string EmptyText { get; }

    public virtual bool ShowSearchBox => false;
    public virtual bool ShowRefresh => false;
    public virtual bool ShowClear => false;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowEmptyState))]
    public partial bool IsBusy { get; set; }

    [ObservableProperty]
    public partial string? BusyText { get; set; }

    public bool ShowEmptyState => !HasItems && !IsBusy;

    /// <summary>true = all selected, false = none, null = some. Bound to the "Select all" check box.</summary>
    public bool? AllSelected
    {
        get
        {
            if (Items.Count == 0) return false;
            var selected = Items.Count(i => i.IsSelected);
            return selected == 0 ? false : selected == Items.Count ? true : null;
        }
        set
        {
            if (_updatingAllSelected) return;
            _updatingAllSelected = true;
            try
            {
                var select = value == true;
                foreach (var item in Items.ToList()) item.IsSelected = select;
            }
            finally
            {
                _updatingAllSelected = false;
            }
            OnPropertyChanged();
        }
    }

    protected void RaiseEmptyTextChanged() => OnPropertyChanged(nameof(EmptyText));

    private void OnItemsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        OnPropertyChanged(nameof(HasItems));
        OnPropertyChanged(nameof(CountText));
        OnPropertyChanged(nameof(ShowEmptyState));
        OnPropertyChanged(nameof(AllSelected));
    }
}
