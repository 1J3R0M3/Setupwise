using System.Collections.Specialized;
using Wpf.Ui.Controls;

namespace Setupwise.App.Views;

public partial class MainWindow : FluentWindow
{
    public MainWindow()
    {
        InitializeComponent();

        // Keep the newest log line visible.
        ((INotifyCollectionChanged)LogList.Items).CollectionChanged += (_, e) =>
        {
            if (e.Action == NotifyCollectionChangedAction.Add && LogList.Items.Count > 0)
                LogList.ScrollIntoView(LogList.Items[^1]);
        };

        // Keep the selected page visible in the navigation, e.g. a new own category at the bottom.
        NavList.SelectionChanged += (_, _) =>
        {
            if (NavList.SelectedItem is { } selected) Dispatcher.BeginInvoke(() => NavList.ScrollIntoView(selected));
        };

        // Opening the console puts the cursor into its input line.
        ConsoleBox.IsVisibleChanged += (_, e) =>
        {
            if (e.NewValue is true) Dispatcher.BeginInvoke(() => ConsoleBox.Focus());
        };
    }
}
