using System.IO;
using System.Runtime.InteropServices;
using Setupwise.Core.Distribution;

namespace Setupwise.App.Infrastructure;

/// <summary>Package identity of the running process (set when Setupwise runs from its MSIX package).</summary>
public static partial class PackageInfo
{
    private const int ErrorSuccess = 0;
    private const int ErrorInsufficientBuffer = 122;
    private const uint KfFlagReturnFilterRedirectionTarget = 0x00040000;
    private static readonly Guid FolderIdLocalAppData = new("F1B32785-6FBA-4FCF-9D55-7B8E7F157091");
    private static readonly Guid FolderIdRoamingAppData = new("3EB685DB-65F9-4CF6-A03A-E3EF65729F3D");

    /// <summary>Package family name, or null when Setupwise runs without package identity (installer, zip, dotnet run).</summary>
    public static string? FamilyName { get; } = ReadFamilyName();

    public static bool IsPackaged => FamilyName is not null;

    /// <summary>
    /// The folder File Explorer should open for a data folder. Inside the package, new files under AppData are
    /// redirected to a per-package location that only Setupwise sees at the original path.
    /// </summary>
    public static string ForShell(string folder)
    {
        if (!IsPackaged) return folder;
        var local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var roaming = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var shown = PackagedPaths.ForShell(folder, local, RedirectionTarget(FolderIdLocalAppData), Directory.Exists);
        return shown != folder ? shown : PackagedPaths.ForShell(folder, roaming, RedirectionTarget(FolderIdRoamingAppData), Directory.Exists);
    }

    private static string? ReadFamilyName()
    {
        if (!OperatingSystem.IsWindowsVersionAtLeast(8)) return null;
        try
        {
            uint length = 0;
            if (GetCurrentPackageFamilyName(ref length, null) != ErrorInsufficientBuffer) return null; // no package identity
            var buffer = new char[length];
            return GetCurrentPackageFamilyName(ref length, buffer) == ErrorSuccess ? new string(buffer, 0, (int)length - 1) : null;
        }
        catch (EntryPointNotFoundException)
        {
            return null;
        }
    }

    /// <summary>Where Windows really stores new files of a redirected known folder (Windows 10 1703+).</summary>
    private static string? RedirectionTarget(Guid folderId)
    {
        if (SHGetKnownFolderPath(folderId, KfFlagReturnFilterRedirectionTarget, IntPtr.Zero, out var path) != ErrorSuccess) return null;
        try
        {
            return Marshal.PtrToStringUni(path);
        }
        finally
        {
            Marshal.FreeCoTaskMem(path);
        }
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, ExactSpelling = true)]
    private static extern int GetCurrentPackageFamilyName(ref uint packageFamilyNameLength, [Out] char[]? packageFamilyName);

    [DllImport("shell32.dll", CharSet = CharSet.Unicode, ExactSpelling = true)]
    private static extern int SHGetKnownFolderPath(in Guid rfid, uint dwFlags, IntPtr hToken, out IntPtr ppszPath);
}
