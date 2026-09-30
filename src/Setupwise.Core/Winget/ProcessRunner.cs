using System.Diagnostics;
using System.Text;

namespace Setupwise.Core.Winget;

public interface IProcessRunner
{
    /// <summary>Starts a process, reports each output line and returns the exit code.</summary>
    /// <exception cref="OperationCanceledException">The token was cancelled; the process tree was killed.</exception>
    Task<int> RunAsync(string fileName, IReadOnlyList<string> arguments, Action<string> onLine, CancellationToken cancellationToken);
}

public sealed class ProcessRunner : IProcessRunner
{
    public async Task<int> RunAsync(string fileName, IReadOnlyList<string> arguments, Action<string> onLine, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(arguments);
        ArgumentNullException.ThrowIfNull(onLine);

        var psi = new ProcessStartInfo(fileName)
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
            CreateNoWindow = true,
        };
        // ArgumentList quotes every argument correctly (spaces, quotes, backslashes).
        foreach (var argument in arguments) psi.ArgumentList.Add(argument);

        using var process = new Process { StartInfo = psi };
        process.Start();

        using var registration = cancellationToken.Register(() =>
        {
            try { if (!process.HasExited) process.Kill(entireProcessTree: true); }
            catch (InvalidOperationException) { /* already exited */ }
        });

        var stdout = PumpAsync(process.StandardOutput, onLine);
        var stderr = PumpAsync(process.StandardError, onLine);
        await Task.WhenAll(stdout, stderr).ConfigureAwait(false);
        await process.WaitForExitAsync(CancellationToken.None).ConfigureAwait(false);

        cancellationToken.ThrowIfCancellationRequested();
        return process.ExitCode;
    }

    private static async Task PumpAsync(StreamReader reader, Action<string> onLine)
    {
        // ReadLine also splits on '\r', which separates the frames of winget's progress bar.
        while (await reader.ReadLineAsync().ConfigureAwait(false) is { } line)
            onLine(line);
    }
}
