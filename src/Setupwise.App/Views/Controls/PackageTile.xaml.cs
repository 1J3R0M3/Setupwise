using System.Windows;
using System.Windows.Controls;

namespace Setupwise.App.Views.Controls;

public partial class PackageTile : UserControl
{
    public static readonly DependencyProperty SizeProperty =
        DependencyProperty.Register(nameof(Size), typeof(double), typeof(PackageTile), new PropertyMetadata(32d));

    public PackageTile() => InitializeComponent();

    /// <summary>Edge length in device-independent pixels.</summary>
    public double Size
    {
        get => (double)GetValue(SizeProperty);
        set => SetValue(SizeProperty, value);
    }
}
