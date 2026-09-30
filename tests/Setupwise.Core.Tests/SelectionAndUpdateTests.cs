using System.Text;
using Setupwise.Core.Selection;
using Setupwise.Core.Updates;

namespace Setupwise.Core.Tests;

public class SelectionAndUpdateTests
{
    [Fact]
    public async Task Selection_round_trips_without_duplicates()
    {
        using var stream = new MemoryStream();
        await SelectionFile.SaveAsync(stream, ["Git.Git", "7zip.7zip", "git.git"], DateTimeOffset.UnixEpoch, TestContext.Current.CancellationToken);
        stream.Position = 0;

        var ids = await SelectionFile.LoadAsync(stream, TestContext.Current.CancellationToken);

        Assert.Equal(["7zip.7zip", "Git.Git"], ids);
    }

    [Theory]
    [InlineData("""{ "hello": "world" }""")]
    [InlineData("not json at all")]
    [InlineData("""{ "format": "setupwise-selection", "version": 99, "packages": [] }""")]
    public async Task Foreign_or_newer_files_are_rejected(string content)
    {
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(content));
        await Assert.ThrowsAsync<InvalidDataException>(() => SelectionFile.LoadAsync(stream, TestContext.Current.CancellationToken));
    }

    [Theory]
    [InlineData("v1.2.3", "1.2.3")]
    [InlineData("1.2", "1.2")]
    [InlineData("v2.0.0-beta.1", "2.0.0")]
    public void Parses_release_tags(string tag, string expected)
    {
        Assert.True(AppUpdateChecker.TryParseVersion(tag, out var version));
        Assert.Equal(Version.Parse(expected), version);
    }

    [Theory]
    [InlineData("")]
    [InlineData("latest")]
    public void Rejects_invalid_tags(string tag) => Assert.False(AppUpdateChecker.TryParseVersion(tag, out _));
}
