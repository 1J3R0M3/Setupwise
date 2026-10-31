using System.Windows;
using System.Windows.Controls;
using Setupwise.App.Localization;
using Setupwise.App.Models;
using Setupwise.Core.Packages;
using Wpf.Ui.Controls;
using MenuItem = System.Windows.Controls.MenuItem;

namespace Setupwise.App.Services;

/// <summary>
/// Right-click menu of a package card: install or update the app right away, retry it with extra options
/// after it failed, choose "Install for" / installation mode for this app (kept), exclude it from updates,
/// skip a version, add it to one of the user's categories
/// (checked = already in there, click again to remove) or create a new category with it.
/// Attached in XAML with <c>services:PackageContextMenu.IsEnabled="True"</c>.
/// </summary>
public static class PackageContextMenu
{
    public static readonly DependencyProperty IsEnabledProperty = DependencyProperty.RegisterAttached(
        "IsEnabled", typeof(bool), typeof(PackageContextMenu), new PropertyMetadata(false, OnIsEnabledChanged));

    public static bool GetIsEnabled(DependencyObject element) => (bool)element.GetValue(IsEnabledProperty);

    public static void SetIsEnabled(DependencyObject element, bool value) => element.SetValue(IsEnabledProperty, value);

    private static void OnIsEnabledChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not FrameworkElement element || e.NewValue is not true) return;
        // An (empty) menu must exist up front, otherwise WPF does not open it on the first right-click.
        element.ContextMenu = new ContextMenu();
        element.ContextMenuOpening += OnOpening;
    }

    private static void OnOpening(object sender, ContextMenuEventArgs e)
    {
        if (sender is not FrameworkElement { DataContext: PackageItem item, ContextMenu: { } menu }
            || UserCategoriesService.Current is not { } service)
        {
            e.Handled = true;
            return;
        }

        menu.Items.Clear();

        if (PackageActions.Current is { } actions)
        {
            AddRunItems(menu, item, actions);
            menu.Items.Add(new Separator());
        }

        var addTo = new MenuItem { Header = Loc.T("Menu_AddToCategory"), Icon = new SymbolIcon(SymbolRegular.FolderAdd24) };
        foreach (var category in service.Categories)
        {
            var entry = new MenuItem { Header = category.Title, IsCheckable = true, IsChecked = category.Contains(item) };
            var target = category;
            entry.Click += (_, _) => service.Toggle(target, item);
            addTo.Items.Add(entry);
        }
        if (service.Categories.Count > 0) addTo.Items.Add(new Separator());

        var create = new MenuItem { Header = Loc.T("Menu_NewCategory"), Icon = new SymbolIcon(SymbolRegular.Add24) };
        create.Click += (_, _) => service.CreateInteractively([item]);
        addTo.Items.Add(create);

        menu.Items.Add(addTo);
        menu.Items.Add(new Separator());
        var select = new MenuItem { Header = Loc.T(item.IsSelected ? "Menu_Unselect" : "Menu_Select") };
        select.Click += (_, _) => item.IsSelected = !item.IsSelected;
        menu.Items.Add(select);
    }

    private static void AddRunItems(ContextMenu menu, PackageItem item, IPackageActions actions)
    {
        var canRun = actions.CanRun;
        var update = item.HasUpdate;

        var now = new MenuItem
        {
            Header = Loc.T(update ? "Menu_UpdateNow" : "Menu_InstallNow"),
            Icon = new SymbolIcon(update ? SymbolRegular.ArrowSync24 : SymbolRegular.ArrowDownload24),
            IsEnabled = canRun,
        };
        now.Click += async (_, _) => await actions.RunNowAsync(item);
        menu.Items.Add(now);

        // Updates often fail with a hash mismatch when the vendor replaced its installer before the catalog caught up.
        if (update)
        {
            var noHash = new MenuItem
            {
                Header = Loc.T("Menu_UpdateIgnoreHash"),
                Icon = new SymbolIcon(SymbolRegular.ShieldDismiss24),
                IsEnabled = canRun,
                ToolTip = Loc.T(actions.CanUnlockHashOverride ? "Menu_IgnoreHash_Tooltip" : "Menu_IgnoreHash_TooltipStore"),
            };
            noHash.Click += async (_, _) => await actions.RetryAsync(item, RetryVariant.IgnoreSecurityHash);
            menu.Items.Add(noHash);
        }

        // Options that are only needed when something went wrong.
        if (item.Status == PackageStatus.Failed)
        {
            var retry = new MenuItem { Header = Loc.T("Menu_Retry"), Icon = new SymbolIcon(SymbolRegular.ArrowRepeatAll24), IsEnabled = canRun };
            AddRetry(retry, item, actions, "Menu_Retry_Plain", RetryVariant.Plain);
            AddRetry(retry, item, actions, "Menu_Retry_Interactive", RetryVariant.Interactive);
            AddRetry(retry, item, actions, "Menu_Retry_Force", RetryVariant.Force);
            AddRetry(retry, item, actions, "Menu_Retry_SkipDependencies", RetryVariant.SkipDependencies);
            AddRetry(retry, item, actions, "Menu_Retry_IgnoreHash", RetryVariant.IgnoreSecurityHash);
            menu.Items.Add(retry);
        }

        // Kept for this app: every later install or update uses it.
        var choice = item.Choice ?? new AppInstallChoice();
        var defaults = actions.DefaultOptions;
        menu.Items.Add(new Separator());
        var scope = new MenuItem { Header = Loc.T("Menu_Scope"), Icon = new SymbolIcon(SymbolRegular.People24) };
        AddChoice(scope, item, actions, Loc.F("Menu_FromSettings", ScopeName(defaults.Scope)), choice.Scope is null, choice with { Scope = null });
        AddChoice(scope, item, actions, Loc.T("Install_Scope_User"), choice.Scope == InstallScope.User, choice with { Scope = InstallScope.User });
        AddChoice(scope, item, actions, Loc.T("Install_Scope_Machine"), choice.Scope == InstallScope.Machine, choice with { Scope = InstallScope.Machine });
        menu.Items.Add(scope);

        var mode = new MenuItem { Header = Loc.T("Menu_Mode"), Icon = new SymbolIcon(SymbolRegular.WindowSettings20) };
        AddChoice(mode, item, actions, Loc.F("Menu_FromSettings", ModeName(defaults.Mode)), choice.Mode is null, choice with { Mode = null });
        foreach (var value in new[] { InstallMode.Silent, InstallMode.Default, InstallMode.Interactive })
            AddChoice(mode, item, actions, ModeName(value), choice.Mode == value, choice with { Mode = value });
        menu.Items.Add(mode);

        if (item.IsInstalled || update) menu.Items.Add(new Separator());

        // "Ignore package" below replaced the winget pin ("Exclude from updates") – it did the same for the user.
        // Pins set earlier, or with winget itself, can still be lifted.
        if (item.IsPinned)
        {
            var unpin = new MenuItem { Header = Loc.T("Menu_Unpin"), Icon = new SymbolIcon(SymbolRegular.PinOff24), IsEnabled = canRun };
            unpin.Click += async (_, _) => await actions.SetPinnedAsync(item, false);
            menu.Items.Add(unpin);
        }

        if (update)
        {
            var skip = new MenuItem { Header = Loc.F("Menu_SkipVersion", item.AvailableVersion ?? string.Empty), Icon = new SymbolIcon(SymbolRegular.CalendarCancel24) };
            skip.Click += (_, _) => actions.SkipUpdate(item);
            menu.Items.Add(skip);
        }
        if (actions.IsUpdateSkipped(item))
        {
            var header = item.SkippedVersion == SkippedUpdates.AllVersions
                ? Loc.T("Menu_Unignore")
                : Loc.F("Menu_UnskipVersion", item.SkippedVersion ?? string.Empty);
            var unskip = new MenuItem { Header = header, Icon = new SymbolIcon(SymbolRegular.Eye24) };
            unskip.Click += async (_, _) => await actions.UnskipUpdateAsync(item);
            menu.Items.Add(unskip);
        }
        else if (update || item.IsInstalled)
        {
            var ignore = new MenuItem { Header = Loc.T("Menu_Ignore"), Icon = new SymbolIcon(SymbolRegular.EyeOff24) };
            ignore.Click += (_, _) => actions.IgnorePackage(item);
            menu.Items.Add(ignore);
        }
    }

    private static void AddRetry(MenuItem parent, PackageItem item, IPackageActions actions, string key, RetryVariant variant)
    {
        var entry = new MenuItem { Header = Loc.T(key) };
        entry.Click += async (_, _) => await actions.RetryAsync(item, variant);
        parent.Items.Add(entry);
    }

    private static void AddChoice(MenuItem parent, PackageItem item, IPackageActions actions, string header, bool isChecked, AppInstallChoice choice)
    {
        var entry = new MenuItem { Header = header, IsCheckable = true, IsChecked = isChecked };
        entry.Click += (_, _) => actions.SetChoice(item, choice);
        parent.Items.Add(entry);
    }

    private static string ScopeName(InstallScope scope) => scope switch
    {
        InstallScope.User => Loc.T("Install_Scope_User"),
        InstallScope.Machine => Loc.T("Install_Scope_Machine"),
        _ => Loc.T("Option_Auto"),
    };

    private static string ModeName(InstallMode mode) => Loc.T(mode switch
    {
        InstallMode.Interactive => "Choice_Interactive",
        InstallMode.Default => "Choice_Progress",
        _ => "Choice_Silent",
    });
}
