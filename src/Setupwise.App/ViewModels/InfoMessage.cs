using Wpf.Ui.Controls;

namespace Setupwise.App.ViewModels;

public enum InfoKind { Info, Success, Warning, Error }

/// <summary>Content of the info bar at the top of the page.</summary>
public sealed record InfoMessage(InfoKind Kind, string Title, string Text, string? ActionText = null, Action? Action = null)
{
    public static InfoMessage Info(string title, string text) => new(InfoKind.Info, title, text);
    public static InfoMessage Success(string title, string text) => new(InfoKind.Success, title, text);
    public static InfoMessage Warning(string title, string text) => new(InfoKind.Warning, title, text);
    public static InfoMessage Error(string title, string text) => new(InfoKind.Error, title, text);

    public SymbolRegular Symbol => Kind switch
    {
        InfoKind.Success => SymbolRegular.CheckmarkCircle24,
        InfoKind.Warning => SymbolRegular.Warning24,
        InfoKind.Error => SymbolRegular.ErrorCircle24,
        _ => SymbolRegular.Info24,
    };

    public bool HasAction => ActionText is not null && Action is not null;
}
