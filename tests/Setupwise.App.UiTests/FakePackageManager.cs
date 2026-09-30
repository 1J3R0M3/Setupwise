using Setupwise.Core.Packages;

namespace Setupwise.App.UiTests;

/// <summary>Deterministic data instead of winget, so screenshots always look the same.</summary>
internal sealed class FakePackageManager : IPackageManager
{
    public Task<IReadOnlyList<PackageSearchResult>> SearchAsync(string query, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<PackageSearchResult>>(
        [
            new("Microsoft.VisualStudioCode", "Microsoft Visual Studio Code", "1.104.2"),
            new("Microsoft.VisualStudioCode.Insiders", "Microsoft Visual Studio Code Insiders", "1.105.0"),
            new("VSCodium.VSCodium", "VSCodium", "1.104.26"),
            new("Anysphere.Cursor", "Cursor", "1.7.2"),
        ]);

    public Task<IReadOnlyList<InstalledPackage>> GetInstalledAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<InstalledPackage>>(
        [
            new("VideoLAN.VLC", "VLC media player", "3.0.21", null),
            new("7zip.7zip", "7-Zip", "25.01", null),
            new("Mozilla.Firefox.de", "Mozilla Firefox (de)", "142.0", "143.0.1"),
        ]);

    public Task<IReadOnlyList<InstalledPackage>> GetUpgradesAsync(InstallOptions options, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<InstalledPackage>>(
        [
            new("Mozilla.Firefox.de", "Mozilla Firefox (de)", "142.0", "143.0.1"),
            new("Microsoft.PowerShell", "PowerShell 7", "7.5.2", "7.5.3"),
        ]);

    public Task<OperationResult> RunAsync(string packageId, OperationKind kind, InstallOptions options,
        IProgress<OperationProgress>? progress = null, Action<string>? log = null, CancellationToken cancellationToken = default) =>
        Task.FromResult(new OperationResult(OperationOutcome.Succeeded, 0));

    public Task<Uri?> GetHomepageAsync(string packageId, CancellationToken cancellationToken = default) =>
        Task.FromResult<Uri?>(null);
}
