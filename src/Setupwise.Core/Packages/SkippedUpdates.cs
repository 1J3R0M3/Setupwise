namespace Setupwise.Core.Packages;

/// <summary>
/// "Skip this version": winget has no such option (a gating pin would block every later version
/// too), so Setupwise remembers the skipped version itself and hides exactly that update.
/// </summary>
public static class SkippedUpdates
{
    /// <param name="skipped">Package id → skipped version.</param>
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
        // A newer version than the skipped one is shown again.
        return skipped.TryGetValue(upgrade.Id, out var version)
               && string.Equals(version, upgrade.AvailableVersion, StringComparison.OrdinalIgnoreCase);
    }
}
