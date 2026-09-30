using System.Globalization;
using Setupwise.Core.Packages;

namespace Setupwise.Core.Winget;

/// <summary>Maps winget HRESULT exit codes to outcomes.</summary>
/// <remarks>See https://github.com/microsoft/winget-cli/blob/master/doc/windows/package-manager/winget/returnCodes.md</remarks>
public static class WingetExitCodes
{
    public const int NoApplicationsFound = unchecked((int)0x8A150014);
    public const int UpdateNotApplicable = unchecked((int)0x8A15002B);
    public const int PackageAlreadyInstalled = unchecked((int)0x8A150061);
    public const int RebootRequiredToFinish = unchecked((int)0x8A150109);

    public static OperationOutcome Classify(int exitCode) => exitCode switch
    {
        0 => OperationOutcome.Succeeded,
        UpdateNotApplicable => OperationOutcome.NoApplicableUpgrade,
        PackageAlreadyInstalled => OperationOutcome.AlreadyInstalled,
        RebootRequiredToFinish => OperationOutcome.RebootRequired,
        NoApplicationsFound => OperationOutcome.NotFound,
        _ => OperationOutcome.Failed,
    };

    public static string ToHex(int exitCode) => "0x" + exitCode.ToString("X8", CultureInfo.InvariantCulture);
}
