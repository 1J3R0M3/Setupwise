using Setupwise.Core.Packages;
using Setupwise.Core.Winget;

namespace Setupwise.Core.Tests;

public class ConsoleAndSkipTests
{
    [Theory]
    [InlineData("show --id Git.Git", new[] { "show", "--id", "Git.Git" })]
    [InlineData("winget show --id Git.Git", new[] { "show", "--id", "Git.Git" })]
    [InlineData("WINGET.EXE  source   update ", new[] { "source", "update" })]
    [InlineData("search \"Visual Studio Code\"", new[] { "search", "Visual Studio Code" })]
    [InlineData("search --name \"\"", new[] { "search", "--name", "" })]
    [InlineData("list a&b | more", new[] { "list", "a&b", "|", "more" })]
    [InlineData("   ", new string[0])]
    [InlineData("winget", new string[0])]
    public void Console_line_is_split_like_a_command_line(string line, string[] expected) =>
        Assert.Equal(expected, ConsoleCommand.Parse(line));

    [Fact]
    public void Only_the_skipped_version_is_hidden()
    {
        var skipped = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { ["Git.Git"] = "2.51.0" };
        InstalledPackage[] upgrades =
        [
            new("git.git", "Git", "2.50.0", "2.51.0"),
            new("VideoLAN.VLC", "VLC", "3.0.20", "3.0.21"),
        ];

        Assert.Equal(["VideoLAN.VLC"], SkippedUpdates.Remove(upgrades, skipped).Select(u => u.Id));
        Assert.False(SkippedUpdates.IsSkipped(new InstalledPackage("Git.Git", "Git", "2.50.0", "2.52.0"), skipped));
    }
}
