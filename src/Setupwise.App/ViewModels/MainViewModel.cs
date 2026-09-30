using System.Collections.ObjectModel;
using System.Net.Http;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Setupwise.App.Infrastructure;
using Setupwise.App.Localization;
using Setupwise.App.Models;
using Setupwise.App.Services;
using Setupwise.Core.Catalog;
using Setupwise.Core.Packages;
using Setupwise.Core.Updates;
using Setupwise.Core.Winget;
using Wpf.Ui.Controls;

namespace Setupwise.App.ViewModels;

public sealed partial class NavItem : ObservableObject
{
    public NavItem(PageViewModel page) => Page = page;

    /// <summary>Creates a divider line in the navigation list.</summary>
    public static NavItem Separator() => new(null!) { IsSeparator = true };

    public PageViewModel Page { get; }
    public bool IsSeparator { get; private init; }
    public string Title => Page?.Title ?? string.Empty;
    public SymbolRegular Symbol => Page?.Symbol ?? SymbolRegular.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasBadge))]
    public partial int BadgeCount { get; set; }

    public bool HasBadge => BadgeCount > 0;
}

public sealed partial class MainViewModel : ObservableObject
{
    private const int MaxLogLines = 1000;

    private readonly PackageStore _store;
    private readonly IPackageManager? _packages;
    private readonly IconLoader _icons;
    private readonly AppSettings _settings;
    private readonly HttpClient _http;
    private readonly NavItem _selectionNav;
    private readonly NavItem _updatesNav;
    private CancellationTokenSource? _queueCancellation;

    public MainViewModel(AppCatalog catalog, PackageStore store, IPackageManager? packages, IconLoader icons, AppSettings settings, HttpClient http)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        _store = store;
        _packages = packages;
        _icons = icons;
        _settings = settings;
        _http = http;

        foreach (var app in catalog.Apps)
        {
            var item = store.GetOrCreate(app.Id, app.Name);
            item.Description = app.Description?.Get(Loc.Instance.Culture);
        }

        Home = new HomeViewModel(catalog, store, ShowInfo);
        Selection = new SelectionViewModel(store);
        Search = new SearchViewModel(packages, store, icons);
        Updates = new UpdatesViewModel(store, RefreshInstalledAsync);
        Settings = new SettingsViewModel(settings, icons);

        _selectionNav = new NavItem(Selection);
        _updatesNav = new NavItem(Updates);
        NavItems.Add(new NavItem(Home));
        NavItems.Add(_selectionNav);
        NavItems.Add(new NavItem(Search));
        NavItems.Add(_updatesNav);
        NavItems.Add(NavItem.Separator());
        foreach (var category in catalog.Categories)
            NavItems.Add(new NavItem(new CategoryViewModel(category, store, catalog.AppsIn(category.Id).Select(a => store.GetOrCreate(a.Id, a.Name)))));
        NavItems.Add(NavItem.Separator());
        NavItems.Add(new NavItem(Settings));

