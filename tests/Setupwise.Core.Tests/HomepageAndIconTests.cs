using Setupwise.Core.Icons;
using Setupwise.Core.Winget;

namespace Setupwise.Core.Tests;

public class HomepageAndIconTests
{
    [Fact]
    public void Homepage_is_preferred_over_publisher_and_installer_urls()
    {
        string[] show =
        [
            "Gefunden Visual Studio Code [Microsoft.VisualStudioCode]",
            "Herausgeber-URL: https://www.microsoft.com/",
            "Startseite: https://code.visualstudio.com/",
            "Lizenz-URL: https://code.visualstudio.com/License/",
            "Installer:",
            "  Installer-URL: https://update.code.visualstudio.com/x.exe",
        ];

        Assert.Equal(new Uri("https://code.visualstudio.com/"), WingetShowParser.GetHomepage(show));
    }

    [Fact]
    public void Publisher_url_is_used_when_there_is_no_homepage()
    {
        string[] show = ["Publisher Url: https://www.videolan.org/", "License Url: https://example.com/license"];
        Assert.Equal(new Uri("https://www.videolan.org/"), WingetShowParser.GetHomepage(show));
    }

    [Fact]
    public void Unknown_language_falls_back_to_first_non_ignored_url()
    {
        string[] show = ["Licentie-URL: https://example.com/license", "Startpagina: https://example.org/"];
        Assert.Equal(new Uri("https://example.org/"), WingetShowParser.GetHomepage(show));
    }

    [Fact]
    public void GitHub_projects_use_the_owner_avatar()
    {
        var icon = IconCandidateFinder.DirectIcon(new Uri("https://github.com/keepassxreboot/keepassxc"));
        Assert.Equal(new Uri("https://github.com/keepassxreboot.png?size=128"), icon);
        Assert.Null(IconCandidateFinder.DirectIcon(new Uri("https://obsidian.md/")));
    }

    [Fact]
    public void Candidates_are_ordered_by_quality_and_resolved()
    {
        const string html = """
            <head>
              <link rel="icon" href="/favicon-16.png" sizes="16x16">
              <link rel='icon' type='image/svg+xml' href='/logo.svg'>
              <link href="/img/touch.png" rel="apple-touch-icon">
              <link rel="icon" sizes="192x192" href="https://cdn.example.com/icon-192.png?v=2&amp;x=1">
              <link rel="stylesheet" href="/site.css">
              <link rel="icon" href="data:image/png;base64,AAAA">
            </head>
            """;

        var candidates = IconCandidateFinder.FromHtml(new Uri("https://www.example.com/app/"), html)
            .Select(u => u.AbsoluteUri).ToList();

        Assert.Equal(
        [
            "https://cdn.example.com/icon-192.png?v=2&x=1",
            "https://www.example.com/img/touch.png",
            "https://www.example.com/apple-touch-icon.png",
            "https://www.example.com/favicon-16.png",
            "https://www.example.com/favicon.ico",
        ], candidates);
    }

    [Theory]
    [InlineData(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }, "png")]
    [InlineData(new byte[] { 0x00, 0x00, 0x01, 0x00, 0x01, 0x00, 0x10, 0x10 }, "ico")]
    [InlineData(new byte[] { 0xFF, 0xD8, 0xFF, 0xE0, 0, 0, 0, 0 }, "jpg")]
    [InlineData(new byte[] { 0x3C, 0x73, 0x76, 0x67, 0x20, 0x78, 0x6D, 0x6C }, null)] // "<svg xml"
    public void Detects_image_formats(byte[] bytes, string? expected) =>
        Assert.Equal(expected, ImageFormat.Detect(bytes));

    [Fact]
    public void Cache_remembers_misses_for_a_while()
    {
        var dir = Directory.CreateTempSubdirectory("setupwise-icons-").FullName;
        try
        {
            using var http = new HttpClient();
            var service = new IconService(http, dir);

            Assert.False(service.IsKnownMissing("Vendor.App"));
            service.MarkMissing("Vendor.App");
            Assert.True(service.IsKnownMissing("Vendor.App"));
            Assert.Null(service.TryGetCached("Vendor.App"));

            service.Forget("Vendor.App");
            Assert.False(service.IsKnownMissing("Vendor.App"));
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }
}
