using System.IO;
using System.Reflection;
using Setupwise.Core.Updates;

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

    /// <summary>Version of this build including a pre-release suffix such as "-beta.1".</summary>
    public static AppVersion AppVersion { get; } = ReadVersion();

    public static string AppVersionText => AppVersion.ToString();

    private static AppVersion ReadVersion()
    {
        // The informational version keeps the suffix ("0.3.0-beta.1+commit"); the assembly version does not.
        var assembly = typeof(AppPaths).Assembly;
        var informational = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
        if (AppVersion.TryParse(informational, out var version)) return version;
        var plain = assembly.GetName().Version ?? new Version(0, 0, 0);
        return new AppVersion(new Version(plain.Major, plain.Minor, Math.Max(plain.Build, 0)), null);
    }
}
