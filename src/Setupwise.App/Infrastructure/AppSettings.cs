using System.IO;
using System.Text.Json;

namespace Setupwise.App.Infrastructure;

public enum ThemeSetting { System, Light, Dark }

public sealed class AppSettings
{
    /// <summary>"system" or a language code such as "en" / "de".</summary>
    public string Language { get; set; } = "system";
    public ThemeSetting Theme { get; set; } = ThemeSetting.System;
    public bool LoadIcons { get; set; } = true;
    public bool CheckForAppUpdates { get; set; } = true;

    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true };

    public static AppSettings Load()
    {
        try
        {
            if (File.Exists(AppPaths.SettingsFile))
                return JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(AppPaths.SettingsFile), Options) ?? new AppSettings();
        }
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException)
        {
            AppLog.Write($"Settings could not be read, using defaults: {ex.Message}");
        }
        return new AppSettings();
    }

    public void Save()
    {
        try
        {
            Directory.CreateDirectory(AppPaths.RoamingData);
            File.WriteAllText(AppPaths.SettingsFile, JsonSerializer.Serialize(this, Options));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            AppLog.Write($"Settings could not be saved: {ex.Message}");
        }
    }
}
