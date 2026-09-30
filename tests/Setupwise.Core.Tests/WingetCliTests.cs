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

    private sealed class SyncProgress(Action<OperationProgress> report) : IProgress<OperationProgress>
    {
        public void Report(OperationProgress value) => report(value);
    }
}
