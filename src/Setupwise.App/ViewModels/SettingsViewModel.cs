using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Setupwise.App.Infrastructure;
using Setupwise.App.Localization;
using Setupwise.App.Services;
using Setupwise.Core.Packages;
using Setupwise.Core.Winget;
using Wpf.Ui.Controls;

namespace Setupwise.App.ViewModels;

public sealed record Choice<T>(T Value, string Label);

public sealed partial class SettingsViewModel : PageViewModel
{
    private readonly AppSettings _settings;
    private readonly IconLoader _icons;
    private readonly string? _wingetPath;
    private readonly Action<InfoMessage> _notify;
    private readonly Func<Task> _resetSkippedUpdates;
    private bool _loading = true;

    public SettingsViewModel(AppSettings settings, IconLoader icons, string? wingetPath, Action<InfoMessage> notify, Func<Task> resetSkippedUpdates)
    {
        _settings = settings;
        _icons = icons;
        _wingetPath = wingetPath;
        _notify = notify;
        _resetSkippedUpdates = resetSkippedUpdates;

        ThemeOptions =
        [
            new(ThemeSetting.System, Loc.T("Settings_Theme_System")),
            new(ThemeSetting.Light, Loc.T("Settings_Theme_Light")),
            new(ThemeSetting.Dark, Loc.T("Settings_Theme_Dark")),
        ];
        LanguageOptions = [new("system", Loc.T("Settings_Language_System")), .. Loc.Available.Select(a => new Choice<string>(a.Code, a.Name))];

        SelectedTheme = ThemeOptions.First(o => o.Value == settings.Theme);
        SelectedLanguage = LanguageOptions.FirstOrDefault(o => o.Value == settings.Language) ?? LanguageOptions[0];
        LoadIcons = settings.LoadIcons;
        CheckForAppUpdates = settings.CheckForAppUpdates;
        IncludePreReleases = settings.IncludePreReleases;

        ModeOptions =
        [
            new(InstallMode.Silent, Loc.T("Install_Mode_Silent")),
            new(InstallMode.Default, Loc.T("Install_Mode_Default")),
            new(InstallMode.Interactive, Loc.T("Install_Mode_Interactive")),
        ];
        ScopeOptions =
        [
            new(InstallScope.Auto, Loc.T("Option_Auto")),
            new(InstallScope.User, Loc.T("Install_Scope_User")),
            new(InstallScope.Machine, Loc.T("Install_Scope_Machine")),
        ];
        ArchitectureOptions =
        [
            new(InstallArchitecture.Auto, Loc.T("Option_Auto")),
            new(InstallArchitecture.X64, "x64"),
            new(InstallArchitecture.X86, "x86 (32 Bit)"),
            new(InstallArchitecture.Arm64, "ARM64"),
        ];
        LocaleOptions = [new(string.Empty, Loc.T("Option_Auto")), new("de-DE", "Deutsch (de-DE)"), new("en-US", "English (en-US)")];

        var o = settings.Install;
        SelectedMode = ModeOptions.FirstOrDefault(c => c.Value == o.Mode) ?? ModeOptions[0];
        SelectedScope = ScopeOptions.FirstOrDefault(c => c.Value == o.Scope) ?? ScopeOptions[0];
        SelectedArchitecture = ArchitectureOptions.FirstOrDefault(c => c.Value == o.Architecture) ?? ArchitectureOptions[0];
        SelectedLocale = LocaleOptions.FirstOrDefault(c => c.Value == (o.Locale ?? string.Empty)) ?? LocaleOptions[0];
        Force = o.Force;
        SkipDependencies = o.SkipDependencies;
        IncludeUnknownUpdates = o.IncludeUnknownUpdates;
        IgnoreSecurityHash = o.IgnoreSecurityHash;
        IgnoreLocalArchiveMalwareScan = o.IgnoreLocalArchiveMalwareScan;
        _loading = false;
    }

    // ---------- Installation (winget options) ----------

