using System.Diagnostics;
using Microsoft.Extensions.Logging;

namespace NoPremium2.Browser;

public interface IStaleBrowserKiller
{
    /// <summary>
    /// Kills the browser process <paramref name="pid"/> (and its tree) and removes the
    /// leftover Chromium Singleton* files from <paramref name="profileDir"/> so the next
    /// launch does not hit the "profile in use / recovering" prompt.
    /// </summary>
    void Kill(int pid, string profileDir);

    /// <summary>Removes stale <c>Singleton{Lock,Socket,Cookie}</c> without killing anything.</summary>
    void CleanSingletonFiles(string profileDir);
}

public sealed class StaleBrowserKiller : IStaleBrowserKiller
{
    private static readonly string[] SingletonFiles = { "SingletonLock", "SingletonSocket", "SingletonCookie" };

    private readonly ILogger<StaleBrowserKiller> _logger;

    public StaleBrowserKiller(ILogger<StaleBrowserKiller> logger) => _logger = logger;

    public void Kill(int pid, string profileDir)
    {
        try
        {
            using var proc = Process.GetProcessById(pid);
            _logger.LogWarning("Killing stale browser process PID {Pid}", pid);
            proc.Kill(entireProcessTree: true);
            proc.WaitForExit(5_000);
        }
        catch (ArgumentException)
        {
            _logger.LogDebug("Stale browser PID {Pid} already gone", pid);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not kill stale browser PID {Pid}", pid);
        }

        CleanSingletonFiles(profileDir);
    }

    public void CleanSingletonFiles(string profileDir)
    {
        foreach (var name in SingletonFiles)
        {
            // File.Delete removes a symlink itself even when its target is missing,
            // and is a no-op when the path doesn't exist.
            try { File.Delete(Path.Combine(profileDir, name)); }
            catch { /* best effort */ }
        }
    }
}
