using System.Text;
using System.Text.RegularExpressions;

namespace Setupwise.Core.Winget;

/// <summary>
/// Parses the tables printed by "winget search/list/upgrade".
/// Columns are located by the positions of the header words, because values can be
/// truncated ("Microsoft Visual C++ 2015…") and then only a single space separates them.
/// Positions are measured in terminal cells, like winget itself does.
/// </summary>
public static partial class WingetTableParser
{
    /// <param name="lines">Output lines, already cleaned and without noise.</param>
    /// <param name="minColumns">Rows with fewer non-empty leading columns are skipped.</param>
    public static IReadOnlyList<string[]> Parse(IReadOnlyList<string> lines, int minColumns)
    {
        ArgumentNullException.ThrowIfNull(lines);
        var rows = new List<string[]>();

        var separator = -1;
        for (var i = 1; i < lines.Count; i++)
        {
            if (Separator().IsMatch(lines[i])) { separator = i; break; }
        }
        if (separator < 1) return rows;

        var starts = ColumnStarts(lines[separator - 1]);
        if (starts.Count < minColumns) return rows;

        for (var i = separator + 1; i < lines.Count; i++)
        {
            // A second table starts: this line is its header.
            if (i + 1 < lines.Count && Separator().IsMatch(lines[i + 1])) break;

            var line = lines[i];
            var cells = WingetOutput.CellLength(line);
            var columns = new string[starts.Count];
            for (var c = 0; c < starts.Count; c++)
            {
                var end = c + 1 < starts.Count ? starts[c + 1] : cells;
                columns[c] = starts[c] >= cells ? string.Empty : WingetOutput.SliceCells(line, starts[c], end).Trim();
            }

            if (PackageId().IsMatch(columns[1]) && columns.Take(minColumns).All(v => v.Length > 0))
                rows.Add(columns);
        }

        return rows;
    }

    private static List<int> ColumnStarts(string header)
    {
        var starts = new List<int>();
        var cell = 0;
        var previousWasSpace = true;
        foreach (var rune in header.EnumerateRunes())
        {
            var isSpace = Rune.IsWhiteSpace(rune);
            if (!isSpace && previousWasSpace) starts.Add(cell);
            previousWasSpace = isSpace;
            cell += WingetOutput.CellWidth(rune);
        }
        return starts;
    }

    [GeneratedRegex(@"^-{10,}$")]
    private static partial Regex Separator();

    // Truncated ids end with "…" and are rejected on purpose.
    [GeneratedRegex(@"^\w[\w.+-]*$")]
    private static partial Regex PackageId();
}
