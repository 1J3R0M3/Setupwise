using System.Windows;
using System.Windows.Threading;

namespace Setupwise.App.Views;

public partial class PackageListView : System.Windows.Controls.UserControl
{
    public PackageListView() => InitializeComponent();

    /// <summary>Puts the cursor into the search box when the search page opens.</summary>
    private void OnSearchBoxLoaded(object sender, RoutedEventArgs e)
    {
        if (sender is UIElement box) Dispatcher.BeginInvoke(() => box.Focus(), DispatcherPriority.Input);
    }
}
