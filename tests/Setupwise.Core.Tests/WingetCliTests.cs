using Setupwise.Core.Packages;
using Setupwise.Core.Winget;

namespace Setupwise.Core.Tests;

public class WingetCliTests
{
    private sealed class FakeRunner(int exitCode, params string[] output) : IProcessRunner
    {
        public IReadOnlyList<string>? LastArguments { get; private set; }

        public Task<int> RunAsync(string fileName, IReadOnlyList<string> arguments, Action<string> onLine, CancellationToken cancellationToken)
        {
            LastArguments = arguments;
            foreach (var line in output) onLine(line);
            return Task.FromResult(exitCode);
        }
    }

    [Fact]
    public async Task Search_parses_results_and_passes_the_query_as_one_argument()
    {
        var runner = new FakeRunner(0,
            "-", "\\",
            "Name               Id                         Version",
            "-----------------------------------------------------",
            "Visual Studio Code Microsoft.VisualStudioCode 1.95.0");
        var cli = new WingetCli("winget.exe", runner);

        var results = await cli.SearchAsync("visual studio", TestContext.Current.CancellationToken);

        var result = Assert.Single(results);
        Assert.Equal(new PackageSearchResult("Microsoft.VisualStudioCode", "Visual Studio Code", "1.95.0"), result);
        Assert.Contains("visual studio", runner.LastArguments!);
    }

    [Fact]
    public async Task Search_without_hits_returns_empty()
    {
        var cli = new WingetCli("winget.exe", new FakeRunner(WingetExitCodes.NoApplicationsFound, "No package found."));
        Assert.Empty(await cli.SearchAsync("xyz", TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Search_failure_throws()
    {
        var cli = new WingetCli("winget.exe", new FakeRunner(unchecked((int)0x8A15000F), "Failed"));
        await Assert.ThrowsAsync<WingetException>(() => cli.SearchAsync("x", TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task List_distinguishes_available_version_from_source_column()
    {
        var cli = new WingetCli("winget.exe", new FakeRunner(0,
            "Name    Id              Version Available Source",
            "------------------------------------------------",
            "Firefox Mozilla.Firefox 130.0   131.0     winget",
            "VLC     VideoLAN.VLC    3.0.21            winget"));

        var installed = await cli.GetInstalledAsync(TestContext.Current.CancellationToken);

        Assert.Equal("131.0", installed[0].AvailableVersion);
        Assert.True(installed[0].HasUpdate);
        Assert.Null(installed[1].AvailableVersion);
    }

    [Fact]
    public async Task List_without_available_column_has_no_updates()
    {
        var cli = new WingetCli("winget.exe", new FakeRunner(0,
            "Name Id           Version Source",
            "--------------------------------",
            "VLC  VideoLAN.VLC 3.0.21  winget"));

        var package = Assert.Single(await cli.GetInstalledAsync(TestContext.Current.CancellationToken));
        Assert.False(package.HasUpdate);
    }

    [Fact]
    public async Task Install_reports_progress_log_and_outcome()
    {
        var runner = new FakeRunner(unchecked((int)0x8A150061),
            "Found Mozilla Firefox [Mozilla.Firefox]",
            "  ██████████▒▒▒▒▒▒▒▒▒▒  2.00 MB / 4.00 MB",
            "-",
            "Package is already installed.");
        var cli = new WingetCli("winget.exe", runner);
        var fractions = new List<double?>();
        var log = new List<string>();

        var result = await cli.RunAsync("Mozilla.Firefox", OperationKind.Install, InstallOptions.Default,
            new SyncProgress(p => fractions.Add(p.Fraction)), log.Add, TestContext.Current.CancellationToken);

        Assert.Equal(OperationOutcome.AlreadyInstalled, result.Outcome);
        Assert.True(result.IsSuccess);
        Assert.Equal([0.5], fractions);
        Assert.Equal(["Found Mozilla Firefox [Mozilla.Firefox]", "Package is already installed."], log.Skip(1));
        Assert.StartsWith("winget install --id Mozilla.Firefox", log[0], StringComparison.Ordinal);
        Assert.Equal(["install", "--id", "Mozilla.Firefox", "--exact"], runner.LastArguments!.Take(4));
        Assert.Contains("--silent", runner.LastArguments!);
    }

    [Fact]
    public async Task Upgrade_uses_the_upgrade_verb()
    {
        var runner = new FakeRunner(0);
        await new WingetCli("winget.exe", runner).RunAsync("Git.Git", OperationKind.Upgrade, InstallOptions.Default, cancellationToken: TestContext.Current.CancellationToken);
        Assert.Equal("upgrade", runner.LastArguments![0]);
    }

    [Fact]
    public async Task Pin_list_returns_the_ids()
    {
        var cli = new WingetCli("winget.exe", new FakeRunner(0,
            "Name      Id                  Version Source Pin type",
            "------------------------------------------------------",
            "PowerToys Microsoft.PowerToys 0.94.0  winget Blocking",
            "7-Zip     7zip.7zip           25.01   winget Pinning"));

        Assert.Equal(["Microsoft.PowerToys", "7zip.7zip"], await cli.GetPinnedAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Pin_list_without_pins_is_empty()
    {
        var cli = new WingetCli("winget.exe", new FakeRunner(0, "There are no pins configured."));
        Assert.Empty(await cli.GetPinnedAsync(TestContext.Current.CancellationToken));
    }

    [Theory]
    [InlineData(true, 0, OperationOutcome.Succeeded)]
    [InlineData(true, unchecked((int)0x8A150062), OperationOutcome.Succeeded)] // pin already exists
    [InlineData(false, unchecked((int)0x8A150063), OperationOutcome.Succeeded)] // there was no pin
    [InlineData(false, unchecked((int)0x8A150062), OperationOutcome.Failed)]
    [InlineData(true, unchecked((int)0x8A150064), OperationOutcome.Failed)] // pin database cannot be opened
    public async Task Setting_a_pin_treats_the_goal_state_as_success(bool pinned, int exitCode, OperationOutcome expected)
    {
        var runner = new FakeRunner(exitCode);
        var result = await new WingetCli("winget.exe", runner).SetPinnedAsync("Git.Git", pinned, cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(expected, result.Outcome);
        Assert.Equal(pinned ? "add" : "remove", runner.LastArguments![1]);
    }

    [Fact]
    public async Task Console_passes_arguments_unchanged_and_hides_spinner()
    {
        var runner = new FakeRunner(0, "-", "\\", "  ██████▒▒▒▒  1.00 MB / 2.00 MB", "Found Git [Git.Git]");
        var output = new List<string>();

        var exitCode = await new WingetCli("winget.exe", runner).RunCommandAsync(["search", "visual studio & del"], output.Add, TestContext.Current.CancellationToken);

        Assert.Equal(0, exitCode);
        Assert.Equal(["search", "visual studio & del"], runner.LastArguments);
        Assert.Equal(["Found Git [Git.Git]"], output);
    }

    private sealed class SyncProgress(Action<OperationProgress> report) : IProgress<OperationProgress>
    {
        public void Report(OperationProgress value) => report(value);
    }
}
