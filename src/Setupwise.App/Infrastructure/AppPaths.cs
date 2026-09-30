using System.IO;

namespace Setupwise.App.Infrastructure;

public static class AppPaths
{
    public const string GitHubRepository = "1J3R0M3/Setupwise";
    public static readonly Uri GitHubUrl = new($"https://github.com/{GitHubRepository}");

    /// <summary>Microsoft Store page of "App Installer", which contains winget.</summary>
    public static readonly Uri AppInstallerStoreUrl = new("ms-windows-store://pdp/?productid=9NBLGGH4NNS1");

    public static string LocalData { get; } =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Setupwise");

    public static string RoamingData { get; } =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Setupwise");

    public static string IconCache => Path.Combine(LocalData, "IconCache");
    public static string Logs => Path.Combine(LocalData, "Logs");
    public static string SettingsFile => Path.Combine(RoamingData, "settings.json");

    public static Version AppVersion { get; } =
        typeof(AppPaths).Assembly.GetName().Version ?? new Version(0, 0, 0);

    public static string AppVersionText => $"{AppVersion.Major}.{AppVersion.Minor}.{AppVersion.Build}";
}
