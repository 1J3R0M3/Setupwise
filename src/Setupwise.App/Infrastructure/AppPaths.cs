using System.IO;

namespace Setupwise.App.Infrastructure;

public static class AppPaths
{
    public const string GitHubRepository = "1J3R0M3/Setupwise";
    public static readonly Uri GitHubUrl = new($"https://github.com/{GitHubRepository}");

    /// <summary>Microsoft Store page of "App Installer", which contains winget.</summary>
    public static readonly Uri AppInstallerStoreUrl = new("ms-windows-store://pdp/?productid=9NBLGGH4NNS1");

    /// <summary>Optional override for all data folders (used by tests): SETUPWISE_DATA_DIR.</summary>
    private static readonly string? DataOverride = Environment.GetEnvironmentVariable("SETUPWISE_DATA_DIR");

    public static string LocalData { get; } = DataOverride is { Length: > 0 }
        ? Path.Combine(DataOverride, "Local")
        : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Setupwise");

    public static string RoamingData { get; } = DataOverride is { Length: > 0 }
        ? Path.Combine(DataOverride, "Roaming")
        : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Setupwise");

    public static string IconCache => Path.Combine(LocalData, "IconCache");
    public static string Logs => Path.Combine(LocalData, "Logs");
    public static string SettingsFile => Path.Combine(RoamingData, "settings.json");
    public static string CategoriesFile => Path.Combine(RoamingData, "categories.json");

    public static Version AppVersion { get; } =
        typeof(AppPaths).Assembly.GetName().Version ?? new Version(0, 0, 0);

    public static string AppVersionText => $"{AppVersion.Major}.{AppVersion.Minor}.{AppVersion.Build}";
}
