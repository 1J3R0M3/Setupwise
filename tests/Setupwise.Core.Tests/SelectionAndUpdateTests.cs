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
    [InlineData("1.2", "1.2.0")]
    [InlineData("v2.0.0-beta.1", "2.0.0-beta.1")]
    [InlineData("0.3.0-beta.1+4f2a9c1", "0.3.0-beta.1")]
    public void Parses_release_tags(string tag, string expected)
    {
        Assert.True(AppVersion.TryParse(tag, out var version));
        Assert.Equal(expected, version.ToString());
    }

    [Theory]
    [InlineData("")]
    [InlineData("latest")]
    [InlineData("1.2.3-")]
    [InlineData("1.2.3-beta..1")]
    public void Rejects_invalid_tags(string tag) => Assert.False(AppVersion.TryParse(tag, out _));

    [Fact]
    public void Pre_releases_sort_before_the_release()
    {
        string[] ordered = ["0.2.0", "0.3.0-alpha.1", "0.3.0-beta.1", "0.3.0-beta.2", "0.3.0-beta.10", "0.3.0-rc.1", "0.3.0", "0.3.1"];
        var parsed = ordered.Select(AppVersion.Parse).ToList();

        for (var i = 1; i < parsed.Count; i++)
            Assert.True(parsed[i - 1] < parsed[i], $"{parsed[i - 1]} < {parsed[i]}");
        Assert.Equal(0, AppVersion.Parse("v1.0").CompareTo(AppVersion.Parse("1.0.0")));
    }

    [Fact]
    public async Task Update_check_ignores_pre_releases_unless_asked()
    {
        const string list = """
            [
              { "tag_name": "v0.4.0-beta.1", "html_url": "https://example.org/beta", "draft": false, "prerelease": true },
              { "tag_name": "v0.5.0", "html_url": "https://example.org/draft", "draft": true, "prerelease": false },
              { "tag_name": "v0.3.0", "html_url": "https://example.org/stable", "draft": false, "prerelease": false }
            ]
            """;
        const string latest = """{ "tag_name": "v0.3.0", "html_url": "https://example.org/stable", "draft": false, "prerelease": false }""";
        using var http = new HttpClient(new FakeGitHub(list, latest));
        var checker = new AppUpdateChecker(http, "owner/repo");
        var token = TestContext.Current.CancellationToken;

        Assert.Equal("0.3.0", (await checker.FindNewerAsync(AppVersion.Parse("0.2.0"), false, token))?.Version.ToString());
        Assert.Equal("0.4.0-beta.1", (await checker.FindNewerAsync(AppVersion.Parse("0.2.0"), true, token))?.Version.ToString());
        Assert.Null(await checker.FindNewerAsync(AppVersion.Parse("0.4.0-beta.1"), true, token));
        // Someone on a beta gets the final release once it is out.
        Assert.NotNull(await checker.FindNewerAsync(AppVersion.Parse("0.3.0-beta.2"), false, token));
    }

    private sealed class FakeGitHub(string list, string latest) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var body = request.RequestUri!.AbsolutePath.EndsWith("/latest", StringComparison.Ordinal) ? latest : list;
            return Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.OK)
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json"),
            });
        }
    }
}
