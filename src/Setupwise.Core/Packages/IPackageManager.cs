namespace Setupwise.Core.Packages;

/// <summary>
/// Abstraction over the package source. Today this is the winget command line;
/// a backend using the winget COM API can be added without touching the UI.
/// </summary>
public interface IPackageManager
{
    Task<IReadOnlyList<PackageSearchResult>> SearchAsync(string query, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<InstalledPackage>> GetInstalledAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<InstalledPackage>> GetUpgradesAsync(CancellationToken cancellationToken = default);

    Task<OperationResult> RunAsync(
        string packageId,
        OperationKind kind,
        IProgress<OperationProgress>? progress = null,
        Action<string>? log = null,
        CancellationToken cancellationToken = default);

    Task<Uri?> GetHomepageAsync(string packageId, CancellationToken cancellationToken = default);
}
