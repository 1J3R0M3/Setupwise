using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
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
/// Starts the real main window with fake data, opens every page and saves a PNG of it.
/// Fails on any exception while building a page (e.g. broken XAML) and on any binding error.
/// </summary>
public class ScreenshotTests
{
    private static readonly string OutputDirectory = Path.Combine(RepositoryRoot(), "artifacts", "screenshots");

    [Fact]
    public void Render_every_page_in_both_languages_and_themes()
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try { RenderAll(); }
            catch (Exception ex) { failure = ex; }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();

        if (failure is not null) throw new InvalidOperationException("Rendering the UI failed.", failure);
    }

    private static void RenderAll()
    {
        // Keep settings, logs and own categories of the test away from the real user profile.
        Environment.SetEnvironmentVariable("SETUPWISE_DATA_DIR", Directory.CreateTempSubdirectory("setupwise-ui-").FullName);
        Directory.CreateDirectory(OutputDirectory);
        SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext(Dispatcher.CurrentDispatcher));

        var bindingErrors = new CollectingListener();
        PresentationTraceSources.Refresh();
        PresentationTraceSources.DataBindingSource.Listeners.Add(bindingErrors);
        PresentationTraceSources.DataBindingSource.Switch.Level = SourceLevels.Error;

        // Not "new App()": WPF would run App.OnStartup (real settings, real winget, system
        // language and theme) as soon as the dispatcher runs. Only the resources are needed.
        var app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        app.Resources.MergedDictionaries.Add(new Wpf.Ui.Markup.ThemesDictionary { Theme = ApplicationTheme.Light });
        app.Resources.MergedDictionaries.Add(new Wpf.Ui.Markup.ControlsDictionary());
        app.Resources.MergedDictionaries.Add(new ResourceDictionary
        {
            Source = new Uri("pack://application:,,,/Setupwise;component/Styles/Controls.xaml", UriKind.Absolute),
        });

        RenderRun(app, "de", ApplicationTheme.Light);
        RenderRun(app, "en", ApplicationTheme.Dark);

        var errors = bindingErrors.Messages.Distinct().Order().ToList();
        File.WriteAllLines(Path.Combine(OutputDirectory, "binding-errors.txt"), errors);
        if (errors.Count > 0)
            throw new InvalidOperationException($"{errors.Count} binding error(s), see binding-errors.txt:\n{string.Join('\n', errors.Take(5))}");
    }

    private static void RenderRun(Application app, string language, ApplicationTheme theme)
    {
        File.Delete(AppPaths.CategoriesFile); // every run starts without own categories
        Loc.Instance.Load(language);
        ApplicationThemeManager.Apply(theme, WindowBackdropType.None, false);

        using var http = new HttpClient();
        var settings = new AppSettings { LoadIcons = false, CheckForAppUpdates = false };
        var store = new PackageStore();
        var packages = new FakePackageManager();
        var icons = new IconLoader(new IconService(http, Path.Combine(Path.GetTempPath(), "setupwise-ui-icons")), packages, store) { Enabled = false };

        using var catalogStream = typeof(App).Assembly.GetManifestResourceStream("Setupwise.catalog.json")!;
        var catalog = CatalogLoader.Load(catalogStream);

        var vm = new MainViewModel(catalog, store, packages, icons, settings, http);
        var window = new MainWindow { DataContext = vm, Width = 1200, Height = 820, WindowBackdropType = WindowBackdropType.None };
        app.MainWindow = window;
        window.Show();

        Wait(vm.InitializeAsync());

        // Some realistic state: a profile, a search, running and finished installs, an own category.
        vm.Home.Profiles[0].ToggleCommand.Execute(null);
        vm.Search.Query = "code";
        Wait(vm.Search.SearchCommand.ExecuteAsync(null));
        var vlc = store.Find("VideoLAN.VLC")!;
        var firefox = store.Find("Mozilla.Firefox.de")!;
        var sevenZip = store.Find("7zip.7zip")!;
        firefox.Status = PackageStatus.Running; firefox.StatusText = Loc.T("Status_Updating"); firefox.Progress = 0.4;
        vlc.Status = PackageStatus.Succeeded; vlc.StatusText = Loc.T("Status_Installed");
        sevenZip.Status = PackageStatus.Failed; sevenZip.StatusText = Loc.F("Status_Failed", "0x8A150011");
        vm.Categories.Create(language == "de" ? "PC von Oma" : "Grandma's PC", [firefox, vlc, store.Find("Zoom.Zoom")!]);

        var index = 0;
        foreach (var nav in vm.NavItems.Where(n => !n.IsSeparator && n.Action is null).ToList())
        {
            vm.SelectedNav = nav;
            Pump();
            var name = $"{language}-{theme.ToString().ToLowerInvariant()}-{index++:00}-{Slug(nav.Title)}";
            Save(window, name, full: false);
            Save(window, name + "-full", full: true);
        }

        vm.IsLogOpen = true;
        vm.ShowInfo(InfoMessage.Success(Loc.T("Result_Done_Title"), Loc.F("Result_Done", 3)));
        vm.SelectedNav = vm.NavItems[1];
        Pump();
        Save(window, $"{language}-{theme.ToString().ToLowerInvariant()}-zz-log-and-info", full: false);

        window.Close();
    }

    /// <summary>Saves the window content, or with <paramref name="full"/> the whole scrollable page.</summary>
    private static void Save(Window window, string name, bool full)
    {
        var root = (FrameworkElement)window.Content;
        FrameworkElement target = root;
        if (full)
        {
            var scroll = FindDescendants<ScrollViewer>(root).FirstOrDefault(s => s.Content is FrameworkElement { ActualHeight: > 0 } && s.ScrollableHeight > 0);
            if (scroll?.Content is not FrameworkElement content) return; // page fits on screen
            target = content;
        }

        var width = Math.Max(1, (int)Math.Ceiling(target.ActualWidth));
        var height = Math.Max(1, (int)Math.Ceiling(target.ActualHeight));
        var visual = new DrawingVisual();
        using (var dc = visual.RenderOpen())
        {
            var background = Application.Current.TryFindResource("ApplicationBackgroundBrush") as Brush ?? Brushes.White;
            dc.DrawRectangle(background, null, new Rect(0, 0, width, height));
            dc.DrawRectangle(new VisualBrush(target) { Stretch = Stretch.None, AlignmentX = AlignmentX.Left, AlignmentY = AlignmentY.Top },
                null, new Rect(0, 0, width, height));
        }

        var bitmap = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(visual);
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var file = File.Create(Path.Combine(OutputDirectory, name + ".png"));
        encoder.Save(file);
    }

    private static IEnumerable<T> FindDescendants<T>(DependencyObject parent) where T : DependencyObject
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            if (child is T match) yield return match;
            foreach (var nested in FindDescendants<T>(child)) yield return nested;
        }
    }

    private static void Wait(Task task)
    {
        var watch = Stopwatch.StartNew();
        while (!task.IsCompleted && watch.Elapsed < TimeSpan.FromSeconds(30)) Pump();
        task.GetAwaiter().GetResult();
    }

    /// <summary>Lets WPF finish layout, bindings and rendering.</summary>
    private static void Pump()
    {
        for (var i = 0; i < 3; i++)
        {
            var frame = new DispatcherFrame();
            Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, () => frame.Continue = false);
            Dispatcher.PushFrame(frame);
        }
    }

    private static string Slug(string text) =>
        new string(text.ToLowerInvariant().Select(c => char.IsLetterOrDigit(c) ? c : '-').ToArray()).Trim('-');

    private static string RepositoryRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "Setupwise.slnx"))) dir = dir.Parent;
        return dir?.FullName ?? throw new InvalidOperationException("Repository root (Setupwise.slnx) not found.");
    }

    private sealed class CollectingListener : TraceListener
    {
        public List<string> Messages { get; } = [];
        public override void Write(string? message) { }
        public override void WriteLine(string? message)
        {
            if (message is not null) Messages.Add(message);
        }
    }
}
