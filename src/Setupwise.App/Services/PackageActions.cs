using Setupwise.App.Models;
using Setupwise.Core.Packages;

namespace Setupwise.App.Services;

/// <summary>What the context menu of an app card can do with the app (implemented by the main view model).</summary>
public interface IPackageActions
{
    /// <summary>False while the queue or a console command runs, or without winget.</summary>
    bool CanRun { get; }

    /// <summary>The winget options from the settings; the app's own choice is applied on top.</summary>
    InstallOptions DefaultOptions { get; }

    /// <summary>Installs or updates just this app right away, with the settings and the app's own choice.</summary>
    Task RunNowAsync(PackageItem item);

    /// <summary>Runs this app right away with one extra option (after a failure, or an update without hash check).</summary>
    Task RetryAsync(PackageItem item, RetryVariant variant);

    /// <summary>False in the Store build: it cannot elevate, so "unlock in winget" is not offered there.</summary>
    bool CanUnlockHashOverride { get; }

    /// <summary>Remembers "Install for" / installation mode for this app (empty choice = back to the settings).</summary>
    void SetChoice(PackageItem item, AppInstallChoice choice);

    /// <summary>Excludes the app from updates (winget pin) or allows updates again.</summary>
    Task SetPinnedAsync(PackageItem item, bool pinned);

    /// <summary>Hides the currently available update until a newer version appears.</summary>
    void SkipUpdate(PackageItem item);

    /// <summary>True while an update of this app is hidden by "Skip this version".</summary>
    bool IsUpdateSkipped(PackageItem item);

    /// <summary>Shows the skipped update of this app again.</summary>
    Task UnskipUpdateAsync(PackageItem item);
}

public static class PackageActions
{
    /// <summary>Used by the package context menu, which lives outside the view model tree.</summary>
    public static IPackageActions? Current { get; set; }
}
