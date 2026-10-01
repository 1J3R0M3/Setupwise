namespace Setupwise.Core.Packages;

public sealed record PackageSearchResult(string Id, string Name, string Version);

public sealed record InstalledPackage(string Id, string Name, string Version, string? AvailableVersion)
{
    public bool HasUpdate => !string.IsNullOrEmpty(AvailableVersion);
}

public enum OperationKind
{
    Install,
    Upgrade,
}

public enum OperationOutcome
{
    Succeeded,
    AlreadyInstalled,
    NoApplicableUpgrade,
    RebootRequired,
    NotFound,
    HashMismatch,
    DownloadFailed,
    /// <summary>The app (or one of its files) is still in use; close it and try again.</summary>
    AppInUse,
    /// <summary>The app is excluded from updates with a winget pin.</summary>
    Pinned,
    Cancelled,
    Failed,
}

public sealed record OperationResult(OperationOutcome Outcome, int ExitCode)
{
    public bool IsSuccess => Outcome is OperationOutcome.Succeeded
        or OperationOutcome.AlreadyInstalled
        or OperationOutcome.NoApplicableUpgrade
        or OperationOutcome.RebootRequired;
}

/// <summary>Progress of a running operation. <see cref="Fraction"/> is null while indeterminate.</summary>
public sealed record OperationProgress(double? Fraction, string? Message);
