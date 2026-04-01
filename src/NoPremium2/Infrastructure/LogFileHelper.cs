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
        int runNumber = Directory.GetFiles(logDir, $"logs_{today}.??.log").Length + 1;
        return Path.Combine(logDir, $"logs_{today}.{runNumber:D2}.log");
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
