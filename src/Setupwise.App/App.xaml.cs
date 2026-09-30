using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Windows;
using System.Windows.Threading;
using Setupwise.App.Infrastructure;
using Setupwise.App.Localization;
using Setupwise.App.Models;
using Setupwise.App.Services;
using Setupwise.App.ViewModels;
using Setupwise.App.Views;
using Setupwise.Core.Catalog;
using Setupwise.Core.Icons;
using Setupwise.Core.Packages;
using Setupwise.Core.Selection;
using Setupwise.Core.Winget;

namespace Setupwise.App;

public partial class App : Application
{
    private HttpClient? _http;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        DispatcherUnhandledException += OnUnhandledException;

        try
        {
            var settings = AppSettings.Load();
            Loc.Instance.Load(settings.Language);

            var catalog = LoadCatalog();
            var store = new PackageStore();

            var wingetPath = WingetCli.Locate();
            IPackageManager? packages = wingetPath is null ? null : new WingetCli(wingetPath, new ProcessRunner());
            if (wingetPath is null) AppLog.Write("winget.exe was not found");

            _http = CreateHttpClient();
            var icons = new IconLoader(new IconService(_http, AppPaths.IconCache), packages, store) { Enabled = settings.LoadIcons };

            var viewModel = new MainViewModel(catalog, store, packages, icons, settings, _http);
            var window = new MainWindow { DataContext = viewModel };
            MainWindow = window;
            ThemeService.Apply(window, settings.Theme);
            window.Show();

            if (e.Args.FirstOrDefault(a => a.EndsWith(SelectionFile.Extension, StringComparison.OrdinalIgnoreCase)) is { } file)
                await viewModel.OpenSelectionFileAsync(file);

            await viewModel.InitializeAsync();
        }
        catch (Exception ex)
        {
            AppLog.Write($"Startup failed: {ex}");
            MessageBox.Show(Loc.F("Error_Unexpected", ex.Message), "Setupwise", MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown(1);
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _http?.Dispose();
        base.OnExit(e);
    }

    private static AppCatalog LoadCatalog()
    {
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("Setupwise.catalog.json")
                           ?? throw new InvalidOperationException("The app catalog is missing from the build.");
        var catalog = CatalogLoader.Load(stream);
        foreach (var error in CatalogLoader.Validate(catalog)) AppLog.Write($"Catalog: {error}");
        return catalog;
    }

    private static HttpClient CreateHttpClient()
    {
        var handler = new SocketsHttpHandler
        {
            AutomaticDecompression = DecompressionMethods.All,
            PooledConnectionLifetime = TimeSpan.FromMinutes(5),
        };
        var http = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(8) };
        // Some vendor sites reject requests without a browser-like user agent.
        http.DefaultRequestHeaders.UserAgent.ParseAdd($"Mozilla/5.0 (Windows NT 10.0; Win64; x64) Setupwise/{AppPaths.AppVersionText}");
        return http;
    }

    private void OnUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        AppLog.Write($"Unhandled exception: {e.Exception}");
        MessageBox.Show(Loc.F("Error_Unexpected", e.Exception.Message), "Setupwise", MessageBoxButton.OK, MessageBoxImage.Error);
        e.Handled = true;
    }
}
