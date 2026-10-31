using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Setupwise.App.Localization;
using Setupwise.Core.Packages;

namespace Setupwise.App.Models;

public enum PackageStatus { None, Queued, Running, Succeeded, Failed }

/// <summary>One winget package as shown in the UI. There is exactly one instance per package id.</summary>
public sealed partial class PackageItem : ObservableObject
{
    private static readonly Brush[] Palette = CreatePalette(
        "#0078D4", "#8764B8", "#038387", "#CA5010", "#498205",
        "#C239B3", "#4F6BED", "#986F0B", "#E3008C", "#00838F");

    public PackageItem(string id, string name)
    {
        Id = id;
        Name = name;
        TileBrush = Palette[StableHash(id) % (uint)Palette.Length];
    }

    public string Id { get; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Initial))]
    public partial string Name { get; set; }

    [ObservableProperty]
    public partial string? Description { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(VersionText))]
    public partial string? Version { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(VersionText), nameof(HasUpdate), nameof(Badge), nameof(Kind))]
    public partial string? AvailableVersion { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Badge))]
    public partial bool IsInstalled { get; set; }

    /// <summary>Excluded from updates with a winget pin.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Badge))]
    public partial bool IsPinned { get; set; }

    [ObservableProperty]
    public partial bool IsSelected { get; set; }

    [ObservableProperty]
    public partial PackageStatus Status { get; set; }

    [ObservableProperty]
    public partial string? StatusText { get; set; }

    /// <summary>Progress of the running operation (0..1), null while indeterminate.</summary>
    [ObservableProperty]
    public partial double? Progress { get; set; }

    [ObservableProperty]
    public partial ImageSource? Icon { get; set; }

    /// <summary>"Install for" / installation mode chosen for this app in its context menu (null = settings).</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ChoiceText))]
    public partial AppInstallChoice? Choice { get; set; }

    /// <summary>Short summary of <see cref="Choice"/> for the card, e.g. "For all users · Silent".</summary>
    public string? ChoiceText
    {
        get
        {
            if (Choice is not { IsEmpty: false } choice) return null;
            var parts = new List<string>();
            if (choice.Scope is { } scope && scope != InstallScope.Auto)
                parts.Add(Loc.T(scope == InstallScope.Machine ? "Install_Scope_Machine" : "Install_Scope_User"));
            if (choice.Mode is { } mode)
                parts.Add(Loc.T(mode switch { InstallMode.Interactive => "Choice_Interactive", InstallMode.Default => "Choice_Progress", _ => "Choice_Silent" }));
            return parts.Count == 0 ? null : string.Join(" · ", parts);
        }
    }

    public Brush TileBrush { get; }

    public string Initial => Name.FirstOrDefault(char.IsLetterOrDigit) is var c and not '\0'
        ? char.ToUpperInvariant(c).ToString()
        : "?";

    public bool HasUpdate => !string.IsNullOrEmpty(AvailableVersion);

    public OperationKind Kind => HasUpdate ? OperationKind.Upgrade : OperationKind.Install;

    /// <summary>The update version the user skipped, while it is skipped.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(VersionText))]
    public partial string? SkippedVersion { get; set; }

    public string? VersionText => HasUpdate ? $"{Version}  →  {AvailableVersion}"
        : SkippedVersion is not null ? $"{Version}  ·  {Loc.F("Skipped_Version", SkippedVersion)}"
        : Version;

    public string? Badge => HasUpdate ? Loc.T("Badge_Update")
        : IsPinned ? Loc.T("Badge_Pinned")
        : IsInstalled ? Loc.T("Badge_Installed")
        : null;

    [RelayCommand]
    private void ToggleSelected() => IsSelected = !IsSelected;

    private static uint StableHash(string text)
    {
        // string.GetHashCode is randomized per process; colors must stay the same.
        uint h = 17;
        foreach (var ch in text.ToUpperInvariant()) h = unchecked(h * 31 + ch);
        return h;
    }

    private static Brush[] CreatePalette(params string[] colors) => colors.Select(c =>
    {
        var brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(c));
        brush.Freeze();
        return (Brush)brush;
    }).ToArray();
}
