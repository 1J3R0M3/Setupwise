namespace Setupwise.Core.Icons;

/// <summary>Detects image formats WPF can display without extra codecs.</summary>
public static class ImageFormat
{
    /// <returns>File extension without dot, or null if unsupported.</returns>
    public static string? Detect(ReadOnlySpan<byte> b)
    {
        if (b.Length < 8) return null;
        if (b[0] == 0x89 && b[1] == 0x50 && b[2] == 0x4E && b[3] == 0x47) return "png";
        if (b[0] == 0x00 && b[1] == 0x00 && b[2] == 0x01 && b[3] == 0x00) return "ico";
        if (b[0] == 0xFF && b[1] == 0xD8) return "jpg";
        if (b[0] == 0x47 && b[1] == 0x49 && b[2] == 0x46) return "gif";
        if (b[0] == 0x42 && b[1] == 0x4D) return "bmp";
        return null;
    }

    public static IReadOnlyList<string> Extensions { get; } = ["png", "ico", "jpg", "gif", "bmp"];
}
