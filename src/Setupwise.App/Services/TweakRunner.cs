using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Reflection;
using Microsoft.Win32;
using Setupwise.App.Infrastructure;
using Setupwise.Core.Tweaks;

namespace Setupwise.App.Services;

/// <summary>Outcome of applying or undoing tweaks: null exit code = the UAC prompt was declined.</summary>
public sealed record TweakRunResult(int? ExitCode, IReadOnlyList<string> Log)
{
    public bool Cancelled => ExitCode is null;
    public bool Succeeded => ExitCode == 0;
}

/// <summary>
/// Runs the tweak script (<see cref="TweakScript"/>) with Windows PowerShell – elevated (one UAC prompt) as soon as
/// one value outside HKCU is touched or a restore point is wanted – and reads the registry for the current state.
/// </summary>
public static class TweakRunner
{
    /// <summary>The tweak list embedded into the installer/zip build; null in the Store build.</summary>
    public static TweakCatalog? LoadCatalog()
    {
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("Setupwise.tweaks.json");
        if (stream is null) return null;
        var catalog = TweakLoader.Load(stream);
        foreach (var error in TweakLoader.Validate(catalog)) AppLog.Write($"Tweaks: {error}");
        return catalog;
    }

    public static async Task<TweakRunResult> RunAsync(IReadOnlyList<Tweak> tweaks, bool undo, bool restorePoint)
    {
        Directory.CreateDirectory(AppPaths.Logs);
        var stamp = DateTime.Now.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture);
        var script = Path.Combine(AppPaths.Logs, $"tweaks-{stamp}.ps1");
        var log = Path.Combine(AppPaths.Logs, $"tweaks-{stamp}.log");
        await File.WriteAllTextAsync(script, TweakScript.Build(tweaks, undo, restorePoint), new System.Text.UTF8Encoding(true)); // BOM: PowerShell 5.1 reads UTF-8 only with it

        var powershell = Path.Combine(Environment.SystemDirectory, @"WindowsPowerShell\v1.0\powershell.exe");
        var arguments = $"-NoProfile -NonInteractive -ExecutionPolicy Bypass -File \"{script}\" -Log \"{log}\"";
        var elevate = restorePoint || tweaks.Any(t => t.NeedsAdministrator);
        AppLog.Write($"{(undo ? "Undoing" : "Applying")} {tweaks.Count} tweak(s){(elevate ? " as administrator" : string.Empty)}: {string.Join(", ", tweaks.Select(t => t.Id))}");

        int? exitCode;
        try
        {
            exitCode = elevate ? await SystemActions.RunElevatedAsync(powershell, arguments) : await RunAsync(powershell, arguments);
        }
        finally
        {
            try { File.Delete(script); }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }

        var lines = File.Exists(log) ? await File.ReadAllLinesAsync(log) : [];
        foreach (var line in lines) AppLog.Write("    " + line);
        AppLog.Write(exitCode is null ? "Tweaks: administrator prompt declined" : $"Tweaks finished, {exitCode} value(s) failed");
        return new TweakRunResult(exitCode, lines);
    }

    private static async Task<int?> RunAsync(string fileName, string arguments)
    {
        using var process = Process.Start(new ProcessStartInfo(fileName, arguments) { UseShellExecute = false, CreateNoWindow = true });
        if (process is null) return null;
        await process.WaitForExitAsync();
        return process.ExitCode;
    }

    /// <summary>Reads a value as text, e.g. ("HKCU\Control Panel\Mouse", "MouseSpeed") → "1"; null if it does not exist.</summary>
    public static string? ReadRegistry(string path, string name)
    {
        if (!OperatingSystem.IsWindows()) return null;
        var index = path.IndexOf('\\', StringComparison.Ordinal);
        var hive = path[..index].ToUpperInvariant() switch
        {
            "HKCU" => RegistryHive.CurrentUser,
            "HKLM" => RegistryHive.LocalMachine,
            "HKU" => RegistryHive.Users,
            _ => (RegistryHive?)null,
        };
        if (hive is null) return null;
        try
        {
            using var root = RegistryKey.OpenBaseKey(hive.Value, RegistryView.Registry64);
            using var key = root.OpenSubKey(path[(index + 1)..]);
            return key?.GetValue(name, null, RegistryValueOptions.DoNotExpandEnvironmentNames) switch
            {
                null => null,
                int i => i.ToString(CultureInfo.InvariantCulture),
                long l => l.ToString(CultureInfo.InvariantCulture),
                var other => Convert.ToString(other, CultureInfo.InvariantCulture),
            };
        }
        catch (Exception ex) when (ex is System.Security.SecurityException or UnauthorizedAccessException or IOException)
        {
            return null;
        }
    }
}
