using System.Diagnostics;
using System.Security.Principal;
using System.Windows;

namespace Setupwise.App.Services;

public static class SystemActions
{
    public static bool IsAdministrator { get; } = CheckAdministrator();

    public static void Open(Uri uri) => Open(uri.ToString());

    public static void Open(string target)
    {
        try
        {
            Process.Start(new ProcessStartInfo(target) { UseShellExecute = true })?.Dispose();
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException)
        {
            Infrastructure.AppLog.Write($"Could not open {target}: {ex.Message}");
        }
    }

    /// <summary>Starts a new instance (optionally elevated) and closes this one.</summary>
    /// <returns>False if the user declined the UAC prompt.</returns>
    public static bool Restart(bool asAdministrator)
    {
        var exe = Environment.ProcessPath;
        if (exe is null) return false;
        try
        {
            var psi = new ProcessStartInfo(exe) { UseShellExecute = true };
            if (asAdministrator) psi.Verb = "runas";
            Process.Start(psi)?.Dispose();
            Application.Current.Shutdown();
            return true;
        }
        catch (System.ComponentModel.Win32Exception)
        {
            return false; // UAC prompt cancelled
        }
    }

    private static bool CheckAdministrator()
    {
        if (!OperatingSystem.IsWindows()) return false;
        using var identity = WindowsIdentity.GetCurrent();
        return new WindowsPrincipal(identity).IsInRole(WindowsBuiltInRole.Administrator);
    }
}
