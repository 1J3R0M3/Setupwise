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

/// <summary>
/// What the user chose for one app in its context menu. Null = use the setting. The choice is kept and
/// applies to every later install or update of that app.
/// </summary>
public sealed record AppInstallChoice
{
    public InstallScope? Scope { get; init; }
    public InstallMode? Mode { get; init; }

    public bool IsEmpty => Scope is null && Mode is null;

    public InstallOptions ApplyTo(InstallOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        return IsEmpty ? options : options with { Scope = Scope ?? options.Scope, Mode = Mode ?? options.Mode };
    }
}

/// <summary>One-off extras for retrying an app that failed – the options that are only needed when something goes wrong.</summary>
public enum RetryVariant
{
    /// <summary>Same options again.</summary>
    Plain,
    /// <summary>Run the installer even if winget thinks the app is installed (--force).</summary>
    Force,
    /// <summary>Do not install the app's dependencies (--skip-dependencies).</summary>
    SkipDependencies,
    /// <summary>Show the installer's own wizard (--interactive), so its error message is visible.</summary>
    Interactive,
}

public static class RetryVariants
{
    public static InstallOptions Apply(InstallOptions options, RetryVariant variant)
    {
        ArgumentNullException.ThrowIfNull(options);
        return variant switch
        {
            RetryVariant.Force => options with { Force = true },
            RetryVariant.SkipDependencies => options with { SkipDependencies = true },
            RetryVariant.Interactive => options with { Mode = InstallMode.Interactive },
            _ => options,
        };
    }
}
