namespace Setupwise.Core.Packages;

public enum InstallMode
{
    /// <summary>No installer UI at all (winget --silent).</summary>
    Silent,
    /// <summary>winget's default: the installer shows its progress but asks nothing.</summary>
    Default,
    /// <summary>The installer's own wizard is shown (winget --interactive).</summary>
    Interactive,
}

public enum InstallScope { Auto, User, Machine }

public enum InstallArchitecture { Auto, X64, X86, Arm64 }

/// <summary>Options for install/upgrade that the user can set in the settings.</summary>
public sealed record InstallOptions
{
    public InstallMode Mode { get; init; } = InstallMode.Silent;
    public InstallScope Scope { get; init; } = InstallScope.Auto;
    public InstallArchitecture Architecture { get; init; } = InstallArchitecture.Auto;

    /// <summary>Preferred installer language, e.g. "de-DE". Null = winget decides.</summary>
    public string? Locale { get; init; }

    /// <summary>Run the installer even if the app is already installed (--force).</summary>
    public bool Force { get; init; }

    public bool SkipDependencies { get; init; }

    /// <summary>Also list updates for apps whose installed version is unknown (--include-unknown).</summary>
    public bool IncludeUnknownUpdates { get; init; }

    /// <summary>
    /// Install even if the installer's SHA256 does not match the manifest (--ignore-security-hash).
    /// Requires "winget settings --enable InstallerHashOverride" (admin).
    /// </summary>
    public bool IgnoreSecurityHash { get; init; }

    /// <summary>
    /// Skip the malware scan of archive installers (--ignore-local-archive-malware-scan).
    /// Requires "winget settings --enable LocalArchiveMalwareScanOverride" (admin).
    /// </summary>
    public bool IgnoreLocalArchiveMalwareScan { get; init; }

    public static InstallOptions Default { get; } = new();
}
