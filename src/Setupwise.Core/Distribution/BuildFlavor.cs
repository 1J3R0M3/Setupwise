namespace Setupwise.Core.Distribution;

/// <summary>How this build of Setupwise is distributed. Set at build time with <c>-p:SetupwiseFlavor=Store</c>.</summary>
public enum BuildFlavor
{
    /// <summary>Installer and portable zip from GitHub Releases.</summary>
    Standard,

    /// <summary>MSIX package from the Microsoft Store: the Store installs and updates it.</summary>
    Store,
}

/// <summary>Features that depend on how Setupwise is distributed.</summary>
/// <param name="CheckForAppUpdates">Ask GitHub Releases for a newer Setupwise. Off in the Store: the Store updates the app.</param>
/// <param name="AutoUpdate">Download and install Setupwise updates itself (R-14). Off in the Store for the same reason.</param>
/// <param name="SelfElevation">
/// Start Setupwise or helper commands with "Run as administrator". Off in the Store: an MSIX package needs the
/// restricted capability <c>allowElevation</c> for that, and the Store only grants it under strict criteria.
/// </param>
public sealed record AppFeatures(bool CheckForAppUpdates, bool AutoUpdate, bool SelfElevation)
{
    public static AppFeatures For(BuildFlavor flavor) => flavor switch
    {
        BuildFlavor.Store => new AppFeatures(CheckForAppUpdates: false, AutoUpdate: false, SelfElevation: false),
        _ => new AppFeatures(CheckForAppUpdates: true, AutoUpdate: true, SelfElevation: true),
    };
}

public static class BuildFlavors
{
    /// <summary>Name of the assembly metadata entry that carries the flavour (see Setupwise.App.csproj).</summary>
    public const string MetadataKey = "SetupwiseFlavor";

    /// <summary>Reads the flavour from the build property; empty or unknown values mean <see cref="BuildFlavor.Standard"/>.</summary>
    public static BuildFlavor Parse(string? value) =>
        string.Equals(value?.Trim(), nameof(BuildFlavor.Store), StringComparison.OrdinalIgnoreCase)
            ? BuildFlavor.Store
            : BuildFlavor.Standard;
}
