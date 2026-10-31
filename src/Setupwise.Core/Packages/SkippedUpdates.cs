namespace Setupwise.Core.Packages;

/// <summary>
/// "Skip this version" and "Ignore package" (like UniGetUI): winget has no such options (a gating pin would block
/// every later version too, and is visible to every winget user), so Setupwise remembers them itself and hides
/// exactly those updates.
/// </summary>
public static class SkippedUpdates
{
    /// <summary>Stored instead of a version: every update of the package is ignored.</summary>
    public const string AllVersions = "*";

    /// <param name="skipped">Package id → skipped version, or <see cref="AllVersions"/>.</param>
    public static IReadOnlyList<InstalledPackage> Remove(IEnumerable<InstalledPackage> upgrades, IReadOnlyDictionary<string, string> skipped)
    {
        ArgumentNullException.ThrowIfNull(upgrades);
        ArgumentNullException.ThrowIfNull(skipped);
        return upgrades.Where(u => !IsSkipped(u, skipped)).ToList();
    }

    public static bool IsSkipped(InstalledPackage upgrade, IReadOnlyDictionary<string, string> skipped)
    {
        ArgumentNullException.ThrowIfNull(upgrade);
        ArgumentNullException.ThrowIfNull(skipped);
        if (!skipped.TryGetValue(upgrade.Id, out var version)) return false;
        // An ignored package stays hidden; after skipping one version, a newer one is shown again.
        return version == AllVersions || string.Equals(version, upgrade.AvailableVersion, StringComparison.OrdinalIgnoreCase);
    }
}
