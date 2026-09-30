using Setupwise.Core.Winget;

namespace Setupwise.Core.Tests;

public class WingetTableParserTests
{
    // Real-world layout of "winget upgrade" (German Windows), including footers and a second table.
    private static readonly string[] UpgradeOutput =
    [
        "Name                                   ID                           Version        Verfügbar      Quelle",
        "--------------------------------------------------------------------------------------------------------",
        "Microsoft Visual C++ 2015-2022 Redis…  Microsoft.VCRedist.2015+.x64 14.38.33130.0  14.40.33810.0  winget",
        "Mozilla Firefox (x64 de)               Mozilla.Firefox.de           130.0          131.0.2        winget",
        "2 Aktualisierungen verfügbar.",
        "1 Paket(e) haben Versionsnummern, die nicht ermittelt werden können. Verwenden Sie --include-unknown, um alle Ergebnisse anzuzeigen.",
        "Die folgenden Pakete haben ein Upgrade verfügbar, erfordern jedoch explizites Targeting für das Upgrade:",
        "Name     ID              Version Verfügbar Quelle",
        "--------------------------------------------------",
        "Discord  Discord.Discord 1.0     1.1       winget",
    ];

    [Fact]
    public void Parses_rows_of_the_first_table_only()
    {
        var rows = WingetTableParser.Parse(UpgradeOutput, 4);

        Assert.Equal(2, rows.Count);
        Assert.Equal(["Microsoft Visual C++ 2015-2022 Redis…", "Microsoft.VCRedist.2015+.x64", "14.38.33130.0", "14.40.33810.0", "winget"], rows[0]);
        Assert.Equal("Mozilla.Firefox.de", rows[1][1]);
        Assert.Equal("131.0.2", rows[1][3]);
    }

    [Fact]
    public void Handles_truncated_names_separated_by_a_single_space()
    {
        string[] lines =
        [
            "Name                 Id                Version",
            "----------------------------------------------",
            "Some Very Long Name… Vendor.LongName   1.0",
        ];

        var row = Assert.Single(WingetTableParser.Parse(lines, 3));
        Assert.Equal("Some Very Long Name…", row[0]);
        Assert.Equal("Vendor.LongName", row[1]);
    }

    [Fact]
    public void Measures_columns_in_terminal_cells_for_wide_characters()
    {
        // "微信" is 2 characters but 4 terminal cells wide.
        string[] lines =
        [
            "Name       Id                Version",
            "------------------------------------",
            "微信       Tencent.WeChat    3.9.12",
        ];

        var row = Assert.Single(WingetTableParser.Parse(lines, 3));
        Assert.Equal("微信", row[0]);
        Assert.Equal("Tencent.WeChat", row[1]);
        Assert.Equal("3.9.12", row[2]);
    }

    [Fact]
    public void Rejects_truncated_ids()
    {
        string[] lines =
        [
            "Name  Id                Version",
            "-------------------------------",
            "Foo   Vendor.VeryLongI… 1.0",
        ];

        Assert.Empty(WingetTableParser.Parse(lines, 3));
    }

    [Fact]
    public void Returns_nothing_without_a_table()
    {
        Assert.Empty(WingetTableParser.Parse(["No package found matching input criteria."], 3));
    }
}
