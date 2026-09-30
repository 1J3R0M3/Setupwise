using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Setupwise.App.Infrastructure;
using Setupwise.App.Localization;
using Setupwise.App.Services;
using Wpf.Ui.Controls;

namespace Setupwise.App.ViewModels;

public sealed record Choice<T>(T Value, string Label);

public sealed partial class SettingsViewModel : PageViewModel
{
    private readonly AppSettings _settings;
    private readonly IconLoader _icons;

    public SettingsViewModel(AppSettings settings, IconLoader icons)
    {
        _settings = settings;
        _icons = icons;

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
