using System.Globalization;
using System.IO;

namespace Setupwise.App.Infrastructure;

/// <summary>Thread-safe log: one file per day plus an event for the in-app log panel.</summary>
public static class AppLog
{
    private static readonly Lock Gate = new();

    public static event Action<string>? LineWritten;

    public static string CurrentFile =>
        Path.Combine(AppPaths.Logs, $"setupwise-{DateTime.Now.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)}.log");

    public static void Write(string message)
    {
        var line = $"{DateTime.Now.ToString("HH:mm:ss", CultureInfo.InvariantCulture)}  {message}";
        lock (Gate)
        {
            try
            {
                Directory.CreateDirectory(AppPaths.Logs);
                File.AppendAllText(CurrentFile, line + Environment.NewLine);
            }
            catch (IOException) { /* logging must never crash the app */ }
            catch (UnauthorizedAccessException) { }
        }
        LineWritten?.Invoke(line);
    }
}
