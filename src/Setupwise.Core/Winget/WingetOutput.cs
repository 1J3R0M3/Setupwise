using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace Setupwise.Core.Winget;

/// <summary>Helpers for the human-oriented text output of winget.exe.</summary>
public static partial class WingetOutput
{
    /// <summary>Removes backspaces used by the spinner and trailing whitespace.</summary>
    public static string Clean(string line)
    {
        ArgumentNullException.ThrowIfNull(line);
        return line.Replace("\b", string.Empty, StringComparison.Ordinal).TrimEnd();
    }

    /// <summary>True for spinner frames, progress bars and download counters.</summary>
    public static bool IsNoise(string line)
    {
        ArgumentNullException.ThrowIfNull(line);
        var t = line.Trim();
        if (t.Length <= 1) return true;
        if (BlockCharacters().IsMatch(t)) return true;
        if (DownloadCounterOnly().IsMatch(t)) return true;
        if (PercentOnly().IsMatch(t)) return true;
        return false;
    }

    /// <summary>Reads a progress value (0..1) from a progress line, if it contains one.</summary>
    public static double? TryParseProgress(string line)
    {
        ArgumentNullException.ThrowIfNull(line);
        var bytes = DownloadCounter().Match(line);
        if (bytes.Success)
        {
            var done = ToBytes(bytes.Groups[1].Value, bytes.Groups[2].Value);
            var total = ToBytes(bytes.Groups[3].Value, bytes.Groups[4].Value);
            if (total > 0) return Math.Clamp(done / total, 0, 1);
        }

        var percent = Percent().Match(line);
        if (percent.Success && int.TryParse(percent.Groups[1].Value, NumberStyles.None, CultureInfo.InvariantCulture, out var p))
            return Math.Clamp(p / 100.0, 0, 1);

        return null;
    }

    private static double ToBytes(string number, string unit)
    {
        var value = double.Parse(number.Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture);
        return unit.ToUpperInvariant() switch
        {
            "KB" => value * 1024,
            "MB" => value * 1024 * 1024,
            "GB" => value * 1024 * 1024 * 1024,
            _ => value,
        };
    }

    /// <summary>Number of terminal cells a character needs (East Asian wide characters need two).</summary>
    public static int CellWidth(Rune rune)
    {
        var v = rune.Value;
        var wide = v is >= 0x1100 and <= 0x115F
            or >= 0x2E80 and <= 0xA4CF
            or >= 0xAC00 and <= 0xD7A3
            or >= 0xF900 and <= 0xFAFF
            or >= 0xFE30 and <= 0xFE4F
            or >= 0xFF00 and <= 0xFF60
            or >= 0xFFE0 and <= 0xFFE6
            or >= 0x1F300 and <= 0x1FAFF
            or >= 0x20000 and <= 0x3FFFD;
        return wide ? 2 : 1;
    }

    /// <summary>Substring measured in terminal cells instead of characters.</summary>
    public static string SliceCells(string line, int startCell, int endCell)
    {
        ArgumentNullException.ThrowIfNull(line);
        var sb = new StringBuilder();
        var cell = 0;
        foreach (var rune in line.EnumerateRunes())
        {
            if (cell >= endCell) break;
            if (cell >= startCell) sb.Append(rune.ToString());
            cell += CellWidth(rune);
        }
        return sb.ToString();
    }

    public static int CellLength(string line)
    {
        ArgumentNullException.ThrowIfNull(line);
        var cells = 0;
        foreach (var rune in line.EnumerateRunes()) cells += CellWidth(rune);
        return cells;
    }

    [GeneratedRegex(@"[▀-▟]")]
    private static partial Regex BlockCharacters();

    [GeneratedRegex(@"^[\d.,]+\s*[KMG]?B\s*/\s*[\d.,]+\s*[KMG]?B$")]
    private static partial Regex DownloadCounterOnly();

    [GeneratedRegex(@"^\d{1,3}\s?%$")]
    private static partial Regex PercentOnly();

    [GeneratedRegex(@"(\d+(?:[.,]\d+)?)\s*(B|KB|MB|GB)\s*/\s*(\d+(?:[.,]\d+)?)\s*(B|KB|MB|GB)", RegexOptions.IgnoreCase)]
    private static partial Regex DownloadCounter();

    [GeneratedRegex(@"(?<![\d.,])(\d{1,3})\s?%")]
    private static partial Regex Percent();
}
