namespace Setupwise.Core.Packages;

/// <summary>
/// Abstraction over the package source. Today this is the winget command line;
/// a backend using the winget COM API can be added without touching the UI.
/// </summary>
public interface IPackageManager
{
    Task<IReadOnlyList<PackageSearchResult>> SearchAsync(string query, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<InstalledPackage>> GetInstalledAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<InstalledPackage>> GetUpgradesAsync(InstallOptions options, CancellationToken cancellationToken = default);

    Task<OperationResult> RunAsync(
        string packageId,
        OperationKind kind,
        InstallOptions options,
        IProgress<OperationProgress>? progress = null,
        Action<string>? log = null,
        CancellationToken cancellationToken = default);

    Task<Uri?> GetHomepageAsync(string packageId, CancellationToken cancellationToken = default);

    /// <summary>Ids of the apps that are excluded from updates (winget pins).</summary>
    Task<IReadOnlyList<string>> GetPinnedAsync(CancellationToken cancellationToken = default);

    /// <summary>Excludes the app from updates (<paramref name="pinned"/>) or allows updates again.</summary>
    Task<OperationResult> SetPinnedAsync(string packageId, bool pinned, Action<string>? log = null, CancellationToken cancellationToken = default);

    /// <summary>Runs a command the user typed in the console, e.g. ["show", "--id", "Git.Git"].</summary>
    /// <returns>The exit code.</returns>
    Task<int> RunCommandAsync(IReadOnlyList<string> arguments, Action<string> output, CancellationToken cancellationToken = default);
}
