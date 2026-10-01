using Setupwise.Core.Packages;
using Setupwise.Core.Winget;

namespace Setupwise.Core.Tests;

public class WingetOutputTests
{
    [Theory]
    [InlineData("-")]
    [InlineData("\\")]
    [InlineData("  ██████▒▒▒▒  ")]
    [InlineData("1.00 MB / 5.00 MB")]
    [InlineData("45%")]
    [InlineData("   ")]
    public void Detects_noise(string line) => Assert.True(WingetOutput.IsNoise(line));

    [Theory]
    [InlineData("Found Mozilla Firefox [Mozilla.Firefox]")]
    [InlineData("Successfully installed")]
    [InlineData("--------------------")]
    public void Keeps_real_output(string line) => Assert.False(WingetOutput.IsNoise(line));

    [Theory]
    [InlineData("  ██████████▒▒▒▒▒▒▒▒▒▒  2.00 MB / 8.00 MB", 0.25)]
    [InlineData("  ████████████████████  1,5 GB / 1,5 GB", 1.0)]
    [InlineData("  ██████████          50%", 0.5)]
    public void Parses_progress(string line, double expected) =>
        Assert.Equal(expected, WingetOutput.TryParseProgress(line)!.Value, 3);

    [Fact]
    public void No_progress_in_normal_lines() =>
        Assert.Null(WingetOutput.TryParseProgress("Version 1.2.3 installed"));

    [Fact]
    public void Removes_backspaces() => Assert.Equal("Done", WingetOutput.Clean("\b\bDone  "));

    [Theory]
    [InlineData(0, OperationOutcome.Succeeded)]
    [InlineData(unchecked((int)0x8A15002B), OperationOutcome.NoApplicableUpgrade)]
    [InlineData(unchecked((int)0x8A150061), OperationOutcome.AlreadyInstalled)]
    [InlineData(unchecked((int)0x8A150109), OperationOutcome.RebootRequired)]
    [InlineData(unchecked((int)0x8A150014), OperationOutcome.NotFound)]
    [InlineData(unchecked((int)0x8A150101), OperationOutcome.AppInUse)]
    [InlineData(unchecked((int)0x8A150103), OperationOutcome.AppInUse)]
    [InlineData(unchecked((int)0x8A150111), OperationOutcome.AppInUse)]
    [InlineData(unchecked((int)0x8A150068), OperationOutcome.Pinned)]
    [InlineData(1603, OperationOutcome.Failed)]
    public void Classifies_exit_codes(int code, OperationOutcome expected) =>
        Assert.Equal(expected, WingetExitCodes.Classify(code));

    [Fact]
    public void Formats_exit_codes_as_hresult() =>
        Assert.Equal("0x8A15002B", WingetExitCodes.ToHex(WingetExitCodes.UpdateNotApplicable));

    [Theory]
    [InlineData(OperationOutcome.AlreadyInstalled, true)]
    [InlineData(OperationOutcome.RebootRequired, true)]
    [InlineData(OperationOutcome.Failed, false)]
    [InlineData(OperationOutcome.Cancelled, false)]
    public void Knows_which_outcomes_are_success(OperationOutcome outcome, bool success) =>
        Assert.Equal(success, new OperationResult(outcome, 0).IsSuccess);
}
