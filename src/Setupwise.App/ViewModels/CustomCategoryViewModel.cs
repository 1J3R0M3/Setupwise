using CommunityToolkit.Mvvm.Input;
using Setupwise.App.Localization;
using Setupwise.App.Models;
using Setupwise.App.Services;
using Setupwise.Core.Catalog;
using Wpf.Ui.Controls;

namespace Setupwise.App.ViewModels;

/// <summary>A category created by the user. Changes are saved by <see cref="UserCategoriesService"/>.</summary>
public sealed partial class CustomCategoryViewModel : PackageListPageViewModel
{
    private readonly UserCategoriesService _service;

    public CustomCategoryViewModel(UserCategory model, PackageStore store, UserCategoriesService service)
        : base(store, new(model.Packages.Select(p => store.GetOrCreate(p.Id, p.Name))))
    {
        Model = model;
        _service = service;
        Items.CollectionChanged += (_, _) => OnPropertyChanged(nameof(Subtitle));
    }

    public UserCategory Model { get; }

    public override string Title => Model.Name;
    public override SymbolRegular Symbol => SymbolRegular.Folder24;
    public override string? Subtitle => Loc.F("Category_Subtitle", Items.Count);
    public override string EmptyText => Loc.T("Custom_Empty");
    public override bool ShowCategoryActions => true;

    public bool Contains(PackageItem item) => Items.Contains(item);

    internal void Rename(string name)
    {
        Model.Name = name;
        OnPropertyChanged(nameof(Title));
    }

    [RelayCommand]
    private void Rename() => _service.RenameInteractively(this);

    [RelayCommand]
    private async Task DeleteAsync() => await _service.DeleteInteractivelyAsync(this);
}
