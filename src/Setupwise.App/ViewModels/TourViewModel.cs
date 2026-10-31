using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Setupwise.App.Localization;
using Wpf.Ui.Controls;

namespace Setupwise.App.ViewModels;

/// <summary>One step of the welcome tour; <paramref name="Show"/> brings the matching page to the front.</summary>
public sealed record TourStep(string Key, SymbolRegular Symbol, Action? Show = null);

/// <summary>
/// Short welcome tour (R-20): starts by itself on the first start, can be skipped at any step and
/// started again from the start page or the settings.
/// </summary>
public sealed partial class TourViewModel : ObservableObject
{
    private readonly IReadOnlyList<TourStep> _steps;
    private readonly Action _seen;

    /// <param name="seen">Called when the tour is finished or skipped (remember it, so it does not start again).</param>
    public TourViewModel(IReadOnlyList<TourStep> steps, Action seen)
    {
        ArgumentOutOfRangeException.ThrowIfZero(steps.Count);
        _steps = steps;
        _seen = seen;
    }

    [ObservableProperty]
    public partial bool IsOpen { get; private set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Title), nameof(Text), nameof(Symbol), nameof(StepText), nameof(IsFirst), nameof(IsLast), nameof(NextText), nameof(Dots))]
    [NotifyCanExecuteChangedFor(nameof(BackCommand))]
    public partial int Index { get; private set; }

    private TourStep Step => _steps[Index];

    public string Title => Loc.T($"Tour_{Step.Key}_Title");
    public string Text => Loc.T($"Tour_{Step.Key}_Text");
    public SymbolRegular Symbol => Step.Symbol;
    public string StepText => Loc.F("Tour_Step", Index + 1, _steps.Count);
    public bool IsFirst => Index == 0;
    public bool IsLast => Index == _steps.Count - 1;
    public string NextText => Loc.T(IsLast ? "Tour_Finish" : IsFirst ? "Tour_Start" : "Tour_Next");

    /// <summary>One entry per step, true for the current one (progress dots).</summary>
    public IReadOnlyList<bool> Dots => _steps.Select((_, i) => i == Index).ToList();

    [RelayCommand]
    public void Start()
    {
        Index = 0;
        IsOpen = true;
        Step.Show?.Invoke();
    }

    [RelayCommand]
    private void Next()
    {
        if (IsLast)
        {
            Close();
            return;
        }
        Index++;
        Step.Show?.Invoke();
    }

    private bool CanGoBack => Index > 0;

    [RelayCommand(CanExecute = nameof(CanGoBack))]
    private void Back()
    {
        Index--;
        Step.Show?.Invoke();
    }

    /// <summary>"Skip" and Esc: the tour can be left at any step.</summary>
    [RelayCommand]
    private void Close()
    {
        if (!IsOpen) return;
        IsOpen = false;
        _seen();
    }
}
