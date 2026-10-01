using System.Windows;
using System.Windows.Controls;
using Setupwise.App.Localization;
using Setupwise.App.Models;
using Setupwise.Core.Packages;
using Wpf.Ui.Controls;
using MenuItem = System.Windows.Controls.MenuItem;

namespace Setupwise.App.Services;

/// <summary>
/// Right-click menu of a package card: install or update the app right away (also with one-off
/// winget options), exclude it from updates, skip a version, add it to one of the user's categories
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
        now.Click += async (_, _) => await actions.RunNowAsync(item, actions.DefaultOptions);
        menu.Items.Add(now);

        // One-off variants of the options from the settings.
        var with = new MenuItem { Header = Loc.T(update ? "Menu_UpdateWith" : "Menu_InstallWith"), Icon = new SymbolIcon(SymbolRegular.Options24), IsEnabled = canRun };
        var defaults = actions.DefaultOptions;
        AddVariant(with, item, actions, "Menu_With_Interactive", defaults with { Mode = InstallMode.Interactive });
        AddVariant(with, item, actions, "Menu_With_User", defaults with { Scope = InstallScope.User });
        AddVariant(with, item, actions, "Menu_With_Machine", defaults with { Scope = InstallScope.Machine });
        AddVariant(with, item, actions, "Menu_With_Force", defaults with { Force = true });
        menu.Items.Add(with);

        if (item.IsInstalled)
        {
            menu.Items.Add(new Separator());
            var pin = new MenuItem
            {
                Header = Loc.T(item.IsPinned ? "Menu_Unpin" : "Menu_Pin"),
                Icon = new SymbolIcon(item.IsPinned ? SymbolRegular.PinOff24 : SymbolRegular.Pin24),
                IsEnabled = canRun,
            };
            var pinned = !item.IsPinned;
            pin.Click += async (_, _) => await actions.SetPinnedAsync(item, pinned);
            menu.Items.Add(pin);
        }

        if (update)
        {
            var skip = new MenuItem { Header = Loc.F("Menu_SkipVersion", item.AvailableVersion ?? string.Empty), Icon = new SymbolIcon(SymbolRegular.CalendarCancel24) };
            skip.Click += (_, _) => actions.SkipUpdate(item);
            menu.Items.Add(skip);
        }
    }

    private static void AddVariant(MenuItem parent, PackageItem item, IPackageActions actions, string key, InstallOptions options)
    {
        var entry = new MenuItem { Header = Loc.T(key) };
        entry.Click += async (_, _) => await actions.RunNowAsync(item, options);
        parent.Items.Add(entry);
    }
}
