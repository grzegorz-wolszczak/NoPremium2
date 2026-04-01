namespace NoPremium2.Infrastructure;

public static class DataSizeConverter
{
    private static readonly (string Suffix, long Multiplier)[] Units = new[]
    {
        ("TB", 1024L * 1024 * 1024 * 1024),
        ("GB", 1024L * 1024 * 1024),
        ("MB", 1024L * 1024),
        ("KB", 1024L),
        ("B",  1L),
    };

    /// <summary>Parses a human-readable size string like "512MB" or "3.5GB" to bytes.</summary>
    public static long ParseToBytes(string sizeStr)
    {
        if (string.IsNullOrWhiteSpace(sizeStr))
            throw new FormatException("Size string is empty.");

        foreach (var (suffix, multiplier) in Units)
        {
            if (sizeStr.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
            {
                var numStr = sizeStr[..^suffix.Length].Trim();
                if (double.TryParse(numStr, System.Globalization.NumberStyles.Any,
                        System.Globalization.CultureInfo.InvariantCulture, out double value))
                    return (long)(value * multiplier);
            }
        }

        // Fallback: try parsing as plain bytes
        if (long.TryParse(sizeStr.Trim(), out long bytes))
            return bytes;

        throw new FormatException($"Cannot parse size string: '{sizeStr}'");
    }

    /// <summary>Formats bytes as a human-readable string.</summary>
    public static string FormatBytes(long bytes)
    {
        if (bytes >= 1024L * 1024 * 1024)
            return $"{bytes / (1024.0 * 1024 * 1024):F2} GB";
        if (bytes >= 1024L * 1024)
            return $"{bytes / (1024.0 * 1024):F2} MB";
        if (bytes >= 1024L)
            return $"{bytes / 1024.0:F2} KB";
        return $"{bytes} B";
    }
}
