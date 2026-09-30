using Setupwise.Core.Packages;

namespace Setupwise.Core.Winget;

/// <summary>Builds winget command lines. Pure functions, so every option is unit-tested.</summary>
public static class WingetArguments
{
    public const string Source = "winget";

    /// <summary>Admin setting that allows --ignore-security-hash.</summary>
    public const string HashOverrideSetting = "InstallerHashOverride";

    /// <summary>Admin setting that allows --ignore-local-archive-malware-scan.</summary>
    public const string MalwareScanOverrideSetting = "LocalArchiveMalwareScanOverride";

    private static readonly string[] Agreements = ["--accept-source-agreements", "--disable-interactivity"];

    public static IReadOnlyList<string> Operation(string packageId, OperationKind kind, InstallOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        var args = new List<string>
        {
            kind == OperationKind.Upgrade ? "upgrade" : "install",
            "--id", packageId, "--exact", "--source", Source,
        };

        switch (options.Mode)
        {
            case InstallMode.Silent: args.Add("--silent"); break;
            case InstallMode.Interactive: args.Add("--interactive"); break;
        }

        switch (options.Scope)
        {
            case InstallScope.User: args.AddRange(["--scope", "user"]); break;
            case InstallScope.Machine: args.AddRange(["--scope", "machine"]); break;
        }

        switch (options.Architecture)
        {
            case InstallArchitecture.X64: args.AddRange(["--architecture", "x64"]); break;
            case InstallArchitecture.X86: args.AddRange(["--architecture", "x86"]); break;
            case InstallArchitecture.Arm64: args.AddRange(["--architecture", "arm64"]); break;
        }

        if (!string.IsNullOrWhiteSpace(options.Locale)) args.AddRange(["--locale", options.Locale.Trim()]);
        if (options.Force) args.Add("--force");
        if (options.SkipDependencies) args.Add("--skip-dependencies");
        if (options.IgnoreSecurityHash) args.Add("--ignore-security-hash");
        if (options.IgnoreLocalArchiveMalwareScan) args.Add("--ignore-local-archive-malware-scan");

        args.Add("--accept-package-agreements");
        // --disable-interactivity only concerns winget's own prompts; to be safe it is left out
        // when the user explicitly wants to see the installer.
        args.AddRange(options.Mode == InstallMode.Interactive ? ["--accept-source-agreements"] : Agreements);
        return args;
    }

    public static IReadOnlyList<string> UpgradeList(InstallOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        var args = new List<string> { "upgrade", "--source", Source };
        if (options.IncludeUnknownUpdates) args.Add("--include-unknown");
        args.AddRange(Agreements);
        return args;
    }

    /// <summary>"winget settings --enable X" – must run elevated.</summary>
    public static IReadOnlyList<string> EnableAdminSetting(string setting) => ["settings", "--enable", setting];
}
