using Setupwise.Core.Catalog;

namespace Setupwise.Core.Tests;

public sealed class UserCategoriesTests : IDisposable
{
    private readonly string _dir = Directory.CreateTempSubdirectory("setupwise-categories-").FullName;
    private string FilePath => Path.Combine(_dir, "sub", "categories.json");

    public void Dispose() => Directory.Delete(_dir, recursive: true);

    [Fact]
    public void Missing_file_means_no_categories() => Assert.Empty(UserCategoriesFile.Load(FilePath));

    [Fact]
    public void Round_trip_keeps_names_and_removes_duplicates()
    {
        var category = new UserCategory
        {
            Name = "  Oma-PC ",
            Packages = [new("Mozilla.Firefox", "Firefox"), new("mozilla.firefox", "Firefox"), new("VideoLAN.VLC", "VLC")],
        };

        UserCategoriesFile.Save(FilePath, [category]);
        var loaded = Assert.Single(UserCategoriesFile.Load(FilePath));

        Assert.Equal(category.Id, loaded.Id);
        Assert.Equal("Oma-PC", loaded.Name);
        Assert.Equal(["Mozilla.Firefox", "VideoLAN.VLC"], loaded.Packages.Select(p => p.Id));
        Assert.Equal("VLC", loaded.Packages[1].Name);
        Assert.False(File.Exists(FilePath + ".tmp"));
    }

    [Fact]
    public void Damaged_file_is_kept_aside_instead_of_being_overwritten()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
        File.WriteAllText(FilePath, "{ this is not json");

        Assert.Empty(UserCategoriesFile.Load(FilePath));
        Assert.True(File.Exists(FilePath + ".broken"));
    }
}
