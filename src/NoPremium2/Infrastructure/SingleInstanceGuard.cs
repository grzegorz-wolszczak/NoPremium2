using System.Diagnostics;

namespace NoPremium2.Infrastructure;

/// <summary>
/// Prevents multiple instances of the application from running simultaneously.
/// Uses a PID file in the system temp directory.
/// </summary>
public sealed class SingleInstanceGuard : IDisposable
{
    private static readonly string PidFilePath =
        Path.Combine(Path.GetTempPath(), "nopremium2.pid");

    private bool _acquired;

    /// <summary>
    /// Tries to acquire the single-instance lock.
    /// Returns true if acquired (no other instance running).
    /// Returns false if another instance is detected; sets out parameters with its info.
    /// </summary>
    public bool TryAcquire(out int? existingPid, out DateTime? existingStartTime)
    {
        existingPid = null;
        existingStartTime = null;

        if (File.Exists(PidFilePath))
        {
            var content = string.Empty;
            try { content = File.ReadAllText(PidFilePath).Trim(); } catch { }

            if (int.TryParse(content, out int pid))
            {
                try
                {
                    var proc = Process.GetProcessById(pid);
                    // Process exists — another instance is running
                    existingPid = pid;
                    try { existingStartTime = proc.StartTime; } catch { }
                    return false;
                }
                catch (ArgumentException)
                {
                    // Process with that PID no longer exists — stale lock file
                }
            }
        }

        try
        {
            File.WriteAllText(PidFilePath, Environment.ProcessId.ToString());
        }
        catch (Exception ex)
        {
            // Non-fatal: can't write PID file, continue anyway
            Console.Error.WriteLine($"[WARN] Could not write PID file '{PidFilePath}': {ex.Message}");
        }

        _acquired = true;
        return true;
    }

    public void Dispose()
    {
        if (!_acquired) return;
        try { File.Delete(PidFilePath); } catch { }
    }
}
