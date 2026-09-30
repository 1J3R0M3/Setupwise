using System.Windows;
using System.Windows.Controls;
using Setupwise.App.Localization;
using Setupwise.App.Models;
using Wpf.Ui.Controls;
using MenuItem = System.Windows.Controls.MenuItem;

namespace Setupwise.App.Services;

/// <summary>
/// Right-click menu of a package card: add the app to one of the user's categories
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
}
