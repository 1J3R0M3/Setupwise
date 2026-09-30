using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using Setupwise.App.ViewModels;

namespace Setupwise.App.Views;

public partial class PackageListView : UserControl
{
    public PackageListView()
    {
        InitializeComponent();
        // Put the cursor into the search box when the search page opens.
        Loaded += (_, _) => FocusSearch();
        DataContextChanged += (_, _) => FocusSearch();
    }

    private void FocusSearch()
    {
        if (DataContext is SearchViewModel && SearchBox.Visibility == Visibility.Visible)
            Dispatcher.BeginInvoke(() => SearchBox.Focus(), DispatcherPriority.Input);
    }
}
