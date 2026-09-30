using Setupwise.Core.Packages;

namespace Setupwise.Core.Winget;

/// <summary><see cref="IPackageManager"/> implemented on top of winget.exe.</summary>
public sealed class WingetCli : IPackageManager
{
    private const string Source = "winget";
    private readonly string _wingetPath;
    private readonly IProcessRunner _runner;

    public WingetCli(string wingetPath, IProcessRunner runner)
    {
        _wingetPath = wingetPath;
        _runner = runner;
    }

    /// <summary>Finds winget.exe, also when the app runs under a different (admin) account.</summary>
    public static string? Locate()
    {
        var candidates = new List<string>();
        foreach (var dir in (Environment.GetEnvironmentVariable("PATH") ?? string.Empty).Split(Path.PathSeparator))
        {
            if (!string.IsNullOrWhiteSpace(dir)) candidates.Add(Path.Combine(dir.Trim(), "winget.exe"));
        }
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        if (!string.IsNullOrEmpty(localAppData))
            candidates.Add(Path.Combine(localAppData, "Microsoft", "WindowsApps", "winget.exe"));

        return candidates.FirstOrDefault(File.Exists);
    }

    public async Task<string?> GetVersionAsync(CancellationToken cancellationToken = default)
    {
        var (exitCode, lines) = await RunCollectAsync(["--version"], null, cancellationToken).ConfigureAwait(false);
        return exitCode == 0 && lines.Count > 0 ? lines[0].Trim() : null;
    }

    public async Task<IReadOnlyList<PackageSearchResult>> SearchAsync(string query, CancellationToken cancellationToken = default)
    {
        var (exitCode, lines) = await RunCollectAsync(
            ["search", "--query", query, "--source", Source, "--accept-source-agreements", "--disable-interactivity"],
            null, cancellationToken).ConfigureAwait(false);

        if (exitCode == WingetExitCodes.NoApplicationsFound) return [];
        EnsureSuccess(exitCode, "search");
        return WingetTableParser.Parse(lines, 3)
            .Select(c => new PackageSearchResult(c[1], c[0], c[2]))
            .ToList();
    }

    public async Task<IReadOnlyList<InstalledPackage>> GetInstalledAsync(CancellationToken cancellationToken = default)
    {
        var (exitCode, lines) = await RunCollectAsync(
            ["list", "--source", Source, "--accept-source-agreements", "--disable-interactivity"],
            null, cancellationToken).ConfigureAwait(false);

        if (exitCode == WingetExitCodes.NoApplicationsFound) return [];
        EnsureSuccess(exitCode, "list");
        return WingetTableParser.Parse(lines, 3).Select(ToInstalled).ToList();
    }

    public async Task<IReadOnlyList<InstalledPackage>> GetUpgradesAsync(CancellationToken cancellationToken = default)
    {
        var (exitCode, lines) = await RunCollectAsync(
            ["upgrade", "--source", Source, "--accept-source-agreements", "--disable-interactivity"],
            null, cancellationToken).ConfigureAwait(false);

        if (exitCode == WingetExitCodes.NoApplicationsFound) return [];
        EnsureSuccess(exitCode, "upgrade");
        return WingetTableParser.Parse(lines, 4)
            .Select(c => new InstalledPackage(c[1], c[0], c[2], c[3]))
            .ToList();
    }

    public async Task<OperationResult> RunAsync(
        string packageId,
        OperationKind kind,
        IProgress<OperationProgress>? progress = null,
        Action<string>? log = null,
        CancellationToken cancellationToken = default)
    {
        var verb = kind == OperationKind.Upgrade ? "upgrade" : "install";
        string[] args =
        [
            verb, "--id", packageId, "--exact", "--source", Source, "--silent",
            "--accept-source-agreements", "--accept-package-agreements", "--disable-interactivity",
        ];

        void OnLine(string line)
        {
            var fraction = WingetOutput.TryParseProgress(line);
            if (fraction is not null) progress?.Report(new OperationProgress(fraction, null));
            else if (!WingetOutput.IsNoise(line)) log?.Invoke(line.Trim());
        }

        try
        {
            var (exitCode, _) = await RunCollectAsync(args, OnLine, cancellationToken).ConfigureAwait(false);
            return new OperationResult(WingetExitCodes.Classify(exitCode), exitCode);
        }
        catch (OperationCanceledException)
        {
            return new OperationResult(OperationOutcome.Cancelled, -1);
        }
    }

    public async Task<Uri?> GetHomepageAsync(string packageId, CancellationToken cancellationToken = default)
    {
        var (exitCode, lines) = await RunCollectAsync(
            ["show", "--id", packageId, "--exact", "--source", Source, "--accept-source-agreements", "--disable-interactivity"],
            null, cancellationToken).ConfigureAwait(false);
        return exitCode == 0 ? WingetShowParser.GetHomepage(lines) : null;
    }

    private static InstalledPackage ToInstalled(string[] c)
    {
        // Columns: Name, Id, Version, [Available], [Source]. "Available" only exists when an
        // update is known for at least one package; source names are never translated.
        string? available = null;
        if (c.Length >= 5) available = c[3];
        else if (c.Length == 4 && !IsSourceName(c[3])) available = c[3];
        return new InstalledPackage(c[1], c[0], c[2], string.IsNullOrWhiteSpace(available) ? null : available);
    }

    private static bool IsSourceName(string value) =>
        value.Equals("winget", StringComparison.OrdinalIgnoreCase) || value.Equals("msstore", StringComparison.OrdinalIgnoreCase);

    private static void EnsureSuccess(int exitCode, string command)
    {
        if (exitCode != 0)
            throw new WingetException($"winget {command} failed ({WingetExitCodes.ToHex(exitCode)}).", exitCode);
    }

    private async Task<(int ExitCode, IReadOnlyList<string> Lines)> RunCollectAsync(
        IReadOnlyList<string> args, Action<string>? onLine, CancellationToken cancellationToken)
    {
        var lines = new List<string>();
        var gate = new object();
        var exitCode = await _runner.RunAsync(_wingetPath, args, raw =>
        {
            var line = WingetOutput.Clean(raw);
            lock (gate)
            {
                onLine?.Invoke(line);
                if (!WingetOutput.IsNoise(line)) lines.Add(line);
            }
        }, cancellationToken).ConfigureAwait(false);

        lock (gate) return (exitCode, lines.ToList());
    }
}

public sealed class WingetException : Exception
{
    public WingetException(string message, int exitCode) : base(message) => ExitCode = exitCode;

    public WingetException() { }
    public WingetException(string message) : base(message) { }
    public WingetException(string message, Exception innerException) : base(message, innerException) { }

    public int ExitCode { get; }
}
