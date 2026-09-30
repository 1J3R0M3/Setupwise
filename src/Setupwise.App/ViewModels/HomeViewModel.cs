using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using Setupwise.App.Localization;
using Setupwise.App.Models;
using Setupwise.Core.Catalog;
using Setupwise.Core.Selection;
using Wpf.Ui.Controls;

namespace Setupwise.App.ViewModels;

public sealed partial class HomeViewModel : PageViewModel
{
    private readonly PackageStore _store;
    private readonly Action<InfoMessage> _notify;

    public HomeViewModel(AppCatalog catalog, PackageStore store, Action<InfoMessage> notify)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        _store = store;
        _notify = notify;
        Profiles = catalog.Profiles.Select(p => new ProfileViewModel(p, store)).ToList();
    }

    public override string Title => Loc.T("Nav_Home");
    public override SymbolRegular Symbol => SymbolRegular.Home24;

    public IReadOnlyList<ProfileViewModel> Profiles { get; }

    public override IEnumerable<PackageItem> VisiblePackages => Profiles.SelectMany(p => p.Apps);

    [RelayCommand]
    private async Task ExportAsync()
    {
        var ids = _store.Selected.Select(i => i.Id).ToList();
        if (ids.Count == 0)
        {
            _notify(InfoMessage.Info(Loc.T("Home_Transfer"), Loc.T("Home_ExportEmpty")));
            return;
        }

        var dialog = new SaveFileDialog
        {
            Filter = Loc.T("Dialog_SelectionFilter"),
            DefaultExt = SelectionFile.Extension,
            FileName = $"{Environment.MachineName}{SelectionFile.Extension}",
        };
        if (dialog.ShowDialog() != true) return;

        try
        {
            await using var stream = File.Create(dialog.FileName);
            await SelectionFile.SaveAsync(stream, ids, DateTimeOffset.Now);
            _notify(InfoMessage.Success(Loc.T("Home_Transfer"), Loc.F("Home_ExportDone", dialog.FileName)));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _notify(InfoMessage.Error(Loc.T("Error_Title"), Loc.F("Error_Export", ex.Message)));
        }
    }

    [RelayCommand]
    private async Task ImportAsync()
    {
        var dialog = new OpenFileDialog { Filter = Loc.T("Dialog_SelectionFilter") };
        if (dialog.ShowDialog() == true) await ImportFileAsync(dialog.FileName);
    }

    /// <summary>Selects all packages from a *.setupwise file (also used when such a file is opened with Setupwise).</summary>
    public async Task<bool> ImportFileAsync(string path)
    {
        try
        {
            await using var stream = File.OpenRead(path);
            var ids = await SelectionFile.LoadAsync(stream);
            foreach (var id in ids) _store.GetOrCreate(id, id).IsSelected = true;
            _notify(InfoMessage.Success(Loc.T("Home_Transfer"), Loc.F("Home_ImportDone", ids.Count)));
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException)
        {
            _notify(InfoMessage.Error(Loc.T("Error_Title"), Loc.F("Error_Import", ex.Message)));
            return false;
        }
    }
}

public sealed partial class ProfileViewModel : ObservableObject
{
    private readonly CatalogProfile _profile;

    public ProfileViewModel(CatalogProfile profile, PackageStore store)
    {
        _profile = profile;
        Apps = profile.Apps.Select(id => store.Find(id)).OfType<PackageItem>().ToList();
        Symbol = Enum.TryParse<SymbolRegular>(profile.Icon, out var symbol) ? symbol : SymbolRegular.Box24;
        store.SelectionChanged += (_, _) =>
        {
            OnPropertyChanged(nameof(IsFullySelected));
            OnPropertyChanged(nameof(ButtonText));
        };
    }

    public string Name => _profile.Name.Get(Loc.Instance.Culture);
    public string? Description => _profile.Description?.Get(Loc.Instance.Culture);
    public SymbolRegular Symbol { get; }
    public IReadOnlyList<PackageItem> Apps { get; }
    public string AppCountText => Loc.F("Home_ProfileApps", Apps.Count);

    public bool IsFullySelected => Apps.Count > 0 && Apps.All(a => a.IsSelected);
    public string ButtonText => IsFullySelected ? Loc.T("Home_ProfileSelected") : Loc.T("Home_SelectProfile");

    /// <summary>Selects all apps of the profile; if all are selected already, unselects them.</summary>
    [RelayCommand]
    private void Toggle()
    {
        var select = !IsFullySelected;
        foreach (var app in Apps) app.IsSelected = select;
    }
}
