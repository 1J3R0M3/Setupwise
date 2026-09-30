using System.Collections.ObjectModel;
using System.IO;
using Setupwise.App.Infrastructure;
using Setupwise.App.Localization;
using Setupwise.App.Models;
using Setupwise.App.ViewModels;
using Setupwise.App.Views;
using Setupwise.Core.Catalog;

namespace Setupwise.App.Services;

/// <summary>Owns the user's own categories and stores every change immediately.</summary>
public sealed class UserCategoriesService
{
    private readonly PackageStore _store;
    private readonly string _path;

    public UserCategoriesService(PackageStore store, string path)
    {
        _store = store;
        _path = path;
        Current = this;
    }

    /// <summary>Used by the package context menu, which lives outside the view model tree.</summary>
    public static UserCategoriesService? Current { get; private set; }

    public ObservableCollection<CustomCategoryViewModel> Categories { get; } = [];

    public event EventHandler<CustomCategoryViewModel>? Added;
    public event EventHandler<CustomCategoryViewModel>? Removed;

    public void Load()
    {
        try
        {
            foreach (var model in UserCategoriesFile.Load(_path))
                Categories.Add(new CustomCategoryViewModel(model, _store, this));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            AppLog.Write($"Own categories could not be loaded: {ex.Message}");
        }
    }

    /// <summary>Asks for a name and creates a category with the given apps.</summary>
    public CustomCategoryViewModel? CreateInteractively(IEnumerable<PackageItem> items)
    {
        var name = TextPromptWindow.Ask(Loc.T("Custom_NewTitle"), Loc.T("Custom_NewMessage"));
        return string.IsNullOrWhiteSpace(name) ? null : Create(name, items);
    }

    public CustomCategoryViewModel Create(string name, IEnumerable<PackageItem> items)
    {
        var model = new UserCategory
        {
            Name = name,
            Packages = items.Select(i => new UserCategoryEntry(i.Id, i.Name)).DistinctBy(p => p.Id, StringComparer.OrdinalIgnoreCase).ToList(),
        };
        var category = new CustomCategoryViewModel(model, _store, this);
        Categories.Add(category);
        Save();
        Added?.Invoke(this, category);
        return category;
    }

    public void RenameInteractively(CustomCategoryViewModel category)
    {
        ArgumentNullException.ThrowIfNull(category);
        var name = TextPromptWindow.Ask(Loc.T("Custom_RenameTitle"), Loc.T("Custom_NewMessage"), category.Title);
        if (string.IsNullOrWhiteSpace(name) || name == category.Title) return;
        category.Rename(name);
        Save();
    }

    public async Task DeleteInteractivelyAsync(CustomCategoryViewModel category)
    {
        ArgumentNullException.ThrowIfNull(category);
        var box = new Wpf.Ui.Controls.MessageBox
        {
            Title = Loc.T("Custom_DeleteTitle"),
            Content = Loc.F("Custom_DeleteMessage", category.Title),
            PrimaryButtonText = Loc.T("Custom_Delete"),
            CloseButtonText = Loc.T("Dialog_Cancel"),
        };
        if (await box.ShowDialogAsync() != Wpf.Ui.Controls.MessageBoxResult.Primary) return;

        Categories.Remove(category);
        Save();
        Removed?.Invoke(this, category);
    }

    /// <summary>Adds the app to the category, or removes it if it is already in there.</summary>
    public void Toggle(CustomCategoryViewModel category, PackageItem item)
    {
        ArgumentNullException.ThrowIfNull(category);
        ArgumentNullException.ThrowIfNull(item);
        if (category.Items.Remove(item))
        {
            category.Model.Packages.RemoveAll(p => string.Equals(p.Id, item.Id, StringComparison.OrdinalIgnoreCase));
        }
        else
        {
            category.Items.Add(item);
            category.Model.Packages.Add(new UserCategoryEntry(item.Id, item.Name));
        }
        Save();
    }

    private void Save()
    {
        try
        {
            UserCategoriesFile.Save(_path, Categories.Select(c => c.Model));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            AppLog.Write($"Own categories could not be saved: {ex.Message}");
        }
    }
}
