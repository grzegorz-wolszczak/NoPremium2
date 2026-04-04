namespace NoPremium2.Infrastructure;

public static class LogFileHelper
{
    /// <summary>
    /// Returns the path for this run's log file, using the next available run number for today.
    /// Files follow the pattern: logs_YYYYMMDD.NN.log (e.g. logs_20260401.01.log)
    /// </summary>
    public static string ResolveLogFilePath(string logDir, DateTime now)
    {
        string today = now.ToString("yyyyMMdd");
        var existing = Directory.GetFiles(logDir, $"logs_{today}.??.log");
        int maxNumber = 0;
        foreach (var f in existing)
        {
            var baseName = Path.GetFileNameWithoutExtension(f); // e.g. "logs_20260401.03"
            var dotIdx = baseName.LastIndexOf('.');
            if (dotIdx >= 0 && int.TryParse(baseName[(dotIdx + 1)..], out int n))
                maxNumber = Math.Max(maxNumber, n);
        }
        return Path.Combine(logDir, $"logs_{today}.{maxNumber + 1:D2}.log");
    }

    /// <summary>
    /// Deletes log files matching logs_YYYYMMDD.NN.log that are strictly older
    /// than <paramref name="retentionDays"/> days relative to <paramref name="now"/>.
    /// </summary>
    public static void DeleteOldLogs(string logDir, DateTime now, int retentionDays = 30)
    {
        var cutoff = now.AddDays(-retentionDays);
        foreach (var f in Directory.GetFiles(logDir, "logs_????????.??.log"))
        {
            var datePart = Path.GetFileName(f).Substring(5, 8); // skip "logs_"
            if (DateTime.TryParseExact(datePart, "yyyyMMdd", null,
                    System.Globalization.DateTimeStyles.None, out var fileDate)
                && fileDate < cutoff)
            {
                try { File.Delete(f); } catch { /* best-effort */ }
            }
        }
    }
}
