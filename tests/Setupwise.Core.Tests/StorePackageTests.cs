using System.Text.Json;
using System.Xml.Linq;
using Setupwise.Core.Distribution;
using Setupwise.Core.Selection;

namespace Setupwise.Core.Tests;

/// <summary>Store build flavour and the MSIX package manifest in installer/msix (R-13).</summary>
public class StorePackageTests
{
    private static readonly string MsixDirectory = Path.Combine(RepositoryRoot(), "installer", "msix");
    private static readonly XNamespace Foundation = "http://schemas.microsoft.com/appx/manifest/foundation/windows10";
    private static readonly XNamespace Uap = "http://schemas.microsoft.com/appx/manifest/uap/windows10";
    private static readonly XNamespace Uap10 = "http://schemas.microsoft.com/appx/manifest/uap/windows10/10";
    private static readonly XNamespace Rescap = "http://schemas.microsoft.com/appx/manifest/foundation/windows10/restrictedcapabilities";

    [Theory]
    [InlineData("Store", BuildFlavor.Store)]
    [InlineData(" store ", BuildFlavor.Store)]
    [InlineData("Standard", BuildFlavor.Standard)]
    [InlineData("", BuildFlavor.Standard)]
    [InlineData(null, BuildFlavor.Standard)]
    [InlineData("1", BuildFlavor.Standard)]
    [InlineData("MicrosoftStore", BuildFlavor.Standard)]
    public void Flavour_is_read_from_the_build_property(string? value, BuildFlavor expected) =>
        Assert.Equal(expected, BuildFlavors.Parse(value));

    [Fact]
    public void Store_build_leaves_updates_to_the_Store_and_does_not_elevate()
    {
        var store = AppFeatures.For(BuildFlavor.Store);
        Assert.False(store.CheckForAppUpdates);
        Assert.False(store.AutoUpdate);
        Assert.False(store.SelfElevation);
        Assert.False(store.Tweaks);

        var standard = AppFeatures.For(BuildFlavor.Standard);
        Assert.True(standard.CheckForAppUpdates);
        Assert.True(standard.AutoUpdate);
        Assert.True(standard.SelfElevation);
        Assert.True(standard.Tweaks);
    }

    [Fact]
    public void Paths_below_the_real_folder_are_translated_to_the_redirection_target()
    {
        var real = Path.Combine(Path.GetTempPath(), "Real", "AppData", "Local");
        var redirected = Path.Combine(Path.GetTempPath(), "Real", "AppData", "Local", "Packages", "Setupwise_abc", "LocalCache", "Local");

        Assert.Equal(Path.Combine(redirected, "Setupwise", "Logs"),
            PackagedPaths.Redirect(Path.Combine(real, "Setupwise", "Logs"), real + Path.DirectorySeparatorChar, redirected));
        Assert.Equal(redirected, PackagedPaths.Redirect(real, real, redirected));
        Assert.Null(PackagedPaths.Redirect(Path.Combine(Path.GetTempPath(), "Elsewhere", "Logs"), real, redirected));
        Assert.Null(PackagedPaths.Redirect(real + "Low" + Path.DirectorySeparatorChar + "Logs", real, redirected)); // "LocalLow" is not below "Local"
    }

    [Fact]
    public void Shell_gets_the_redirected_folder_only_if_it_exists()
    {
        var real = Path.Combine(Path.GetTempPath(), "Real");
        var redirected = Path.Combine(Path.GetTempPath(), "Redirected");
        var logs = Path.Combine(real, "Setupwise", "Logs");

        Assert.Equal(Path.Combine(redirected, "Setupwise", "Logs"), PackagedPaths.ForShell(logs, real, redirected, _ => true));
        Assert.Equal(logs, PackagedPaths.ForShell(logs, real, redirected, _ => false));
        Assert.Equal(logs, PackagedPaths.ForShell(logs, real, null, _ => true));
    }

