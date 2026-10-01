using Setupwise.App.Models;
using Setupwise.Core.Packages;

namespace Setupwise.App.Services;

/// <summary>What the context menu of an app card can do with the app (implemented by the main view model).</summary>
public interface IPackageActions
{
    /// <summary>False while the queue or a console command runs, or without winget.</summary>
    bool CanRun { get; }

    /// <summary>The winget options from the settings, as the base for one-off variants.</summary>
    InstallOptions DefaultOptions { get; }

    /// <summary>Installs or updates just this app right away.</summary>
    Task RunNowAsync(PackageItem item, InstallOptions options);

    /// <summary>Excludes the app from updates (winget pin) or allows updates again.</summary>
    Task SetPinnedAsync(PackageItem item, bool pinned);

    /// <summary>Hides the currently available update until a newer version appears.</summary>
    void SkipUpdate(PackageItem item);
}

public static class PackageActions
{
    /// <summary>Used by the package context menu, which lives outside the view model tree.</summary>
    public static IPackageActions? Current { get; set; }
}
