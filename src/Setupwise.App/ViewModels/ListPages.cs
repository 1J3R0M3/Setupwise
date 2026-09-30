using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Setupwise.App.Infrastructure;
using Setupwise.App.Localization;
using Setupwise.App.Models;
using Setupwise.App.Services;
using Setupwise.Core.Catalog;
using Setupwise.Core.Packages;
using Wpf.Ui.Controls;

namespace Setupwise.App.ViewModels;

public sealed class CategoryViewModel : PackageListPageViewModel
{
    private readonly CatalogCategory _category;

    public CategoryViewModel(CatalogCategory category, PackageStore store, IEnumerable<PackageItem> items)
        : base(store, new ObservableCollection<PackageItem>(items))
    {
        _category = category;
        Symbol = Enum.TryParse<SymbolRegular>(category.Icon, out var symbol) ? symbol : SymbolRegular.Apps24;
    }

    public override string Title => _category.Name.Get(Loc.Instance.Culture);
    public override SymbolRegular Symbol { get; }
    public override string? Subtitle => Loc.F("Category_Subtitle", Items.Count);
    public override string EmptyText => Loc.T("Category_Empty");
}

public sealed class SelectionViewModel : PackageListPageViewModel
{
    private readonly PackageStore _store;

    public SelectionViewModel(PackageStore store, UserCategoriesService categories) : base(store, store.Selected)
    {
        _store = store;
        ClearCommand = new RelayCommand(_store.ClearSelection);
        SaveAsCategoryCommand = new RelayCommand(() => categories.CreateInteractively(_store.Selected.ToList()));
    }

    public override string Title => Loc.T("Nav_Selection");
    public override SymbolRegular Symbol => SymbolRegular.TaskListSquareLtr24;
    public override string? Subtitle => Loc.T("Selection_Subtitle");
    public override string EmptyText => Loc.T("Selection_Empty");
    public override bool ShowClear => true;
    public override bool ShowSaveAsCategory => HasItems;

    public IRelayCommand ClearCommand { get; }
    public IRelayCommand SaveAsCategoryCommand { get; }
}

public sealed partial class UpdatesViewModel : PackageListPageViewModel
{
    private readonly PackageStore _store;
    private readonly Func<Task> _refresh;

    public UpdatesViewModel(PackageStore store, Func<Task> refresh) : base(store, store.Updates)
    {
        _store = store;
        _refresh = refresh;
    }

    public override string Title => Loc.T("Nav_Updates");
    public override SymbolRegular Symbol => SymbolRegular.ArrowSync24;
    public override string? Subtitle => Loc.T("Updates_Subtitle");
    public override bool ShowRefresh => true;

    public override string EmptyText => _store.InstalledStateKnown ? Loc.T("Updates_None") : Loc.T("Updates_NotChecked");

    [RelayCommand(AllowConcurrentExecutions = false)]
    private async Task RefreshAsync()
    {
        await _refresh();
        RaiseEmptyTextChanged();
    }

    public void SetChecking(bool checking)
    {
        IsBusy = checking;
        BusyText = checking ? Loc.T("Updates_Checking") : null;
        RaiseEmptyTextChanged();
    }
}

public sealed partial class SearchViewModel : PackageListPageViewModel
{
    private readonly IPackageManager? _packages;
    private readonly PackageStore _store;
    private readonly IconLoader _icons;
    private string _emptyText = Loc.T("Search_Empty");

    public SearchViewModel(IPackageManager? packages, PackageStore store, IconLoader icons)
        : base(store, [])
    {
        _packages = packages;
        _store = store;
        _icons = icons;
    }

    public override string Title => Loc.T("Nav_Search");
    public override SymbolRegular Symbol => SymbolRegular.Search24;
    public override string? Subtitle => Loc.T("Search_Subtitle");
    public override string EmptyText => _emptyText;
    public override bool ShowSearchBox => true;

    [ObservableProperty]
    public partial string Query { get; set; } = string.Empty;

    [RelayCommand(AllowConcurrentExecutions = false)]
    private async Task SearchAsync()
    {
        var query = Query.Trim();
        if (query.Length == 0 || _packages is null) return;

        IsBusy = true;
        BusyText = Loc.F("Search_Running", query);
        try
        {
            var results = await _packages.SearchAsync(query);

            // Keep checked results of the previous search, so nothing selected gets lost.
            var keep = Items.Where(i => i.IsSelected).ToList();
            Items.Clear();
            foreach (var item in keep) Items.Add(item);
            foreach (var result in results)
            {
                var item = _store.GetOrCreate(result.Id, result.Name);
                item.Version ??= result.Version;
                if (!Items.Contains(item)) Items.Add(item);
            }

            _emptyText = Loc.F("Search_NoResults", query);
            AppLog.Write($"Search \"{query}\": {results.Count} results");
            _icons.Request(Items);
        }
        catch (Exception ex)
        {
            _emptyText = Loc.F("Winget_Failed", ex.Message);
            AppLog.Write($"Search failed: {ex}");
        }
        finally
        {
            IsBusy = false;
            BusyText = null;
            RaiseEmptyTextChanged();
        }
    }
}
