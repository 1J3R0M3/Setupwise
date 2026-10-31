namespace Setupwise.Core.Distribution;

/// <summary>
/// Inside an MSIX package, new files under %APPDATA% / %LOCALAPPDATA% are written to a private per-package location
/// and merged back into the real folder only for the app itself. Other programs (e.g. File Explorer) see the real
/// folder, so folders that are opened for the user must be translated to the redirection target.
/// </summary>
public static class PackagedPaths
{
    /// <summary>
    /// Translates <paramref name="path"/> below <paramref name="realRoot"/> to the same relative path below
    /// <paramref name="redirectedRoot"/>. Returns null if the path is not below the real root.
    /// </summary>
    public static string? Redirect(string path, string realRoot, string redirectedRoot)
    {
        ArgumentNullException.ThrowIfNull(path);
        ArgumentNullException.ThrowIfNull(realRoot);
        ArgumentNullException.ThrowIfNull(redirectedRoot);
        if (realRoot.Length == 0 || redirectedRoot.Length == 0) return null;

        var root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(realRoot));
        var full = Path.GetFullPath(path);
        if (full.Equals(root, StringComparison.OrdinalIgnoreCase)) return redirectedRoot;
        if (!full.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)) return null;
        return Path.Combine(redirectedRoot, full[(root.Length + 1)..]);
    }

    /// <summary>The folder to show the user: the redirected one if it exists, otherwise the real one.</summary>
    public static string ForShell(string path, string? realRoot, string? redirectedRoot, Func<string, bool> directoryExists)
    {
        ArgumentNullException.ThrowIfNull(directoryExists);
        if (realRoot is null || redirectedRoot is null) return path;
        return Redirect(path, realRoot, redirectedRoot) is { } redirected && directoryExists(redirected) ? redirected : path;
    }
}
