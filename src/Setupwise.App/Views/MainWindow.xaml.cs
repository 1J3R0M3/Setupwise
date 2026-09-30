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
    }
}
