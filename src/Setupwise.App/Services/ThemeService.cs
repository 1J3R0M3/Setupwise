using System.Windows;
using Setupwise.App.Infrastructure;
using Wpf.Ui.Appearance;
using Wpf.Ui.Controls;

namespace Setupwise.App.Services;

public static class ThemeService
{
    public static void Apply(Window window, ThemeSetting setting)
    {
        ArgumentNullException.ThrowIfNull(window);
        if (setting == ThemeSetting.System)
        {
            ApplicationThemeManager.ApplySystemTheme(true);
            SystemThemeWatcher.Watch(window, WindowBackdropType.Mica, true);
        }
        else
        {
            SystemThemeWatcher.UnWatch(window);
            ApplicationThemeManager.Apply(
                setting == ThemeSetting.Dark ? ApplicationTheme.Dark : ApplicationTheme.Light,
                WindowBackdropType.Mica,
                true);
        }
    }
}
