using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Setupwise.App.Localization;
using Setupwise.App.Services;
using Setupwise.Core.Tweaks;
using Wpf.Ui.Controls;

namespace Setupwise.App.ViewModels;

/// <summary>Windows tweaks (R-19), only in the installer/zip build: tick tweaks, apply them or undo them.</summary>
public sealed partial class TweaksViewModel : PageViewModel
{
    private readonly Func<string, string, string?> _readRegistry;
    private readonly Action<InfoMessage> _notify;

    /// <param name="readRegistry">(path, value name) → value; the UI test passes a fake so it never reads the real PC.</param>
    public TweaksViewModel(TweakCatalog catalog, Func<string, string, string?> readRegistry, Action<InfoMessage> notify)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        _readRegistry = readRegistry;
        _notify = notify;
        Groups = catalog.Categories
            .Select(c => new TweakGroupViewModel(
                c.Name.Get(Loc.Instance.Culture),
                Enum.TryParse<SymbolRegular>(c.Icon, out var symbol) ? symbol : SymbolRegular.Settings24,
                catalog.Tweaks.Where(t => t.Category == c.Id).Select(t => new TweakItemViewModel(t, OnSelectionChanged)).ToList()))
            .Where(g => g.Items.Count > 0)
            .ToList();
        RefreshStates();
    }

    public override string Title => Loc.T("Nav_Tweaks");
    public override SymbolRegular Symbol => SymbolRegular.Options24;
    public override string? Subtitle => Loc.T("Tweaks_Subtitle");

    public IReadOnlyList<TweakGroupViewModel> Groups { get; }

    private IEnumerable<TweakItemViewModel> Items => Groups.SelectMany(g => g.Items);

    /// <summary>Create a Windows restore point first (needs administrator rights; on by default).</summary>
    [ObservableProperty]
    public partial bool CreateRestorePoint { get; set; } = true;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ApplyCommand), nameof(UndoCommand))]
    public partial bool IsBusy { get; private set; }

    public string SelectionText => Items.Count(i => i.IsSelected) switch
    {
        0 => Loc.T("Tweaks_NoneSelected"),
        var n => Loc.F("Tweaks_Selected", n),
    };

    private bool CanRun => !IsBusy && Items.Any(i => i.IsSelected);

    private void OnSelectionChanged()
    {
        OnPropertyChanged(nameof(SelectionText));
        ApplyCommand.NotifyCanExecuteChanged();
        UndoCommand.NotifyCanExecuteChanged();
    }

    [RelayCommand(CanExecute = nameof(CanRun))]
    private Task ApplyAsync() => RunAsync(undo: false);

    [RelayCommand(CanExecute = nameof(CanRun))]
    private Task UndoAsync() => RunAsync(undo: true);

    private async Task RunAsync(bool undo)
    {
        var selected = Items.Where(i => i.IsSelected).ToList();
        IsBusy = true;
        try
        {
            var result = await TweakRunner.RunAsync(selected.Select(i => i.Tweak).ToList(), undo, CreateRestorePoint && !undo);
            if (result.Cancelled)
            {
                _notify(InfoMessage.Warning(Loc.T("Tweaks_Title"), Loc.T("Tweaks_Declined")));
                return;
            }
            foreach (var item in selected) item.IsSelected = false;
            _notify(result.Succeeded
                ? InfoMessage.Success(Loc.T("Tweaks_Title"), Loc.F(undo ? "Tweaks_Undone" : "Tweaks_Applied", selected.Count))
                : InfoMessage.Warning(Loc.T("Tweaks_Title"), Loc.F("Tweaks_Failed", result.ExitCode ?? 0)));
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or System.IO.IOException or UnauthorizedAccessException)
        {
            _notify(InfoMessage.Error(Loc.T("Error_Title"), Loc.F("Tweaks_Error", ex.Message)));
        }
        finally
        {
            IsBusy = false;
            RefreshStates();
        }
    }

    [RelayCommand]
    private void RefreshStates()
    {
        foreach (var item in Items) item.State = TweakStates.Evaluate(item.Tweak, _readRegistry);
    }
}

public sealed record TweakGroupViewModel(string Title, SymbolRegular Symbol, IReadOnlyList<TweakItemViewModel> Items);

public sealed partial class TweakItemViewModel : ObservableObject
{
    private readonly Action _selectionChanged;

    public TweakItemViewModel(Tweak tweak, Action selectionChanged)
    {
        Tweak = tweak;
        _selectionChanged = selectionChanged;
    }

    public Tweak Tweak { get; }
    public string Name => Tweak.Name.Get(Loc.Instance.Culture);
    public string? Description => Tweak.Description?.Get(Loc.Instance.Culture);
    public bool NeedsAdministrator => Tweak.NeedsAdministrator;

    [ObservableProperty]
    public partial bool IsSelected { get; set; }

    partial void OnIsSelectedChanged(bool value) => _selectionChanged();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(StateText), nameof(IsOn))]
    public partial TweakState State { get; set; }

    public bool IsOn => State == TweakState.On;

    public string StateText => Loc.T(State switch
    {
        TweakState.On => "Tweaks_StateOn",
        TweakState.Partly => "Tweaks_StatePartly",
        _ => "Tweaks_StateOff",
    });
}