    [Fact]
    public void Manifest_runs_Setupwise_as_full_trust_app_without_elevation()
    {
        var manifest = LoadManifest();
        var application = Assert.Single(manifest.Descendants(Foundation + "Application"));

        Assert.Equal(AppAssemblyName() + ".exe", (string?)application.Attribute("Executable"));
        Assert.Equal("packagedClassicApp", (string?)application.Attribute(Uap10 + "RuntimeBehavior"));
        Assert.Equal("mediumIL", (string?)application.Attribute(Uap10 + "TrustLevel"));

        var capabilities = manifest.Descendants(Rescap + "Capability").Select(c => (string?)c.Attribute("Name")).ToList();
        Assert.Contains("runFullTrust", capabilities);
        Assert.DoesNotContain("allowElevation", capabilities); // restricted, see docs/store.md
    }

    [Fact]
    public void Manifest_opens_selection_files()
    {
        var manifest = LoadManifest();
        var fileType = Assert.Single(manifest.Descendants(Uap + "FileType"));
        Assert.Equal(SelectionFile.Extension, fileType.Value);

        // Same name as the ProgID the installer registers would be nice, but the manifest only allows lower case.
        var association = fileType.Parent!.Parent!;
        Assert.Equal("setupwise", (string?)association.Attribute("Name"));
        Assert.Equal("\"%1\"", (string?)association.Attribute("Parameters"));
    }

    [Fact]
    public void Manifest_identity_is_filled_in_by_the_build_script()
    {
        var manifest = LoadManifest();
        var identity = Assert.Single(manifest.Descendants(Foundation + "Identity"));
        Assert.Equal("$IdentityName$", (string?)identity.Attribute("Name"));
        Assert.Equal("$Publisher$", (string?)identity.Attribute("Publisher"));
        Assert.Equal("$Version$", (string?)identity.Attribute("Version"));
        Assert.Equal("x64", (string?)identity.Attribute("ProcessorArchitecture"));

        using var json = JsonDocument.Parse(File.ReadAllText(Path.Combine(MsixDirectory, "identity.json")));
        foreach (var key in new[] { "name", "publisher", "publisherDisplayName" })
            Assert.False(string.IsNullOrWhiteSpace(json.RootElement.GetProperty(key).GetString()), key);
        Assert.StartsWith("CN=", json.RootElement.GetProperty("publisher").GetString(), StringComparison.Ordinal);
    }

    [Fact]
    public void Every_logo_in_the_manifest_exists()
    {
        var assets = Directory.EnumerateFiles(Path.Combine(MsixDirectory, "Assets")).Select(Path.GetFileName).ToList();
        var logos = LoadManifest().Descendants()
            .SelectMany(e => e.Attributes().Where(a => a.Name.LocalName.EndsWith("Logo", StringComparison.Ordinal)).Select(a => a.Value)
                .Concat(e.Name.LocalName == "Logo" ? [e.Value] : []))
            .Distinct()
            .ToList();

        Assert.NotEmpty(logos);
        foreach (var logo in logos)
        {
            // "Assets\Square44x44Logo.png" is resolved through resources.pri to e.g. "Square44x44Logo.scale-200.png".
            Assert.StartsWith(@"Assets\", logo, StringComparison.Ordinal);
            var name = Path.GetFileNameWithoutExtension(logo[@"Assets\".Length..]);
            Assert.True(assets.Any(a => a!.StartsWith(name + ".scale-100", StringComparison.Ordinal)), $"{logo}: scale-100 file is missing");
            Assert.True(assets.Any(a => a!.StartsWith(name + ".scale-200", StringComparison.Ordinal)), $"{logo}: scale-200 file is missing");
        }
    }

    private static XDocument LoadManifest() => XDocument.Load(Path.Combine(MsixDirectory, "AppxManifest.xml"));

    private static string AppAssemblyName()
    {
        var project = XDocument.Load(Path.Combine(RepositoryRoot(), "src", "Setupwise.App", "Setupwise.App.csproj"));
        return project.Descendants("AssemblyName").Single().Value;
    }

    private static string RepositoryRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "Setupwise.slnx"))) dir = dir.Parent;
        return dir?.FullName ?? throw new InvalidOperationException("Repository root (Setupwise.slnx) not found.");
    }
}
