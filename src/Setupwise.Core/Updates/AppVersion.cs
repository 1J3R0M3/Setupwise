using System.Globalization;

namespace Setupwise.Core.Updates;

/// <summary>
/// A version like "0.3.0" or "0.3.0-beta.1" with Semantic Versioning order:
/// 0.3.0-alpha.1 &lt; 0.3.0-beta.1 &lt; 0.3.0-beta.2 &lt; 0.3.0-rc.1 &lt; 0.3.0.
/// </summary>
public sealed record AppVersion(Version Core, string? PreRelease) : IComparable<AppVersion>
{
    public bool IsPreRelease => !string.IsNullOrEmpty(PreRelease);

    /// <summary>Parses "v1.2.3", "1.2", "1.2.3-beta.1" and "1.2.3-beta.1+abc123" (build metadata is ignored).</summary>
    public static bool TryParse(string? text, out AppVersion version)
    {
        version = new AppVersion(new Version(0, 0, 0), null);
        if (string.IsNullOrWhiteSpace(text)) return false;

        var value = text.Trim().TrimStart('v', 'V');
        var plus = value.IndexOf('+', StringComparison.Ordinal);
        if (plus >= 0) value = value[..plus];
        var dash = value.IndexOf('-', StringComparison.Ordinal);
        var pre = dash >= 0 ? value[(dash + 1)..] : null;
        if (dash >= 0) value = value[..dash];

        if (!Version.TryParse(value, out var core)) return false;
        if (pre is not null && (pre.Length == 0 || pre.Split('.').Any(p => p.Length == 0))) return false;

        version = new AppVersion(new Version(core.Major, core.Minor, Math.Max(core.Build, 0)), pre);
        return true;
    }

    public static AppVersion Parse(string text) =>
        TryParse(text, out var version) ? version : throw new FormatException($"'{text}' is not a valid version.");

    public int CompareTo(AppVersion? other)
    {
        if (other is null) return 1;
        var core = Core.CompareTo(other.Core);
        if (core != 0) return core;

        // A release is newer than any of its pre-releases.
        if (!IsPreRelease) return other.IsPreRelease ? 1 : 0;
        if (!other.IsPreRelease) return -1;

        var a = PreRelease!.Split('.');
        var b = other.PreRelease!.Split('.');
        for (var i = 0; i < Math.Min(a.Length, b.Length); i++)
        {
            var aIsNumber = int.TryParse(a[i], NumberStyles.None, CultureInfo.InvariantCulture, out var aNumber);
            var bIsNumber = int.TryParse(b[i], NumberStyles.None, CultureInfo.InvariantCulture, out var bNumber);
            var result = (aIsNumber, bIsNumber) switch
            {
                (true, true) => aNumber.CompareTo(bNumber),
                (true, false) => -1, // numbers sort before words
                (false, true) => 1,
                _ => string.CompareOrdinal(a[i], b[i]),
            };
            if (result != 0) return Math.Sign(result);
        }
        return a.Length.CompareTo(b.Length);
    }

    public static bool operator >(AppVersion left, AppVersion right) => left is not null && left.CompareTo(right) > 0;
    public static bool operator <(AppVersion left, AppVersion right) => right is not null && right.CompareTo(left) > 0;
    public static bool operator >=(AppVersion left, AppVersion right) => !(left < right);
    public static bool operator <=(AppVersion left, AppVersion right) => !(left > right);

    public override string ToString() => IsPreRelease ? $"{Core.ToString(3)}-{PreRelease}" : Core.ToString(3);
}
