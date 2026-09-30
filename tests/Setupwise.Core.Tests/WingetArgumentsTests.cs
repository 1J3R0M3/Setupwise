using Setupwise.Core.Packages;
using Setupwise.Core.Winget;

namespace Setupwise.Core.Tests;

public class WingetArgumentsTests
{
    [Fact]
    public void Defaults_install_silently_and_accept_agreements()
    {
        var args = WingetArguments.Operation("Git.Git", OperationKind.Install, InstallOptions.Default);

        Assert.Equal(["install", "--id", "Git.Git", "--exact", "--source", "winget", "--silent"], args.Take(7));
        Assert.Contains("--accept-package-agreements", args);
        Assert.Contains("--accept-source-agreements", args);
        Assert.Contains("--disable-interactivity", args);
        Assert.DoesNotContain("--force", args);
        Assert.DoesNotContain("--ignore-security-hash", args);
        Assert.DoesNotContain("--scope", args);
    }

    [Fact]
    public void All_options_become_arguments()
    {
        var options = new InstallOptions
        {
            Mode = InstallMode.Default,
            Scope = InstallScope.Machine,
            Architecture = InstallArchitecture.Arm64,
            Locale = " de-DE ",
            Force = true,
            SkipDependencies = true,
            IgnoreSecurityHash = true,
            IgnoreLocalArchiveMalwareScan = true,
        };

        var args = WingetArguments.Operation("Vendor.App", OperationKind.Upgrade, options);
        var line = string.Join(' ', args);

        Assert.Equal("upgrade", args[0]);
        Assert.DoesNotContain("--silent", args);
        Assert.DoesNotContain("--interactive", args);
        Assert.Contains("--scope machine", line, StringComparison.Ordinal);
        Assert.Contains("--architecture arm64", line, StringComparison.Ordinal);
        Assert.Contains("--locale de-DE", line, StringComparison.Ordinal);
        Assert.Contains("--force", args);
        Assert.Contains("--skip-dependencies", args);
        Assert.Contains("--ignore-security-hash", args);
        Assert.Contains("--ignore-local-archive-malware-scan", args);
    }

    [Fact]
    public void Interactive_mode_shows_the_installer()
    {
        var args = WingetArguments.Operation("Vendor.App", OperationKind.Install, new InstallOptions { Mode = InstallMode.Interactive });

        Assert.Contains("--interactive", args);
        Assert.DoesNotContain("--silent", args);
        Assert.DoesNotContain("--disable-interactivity", args);
    }

    [Theory]
    [InlineData(InstallScope.User, "user")]
    [InlineData(InstallScope.Machine, "machine")]
    public void Scope_values_match_winget(InstallScope scope, string expected)
    {
        var args = WingetArguments.Operation("A.B", OperationKind.Install, new InstallOptions { Scope = scope }).ToList();
        Assert.Equal(expected, args[args.IndexOf("--scope") + 1]);
    }

    [Fact]
    public void Upgrade_list_can_include_unknown_versions()
    {
        Assert.DoesNotContain("--include-unknown", WingetArguments.UpgradeList(InstallOptions.Default));
        Assert.Contains("--include-unknown", WingetArguments.UpgradeList(new InstallOptions { IncludeUnknownUpdates = true }));
    }

    [Fact]
    public void Admin_setting_command() =>
        Assert.Equal(["settings", "--enable", "InstallerHashOverride"], WingetArguments.EnableAdminSetting(WingetArguments.HashOverrideSetting));

    [Theory]
    [InlineData(unchecked((int)0x8A150011), OperationOutcome.HashMismatch)]
    [InlineData(unchecked((int)0x8A150008), OperationOutcome.DownloadFailed)]
    public void Classifies_download_and_hash_errors(int code, OperationOutcome expected) =>
        Assert.Equal(expected, WingetExitCodes.Classify(code));
}
