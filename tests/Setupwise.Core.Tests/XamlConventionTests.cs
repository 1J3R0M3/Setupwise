using System.Text.RegularExpressions;

namespace Setupwise.Core.Tests;

/// <summary>
/// Catches XAML mistakes the compiler does not report, e.g. a view without code-behind:
/// it compiles, but InitializeComponent() is never called and the page stays empty.
/// </summary>
public partial class XamlConventionTests
{
    private static readonly string AppDirectory = Path.Combine(RepositoryRoot(), "src", "Setupwise.App");

    [Fact]
    public void Every_view_calls_InitializeComponent()
    {
        var problems = new List<string>();
        foreach (var xaml in Directory.EnumerateFiles(AppDirectory, "*.xaml", SearchOption.AllDirectories))
        {
            var cls = XClass().Match(File.ReadAllText(xaml));
            if (!cls.Success) continue; // resource dictionaries

            var codeBehind = xaml + ".cs";
            if (!File.Exists(codeBehind))
                problems.Add($"{Path.GetFileName(xaml)}: code-behind file is missing");
            else if (!Path.GetFileName(xaml).Equals("App.xaml", StringComparison.Ordinal)
                     && !File.ReadAllText(codeBehind).Contains("InitializeComponent()", StringComparison.Ordinal))
                problems.Add($"{Path.GetFileName(xaml)}: constructor does not call InitializeComponent()");
        }
        Assert.Empty(problems);
    }

    private static string RepositoryRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "Setupwise.slnx"))) dir = dir.Parent;
        return dir?.FullName ?? throw new InvalidOperationException("Repository root (Setupwise.slnx) not found.");
    }

    [GeneratedRegex(@"x:Class=""([\w.]+)""")]
    private static partial Regex XClass();
}