    public IReadOnlyList<Choice<InstallMode>> ModeOptions { get; }
    public IReadOnlyList<Choice<InstallScope>> ScopeOptions { get; }
    public IReadOnlyList<Choice<InstallArchitecture>> ArchitectureOptions { get; }
    public IReadOnlyList<Choice<string>> LocaleOptions { get; }

    [ObservableProperty] public partial Choice<InstallMode> SelectedMode { get; set; }
    [ObservableProperty] public partial Choice<InstallScope> SelectedScope { get; set; }
    [ObservableProperty] public partial Choice<InstallArchitecture> SelectedArchitecture { get; set; }
    [ObservableProperty] public partial Choice<string> SelectedLocale { get; set; }
    [ObservableProperty] public partial bool Force { get; set; }
    [ObservableProperty] public partial bool SkipDependencies { get; set; }
    [ObservableProperty] public partial bool IncludeUnknownUpdates { get; set; }
    [ObservableProperty] public partial bool IgnoreSecurityHash { get; set; }
    [ObservableProperty] public partial bool IgnoreLocalArchiveMalwareScan { get; set; }

    partial void OnSelectedModeChanged(Choice<InstallMode> value) => SaveInstallOptions();
    partial void OnSelectedScopeChanged(Choice<InstallScope> value) => SaveInstallOptions();
    partial void OnSelectedArchitectureChanged(Choice<InstallArchitecture> value) => SaveInstallOptions();
    partial void OnSelectedLocaleChanged(Choice<string> value) => SaveInstallOptions();
    partial void OnForceChanged(bool value) => SaveInstallOptions();
    partial void OnSkipDependenciesChanged(bool value) => SaveInstallOptions();
    partial void OnIncludeUnknownUpdatesChanged(bool value) => SaveInstallOptions();
    partial void OnIgnoreSecurityHashChanged(bool value) => SaveInstallOptions();
    partial void OnIgnoreLocalArchiveMalwareScanChanged(bool value) => SaveInstallOptions();

    private void SaveInstallOptions()
    {
        if (_loading) return;
        _settings.Install = new InstallOptions
        {
            Mode = SelectedMode?.Value ?? InstallMode.Silent,
            Scope = SelectedScope?.Value ?? InstallScope.Auto,
            Architecture = SelectedArchitecture?.Value ?? InstallArchitecture.Auto,
            Locale = string.IsNullOrEmpty(SelectedLocale?.Value) ? null : SelectedLocale.Value,
            Force = Force,
            SkipDependencies = SkipDependencies,
            IncludeUnknownUpdates = IncludeUnknownUpdates,
            IgnoreSecurityHash = IgnoreSecurityHash,
            IgnoreLocalArchiveMalwareScan = IgnoreLocalArchiveMalwareScan,
        };
        _settings.Save();
    }

    /// <summary>
    /// winget only accepts --ignore-security-hash / --ignore-local-archive-malware-scan after an
    /// administrator enabled them once. This runs "winget settings --enable ..." elevated.
    /// </summary>
    [RelayCommand]
    private async Task UnlockSecurityOverridesAsync()
    {
        if (_wingetPath is null) return;

        // Both settings in one elevated cmd call, so the user sees only one UAC prompt.
        // "/s /c" strips exactly the outer quotes and keeps the quoted winget path intact.
        var commands = new[] { WingetArguments.HashOverrideSetting, WingetArguments.MalwareScanOverrideSetting }
            .Select(setting => $"\"{_wingetPath}\" {string.Join(' ', WingetArguments.EnableAdminSetting(setting))}");
        var exitCode = await SystemActions.RunElevatedAsync("cmd.exe", $"/s /c \"{string.Join(" && ", commands)}\"");

        if (exitCode is null) return; // UAC prompt declined
        _notify(exitCode == 0
            ? InfoMessage.Success(Loc.T("Settings_Security"), Loc.T("Settings_UnlockDone"))
            : InfoMessage.Error(Loc.T("Error_Title"), Loc.F("Winget_Failed", WingetExitCodes.ToHex(exitCode.Value))));
    }

