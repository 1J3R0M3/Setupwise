using System.Globalization;
using Setupwise.Core.Packages;

namespace Setupwise.Core.Winget;

/// <summary>Maps winget HRESULT exit codes to outcomes.</summary>
/// <remarks>See https://github.com/microsoft/winget-cli/blob/master/doc/windows/package-manager/winget/returnCodes.md</remarks>
public static class WingetExitCodes
{
    public const int DownloadFailed = unchecked((int)0x8A150008);
    public const int InstallerHashMismatch = unchecked((int)0x8A150011);
    public const int NoApplicationsFound = unchecked((int)0x8A150014);
    public const int UpdateNotApplicable = unchecked((int)0x8A15002B);
    public const int PackageAlreadyInstalled = unchecked((int)0x8A150061);
    public const int PinAlreadyExists = unchecked((int)0x8A150062);
    public const int PinDoesNotExist = unchecked((int)0x8A150063);
    public const int PackageIsPinned = unchecked((int)0x8A150068);
    public const int PackageInUse = unchecked((int)0x8A150101);
    public const int FileInUse = unchecked((int)0x8A150103);
    public const int RebootRequiredToFinish = unchecked((int)0x8A150109);
    public const int PackageInUseByApplication = unchecked((int)0x8A150111);

    public static OperationOutcome Classify(int exitCode) => exitCode switch
    {
        0 => OperationOutcome.Succeeded,
        UpdateNotApplicable => OperationOutcome.NoApplicableUpgrade,
        PackageAlreadyInstalled => OperationOutcome.AlreadyInstalled,
        RebootRequiredToFinish => OperationOutcome.RebootRequired,
        NoApplicationsFound => OperationOutcome.NotFound,
        InstallerHashMismatch => OperationOutcome.HashMismatch,
        DownloadFailed => OperationOutcome.DownloadFailed,
        PackageInUse or FileInUse or PackageInUseByApplication => OperationOutcome.AppInUse,
        PackageIsPinned => OperationOutcome.Pinned,
        _ => OperationOutcome.Failed,
    };

    public static string ToHex(int exitCode) => "0x" + exitCode.ToString("X8", CultureInfo.InvariantCulture);
}
