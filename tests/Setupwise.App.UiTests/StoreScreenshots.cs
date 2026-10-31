using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Setupwise.App.Infrastructure;
using Setupwise.App.Localization;
using Setupwise.App.Models;
using Setupwise.App.Services;
using Setupwise.App.ViewModels;
using Setupwise.App.Views;
using Setupwise.Core.Catalog;
using Setupwise.Core.Icons;
using Wpf.Ui.Appearance;
using Wpf.Ui.Controls;

namespace Setupwise.App.UiTests;

/// <summary>
/// Screenshots for the Microsoft Store listing (docs/store-listing.md): 1440 × 900 window rendered at 150 % →
/// 2160 × 1350 PNG in artifacts/store-screenshots, de/en × light/dark, with app icons and no error states.
/// Does nothing unless SETUPWISE_STORE_SCREENSHOTS names an icon cache folder (e.g. a copy of
/// %LOCALAPPDATA%\Setupwise\IconCache); missing icons are downloaded into it from the vendors' websites.
/// Run it on its own – WPF allows only one Application per process, and <see cref="ScreenshotTests"/> creates one too:
/// <c>dotnet test --project tests/Setupwise.App.UiTests --filter-class Setupwise.App.UiTests.StoreScreenshots</c>
/// </summary>
public class StoreScreenshots
{
    private static readonly string OutputDirectory = Path.Combine(RepositoryRoot(), "artifacts", "store-screenshots");
    private const double Scale = 1.5;

    [Fact]
    public void Render_store_screenshots()
    {
        var iconCache = Environment.GetEnvironmentVariable("SETUPWISE_STORE_SCREENSHOTS");
        if (string.IsNullOrEmpty(iconCache)) return;

        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try { RenderAll(iconCache); }
            catch (Exception ex) { failure = ex; }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
        if (failure is not null) throw new InvalidOperationException("Rendering the store screenshots failed.", failure);
    }

    private static void RenderAll(string iconCache)
    {
        Environment.SetEnvironmentVariable("SETUPWISE_DATA_DIR", Directory.CreateTempSubdirectory("setupwise-store-").FullName);
        Directory.CreateDirectory(OutputDirectory);
        SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext(Dispatcher.CurrentDispatcher));

        var app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        app.Resources.MergedDictionaries.Add(new Wpf.Ui.Markup.ThemesDictionary { Theme = ApplicationTheme.Light });
        app.Resources.MergedDictionaries.Add(new Wpf.Ui.Markup.ControlsDictionary());
        app.Resources.MergedDictionaries.Add(new ResourceDictionary
        {
            Source = new Uri("pack://application:,,,/Setupwise;component/Styles/Controls.xaml", UriKind.Absolute),
        });

        foreach (var language in new[] { "de", "en" })
        foreach (var theme in new[] { ApplicationTheme.Light, ApplicationTheme.Dark })
            RenderRun(app, language, theme, iconCache);
    }

    private static void RenderRun(Application app, string language, ApplicationTheme theme, string iconCache)
    {
        if (File.Exists(AppPaths.CategoriesFile)) File.Delete(AppPaths.CategoriesFile);
        Loc.Instance.Load(language);
        ApplicationThemeManager.Apply(theme, WindowBackdropType.None, true);

        using var http = new HttpClient();
        var settings = new AppSettings { LoadIcons = true, CheckForAppUpdates = false };
        var store = new PackageStore();
        var packages = new FakePackageManager();
        var icons = new IconLoader(new IconService(http, iconCache), packages, store) { Enabled = true };

        using var catalogStream = typeof(App).Assembly.GetManifestResourceStream("Setupwise.catalog.json")!;
        var catalog = CatalogLoader.Load(catalogStream);

        var vm = new MainViewModel(catalog, store, packages, icons, settings, http);
        // Off screen, so the mouse pointer does not leave a hover highlight in the pictures.
        var window = new MainWindow { DataContext = vm, Width = 1440, Height = 900, Left = -5000, Top = 0, WindowStartupLocation = WindowStartupLocation.Manual, WindowBackdropType = WindowBackdropType.None };
        app.MainWindow = window;
        window.Show();
        Wait(vm.InitializeAsync());

        // A typical moment: a profile picked, one update running, one app already installed.
        vm.Home.Profiles[0].ToggleCommand.Execute(null);
        var firefox = store.Find("Mozilla.Firefox.de")!;
        var vlc = store.Find("VideoLAN.VLC")!;
        firefox.Status = PackageStatus.Running; firefox.StatusText = Loc.T("Status_Updating"); firefox.Progress = 0.4;
        vlc.Status = PackageStatus.Succeeded; vlc.StatusText = Loc.T("Status_Installed");
        vm.Search.Query = "code";
        Wait(vm.Search.SearchCommand.ExecuteAsync(null));

        var prefix = $"{language}-{theme.ToString().ToLowerInvariant()}";
        var navs = vm.NavItems.Where(n => !n.IsSeparator && !n.IsHeader && n.Action is null).ToList();
        var shots = new (int Index, string Name)[] { (0, "1-start"), (1, "2-selection"), (5, "3-category"), (3, "4-updates"), (2, "5-search"), (4, "6-installed") };
        foreach (var (index, name) in shots)
        {
            vm.SelectedNav = navs[index];
            Settle();
            Save(window, $"{prefix}-{name}");
        }
        window.Close();
    }

    private static void Save(Window window, string name)
    {
        var root = (FrameworkElement)window.Content;
        var width = (int)Math.Ceiling(root.ActualWidth);
        var height = (int)Math.Ceiling(root.ActualHeight);
        var visual = new DrawingVisual();
        using (var dc = visual.RenderOpen())
        {
            var background = Application.Current.TryFindResource("ApplicationBackgroundBrush") as Brush ?? Brushes.White;
            dc.DrawRectangle(background, null, new Rect(0, 0, width, height));
            dc.DrawRectangle(new VisualBrush(root) { Stretch = Stretch.None, AlignmentX = AlignmentX.Left, AlignmentY = AlignmentY.Top },
                null, new Rect(0, 0, width, height));
        }
        var bitmap = new RenderTargetBitmap((int)(width * Scale), (int)(height * Scale), 96 * Scale, 96 * Scale, PixelFormats.Pbgra32);
        bitmap.Render(visual);
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var file = File.Create(Path.Combine(OutputDirectory, name + ".png"));
        encoder.Save(file);
    }

    /// <summary>Pumps for a few seconds so icons can load.</summary>
    private static void Settle()
    {
        var watch = Stopwatch.StartNew();
        while (watch.Elapsed < TimeSpan.FromSeconds(3)) { Pump(); Thread.Sleep(50); }
    }

    private static void Wait(Task task)
    {
        var watch = Stopwatch.StartNew();
        while (!task.IsCompleted && watch.Elapsed < TimeSpan.FromSeconds(30)) Pump();
        task.GetAwaiter().GetResult();
    }

    private static void Pump()
    {
        var frame = new DispatcherFrame();
        Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, () => frame.Continue = false);
        Dispatcher.PushFrame(frame);
    }

    private static string RepositoryRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "Setupwise.slnx"))) dir = dir.Parent;
        return dir?.FullName ?? throw new InvalidOperationException("Repository root (Setupwise.slnx) not found.");
    }
}