    public override string Title => Loc.T("Nav_Settings");
    public override SymbolRegular Symbol => SymbolRegular.Settings24;
    public override string? Subtitle => Loc.T("Settings_Subtitle");

    public IReadOnlyList<Choice<ThemeSetting>> ThemeOptions { get; }
    public IReadOnlyList<Choice<string>> LanguageOptions { get; }

    [ObservableProperty]
    public partial Choice<ThemeSetting> SelectedTheme { get; set; }

    [ObservableProperty]
    public partial Choice<string> SelectedLanguage { get; set; }

    [ObservableProperty]
    public partial bool LanguageChanged { get; set; }

    [ObservableProperty]
    public partial bool LoadIcons { get; set; }

    [ObservableProperty]
    public partial bool CheckForAppUpdates { get; set; }

    [ObservableProperty]
    public partial string? WingetVersion { get; set; }

    public bool IsAdministrator => SystemActions.IsAdministrator;
    public string AdminText => IsAdministrator ? Loc.T("Settings_AdminActive") : Loc.T("Settings_AdminDesc");
    public string VersionText => Loc.F("Settings_Version", AppPaths.AppVersionText);
    public string WingetText => WingetVersion is null ? Loc.T("Settings_WingetMissing") : Loc.F("Settings_Winget", WingetVersion);

    partial void OnWingetVersionChanged(string? value) => OnPropertyChanged(nameof(WingetText));

    partial void OnSelectedThemeChanged(Choice<ThemeSetting> value)
    {
        if (value is null) return;
        _settings.Theme = value.Value;
        _settings.Save();
        if (Application.Current?.MainWindow is { } window) ThemeService.Apply(window, value.Value);
    }

    partial void OnSelectedLanguageChanged(Choice<string> value)
    {
        if (value is null || value.Value == _settings.Language) return;
        _settings.Language = value.Value;
        _settings.Save();
        LanguageChanged = true;
    }

    partial void OnLoadIconsChanged(bool value)
    {
        _settings.LoadIcons = value;
        _settings.Save();
        _icons.Enabled = value;
    }

    partial void OnCheckForAppUpdatesChanged(bool value)
    {
        _settings.CheckForAppUpdates = value;
        _settings.Save();
    }

    [ObservableProperty]
    public partial bool IncludePreReleases { get; set; }

    partial void OnIncludePreReleasesChanged(bool value)
    {
        if (_loading) return;
        _settings.IncludePreReleases = value;
        _settings.Save();
    }

    // ---------- Skipped updates ----------

    public string SkippedUpdatesText => _settings.SkippedUpdates.Count == 0
        ? Loc.T("Settings_SkippedNone")
        : Loc.F("Settings_SkippedDesc", string.Join(", ", _settings.SkippedUpdates.Select(s => $"{s.Key} {s.Value}").Order(StringComparer.OrdinalIgnoreCase)));

    private bool HasSkippedUpdates => _settings.SkippedUpdates.Count > 0;

    /// <summary>Called when an update was skipped somewhere else.</summary>
    public void RefreshSkippedUpdates()
    {
        OnPropertyChanged(nameof(SkippedUpdatesText));
        ResetSkippedUpdatesCommand.NotifyCanExecuteChanged();
    }

    [RelayCommand(CanExecute = nameof(HasSkippedUpdates))]
    private async Task ResetSkippedUpdatesAsync()
    {
        await _resetSkippedUpdates();
        RefreshSkippedUpdates();
    }

    [RelayCommand]
    private static void RestartNow() => SystemActions.Restart(asAdministrator: false);

    [RelayCommand]
    private static void RestartAsAdministrator() => SystemActions.Restart(asAdministrator: true);

    [RelayCommand]
    private static void OpenLogs()
    {
        System.IO.Directory.CreateDirectory(AppPaths.Logs);
        SystemActions.Open(AppPaths.Logs);
    }

    [RelayCommand]
    private void ClearIconCache() => _icons.ClearCache();

    [RelayCommand]
    private static void OpenGitHub() => SystemActions.Open(AppPaths.GitHubUrl);
}