        store.SelectionChanged += (_, _) => OnSelectionChanged();
        AppLog.LineWritten += OnLogLine;
        SelectedNav = NavItems[0];
        OnSelectionChanged();
    }

    public HomeViewModel Home { get; }
    public SelectionViewModel Selection { get; }
    public SearchViewModel Search { get; }
    public UpdatesViewModel Updates { get; }
    public SettingsViewModel Settings { get; }

    public ObservableCollection<NavItem> NavItems { get; } = [];

    [ObservableProperty]
    public partial NavItem? SelectedNav { get; set; }

    [ObservableProperty]
    public partial PageViewModel? CurrentPage { get; set; }

    partial void OnSelectedNavChanged(NavItem? oldValue, NavItem? newValue)
    {
        if (newValue is null || newValue.IsSeparator)
        {
            // Separators are not selectable; restore the previous page.
            if (oldValue is not null && !oldValue.IsSeparator) SelectedNav = oldValue;
            return;
        }
        CurrentPage = newValue.Page;
        _icons.Request(newValue.Page.VisiblePackages);
    }

    // ---------- Selection summary & install queue ----------

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(InstallCommand))]
    public partial bool IsRunning { get; set; }

    [ObservableProperty]
    public partial string SelectionText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string StatusText { get; set; } = Loc.T("Bar_Ready");

    [ObservableProperty]
    public partial double OverallProgress { get; set; }

    [ObservableProperty]
    public partial bool IsBusyIndicatorVisible { get; set; }

    private bool CanInstall => !IsRunning && _packages is not null && _store.Selected.Count > 0;

    private void OnSelectionChanged()
    {
        var count = _store.Selected.Count;
        var updates = _store.Selected.Count(i => i.HasUpdate);
        SelectionText = count == 0 ? Loc.T("Bar_NothingSelected")
            : updates > 0 ? Loc.F("Bar_SelectedWithUpdates", count, updates)
            : Loc.F("Bar_Selected", count);
        _selectionNav.BadgeCount = count;
        InstallCommand.NotifyCanExecuteChanged();
    }

    [RelayCommand(CanExecute = nameof(CanInstall))]
    private async Task InstallAsync()
    {
        if (_packages is null) return;
        var queue = _store.Selected.ToList();
        using var cancellation = new CancellationTokenSource();
        _queueCancellation = cancellation;
        IsRunning = true;
        IsLogOpen = true;
        OverallProgress = 0;
        Info = SystemActions.IsAdministrator ? null : InfoMessage.Info(Loc.T("Bar_Install"), Loc.T("Result_AdminHint"));

        foreach (var item in queue)
        {
            item.Status = PackageStatus.Queued;
            item.StatusText = Loc.T("Status_Queued");
            item.Progress = null;
        }

        int succeeded = 0, failed = 0;
        var reboot = false;
        var done = new List<PackageItem>();
        AppLog.Write($"Starting {queue.Count} operation(s)");

        for (var i = 0; i < queue.Count; i++)
        {
            var item = queue[i];
            if (cancellation.IsCancellationRequested)
            {
                item.Status = PackageStatus.None;
                item.StatusText = null;
                continue;
            }

            var kind = item.Kind;
            var index = i;
            item.Status = PackageStatus.Running;
            item.StatusText = Loc.T(kind == OperationKind.Upgrade ? "Status_Updating" : "Status_Installing");
            StatusText = Loc.F("Bar_Progress", i + 1, queue.Count, item.Name);
            AppLog.Write($"▶ {item.Name} ({item.Id}): {kind}");

            var progress = new Progress<OperationProgress>(p =>
            {
                item.Progress = p.Fraction;
                OverallProgress = (index + (p.Fraction ?? 0)) / queue.Count;
            });

            var result = await _packages.RunAsync(item.Id, kind, progress, line => AppLog.Write("    " + line), cancellation.Token);

            item.Progress = null;
            item.Status = result.IsSuccess ? PackageStatus.Succeeded : PackageStatus.Failed;
            item.StatusText = DescribeOutcome(result, kind);
            AppLog.Write($"{(result.IsSuccess ? "✔" : "✖")} {item.Name}: {item.StatusText} ({WingetExitCodes.ToHex(result.ExitCode)})");

            if (result.IsSuccess)
            {
                succeeded++;
                reboot |= result.Outcome == OperationOutcome.RebootRequired;
                done.Add(item);
            }
            else if (result.Outcome != OperationOutcome.Cancelled)
            {
                failed++;
            }
            OverallProgress = (double)(i + 1) / queue.Count;
        }

        // Only now, so the cards stay visible while the queue runs. What stays selected
        // afterwards are exactly the apps that failed or were skipped – ready to retry.
        foreach (var item in done)
        {
            item.IsSelected = false;
            _store.MarkInstalled(item);
        }

        var skipped = queue.Count - succeeded - failed;
        Info = cancellation.IsCancellationRequested
            ? InfoMessage.Warning(Loc.T("Result_Cancelled_Title"), Loc.F("Result_Cancelled", succeeded, skipped))
            : failed > 0
                ? InfoMessage.Warning(Loc.T("Result_Errors_Title"), Loc.F("Result_Errors", succeeded, failed))
                : InfoMessage.Success(Loc.T("Result_Done_Title"), Loc.F("Result_Done", succeeded) + (reboot ? " " + Loc.T("Result_Reboot") : string.Empty));
        AppLog.Write($"Finished: {succeeded} succeeded, {failed} failed, {skipped} skipped");

        _updatesNav.BadgeCount = _store.Updates.Count;
        _queueCancellation = null;
        IsRunning = false;
        StatusText = Loc.T("Bar_Ready");
        OnSelectionChanged();
    }

    [RelayCommand]
    private void Cancel()
    {
        _queueCancellation?.Cancel();
        StatusText = Loc.T("Bar_Cancelling");
    }

    private static string DescribeOutcome(OperationResult result, OperationKind kind) => result.Outcome switch
    {
        OperationOutcome.Succeeded => Loc.T(kind == OperationKind.Upgrade ? "Status_Updated" : "Status_Installed"),
        OperationOutcome.AlreadyInstalled => Loc.T("Status_AlreadyInstalled"),
        OperationOutcome.NoApplicableUpgrade => Loc.T("Status_UpToDate"),
        OperationOutcome.RebootRequired => Loc.T("Status_Reboot"),
        OperationOutcome.NotFound => Loc.T("Status_NotFound"),
        OperationOutcome.Cancelled => Loc.T("Status_Cancelled"),
        _ => Loc.F("Status_Failed", WingetExitCodes.ToHex(result.ExitCode)),
    };

    // ---------- Startup: installed apps, winget check, app updates ----------

    /// <summary>Opens a *.setupwise file passed on the command line (e.g. by double-click).</summary>
    public async Task OpenSelectionFileAsync(string path)
    {
        if (await Home.ImportFileAsync(path)) SelectedNav = _selectionNav;
    }

    public async Task InitializeAsync()
    {
        if (_packages is null)
        {
            Info = new InfoMessage(InfoKind.Error, Loc.T("Winget_Missing_Title"), Loc.T("Winget_Missing"),
                Loc.T("Winget_OpenStore"), () => SystemActions.Open(AppPaths.AppInstallerStoreUrl));
            return;
        }

        if (_packages is WingetCli cli)
        {
            Settings.WingetVersion = await cli.GetVersionAsync();
            AppLog.Write($"Setupwise {AppPaths.AppVersionText}, winget {Settings.WingetVersion}, admin: {SystemActions.IsAdministrator}");
        }

        await Task.WhenAll(RefreshInstalledAsync(), CheckForAppUpdateAsync());
    }

    private async Task RefreshInstalledAsync()
    {
        if (_packages is null) return;
        Updates.SetChecking(true);
        IsBusyIndicatorVisible = true;
        if (!IsRunning) StatusText = Loc.T("Bar_LoadingInstalled");
        try
        {
            var installed = await _packages.GetInstalledAsync();
            var upgrades = await _packages.GetUpgradesAsync();
            _store.ApplyInstalled(installed, upgrades);
            _updatesNav.BadgeCount = _store.Updates.Count;
            AppLog.Write($"{installed.Count} installed apps, {upgrades.Count} updates available");
            _icons.Request(_store.Updates);
            OnSelectionChanged();
        }
        catch (Exception ex)
        {
            AppLog.Write($"Reading installed apps failed: {ex}");
            ShowInfo(InfoMessage.Warning(Loc.T("Error_Title"), Loc.F("Winget_Failed", ex.Message)));
        }
        finally
        {
            Updates.SetChecking(false);
            IsBusyIndicatorVisible = false;
            if (!IsRunning) StatusText = Loc.T("Bar_Ready");
        }
    }

    private async Task CheckForAppUpdateAsync()
    {
        if (!_settings.CheckForAppUpdates) return;
        var release = await new AppUpdateChecker(_http, AppPaths.GitHubRepository).FindNewerAsync(AppPaths.AppVersion);
        if (release is null || Info is not null) return;
        ShowInfo(new InfoMessage(InfoKind.Info, Loc.T("AppUpdate_Title"), Loc.F("AppUpdate_Text", release.Version.ToString(3)),
            Loc.T("AppUpdate_Download"), () => SystemActions.Open(release.PageUrl)));
    }

    // ---------- Info bar ----------

    [ObservableProperty]
    public partial InfoMessage? Info { get; set; }

    public void ShowInfo(InfoMessage message) => Info = message;

    [RelayCommand]
    private void CloseInfo() => Info = null;

    [RelayCommand]
    private void RunInfoAction() => Info?.Action?.Invoke();

    // ---------- Log panel ----------

    public ObservableCollection<string> LogLines { get; } = [];

    [ObservableProperty]
    public partial bool IsLogOpen { get; set; }

    [RelayCommand]
    private void ToggleLog() => IsLogOpen = !IsLogOpen;

    [RelayCommand]
    private void ClearLog() => LogLines.Clear();

    [RelayCommand]
    private static void OpenLogFolder()
    {
        System.IO.Directory.CreateDirectory(AppPaths.Logs);
        SystemActions.Open(AppPaths.Logs);
    }

    private void OnLogLine(string line)
    {
        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher is null) return;
        dispatcher.BeginInvoke(() =>
        {
            LogLines.Add(line);
            while (LogLines.Count > MaxLogLines) LogLines.RemoveAt(0);
        });
    }
}
