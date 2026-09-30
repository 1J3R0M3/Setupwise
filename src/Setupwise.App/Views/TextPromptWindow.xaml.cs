using System.Windows;
using System.Windows.Controls;
using Wpf.Ui.Controls;

namespace Setupwise.App.Views;

/// <summary>Small dialog that asks for one line of text, e.g. a category name.</summary>
public partial class TextPromptWindow : FluentWindow
{
    private TextPromptWindow(string title, string message, string initialText)
    {
        InitializeComponent();
        Title = title;
        TitleBar.Title = title;
        MessageText.Text = message;
        Input.Text = initialText;
        Loaded += (_, _) =>
        {
            Input.Focus();
            Input.SelectAll();
        };
    }

    /// <returns>The trimmed text, or null if the user cancelled.</returns>
    public static string? Ask(string title, string message, string initialText = "")
    {
        var dialog = new TextPromptWindow(title, message, initialText) { Owner = Application.Current?.MainWindow };
        return dialog.ShowDialog() == true ? dialog.Input.Text.Trim() : null;
    }

    private void OnTextChanged(object sender, TextChangedEventArgs e) =>
        OkButton.IsEnabled = !string.IsNullOrWhiteSpace(Input.Text);

    private void OnOk(object sender, RoutedEventArgs e) => DialogResult = true;
}
