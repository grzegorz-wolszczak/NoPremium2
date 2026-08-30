using Microsoft.Extensions.Logging;

namespace NoPremium2.Browser;

/// <summary>Outcome of looking for a browser already running against our profile.</summary>
public abstract record ExistingBrowserResult
{
    private ExistingBrowserResult() { }

    /// <summary>A responding CDP port was found — connect to it.</summary>
    public sealed record Found(int Port) : ExistingBrowserResult;

    /// <summary>Nothing is running for this profile — safe to launch.</summary>
    public sealed record NotRunning : ExistingBrowserResult;

    /// <summary>
    /// A browser owns this profile but exposes no reachable CDP port. Relaunching
    /// would just hand off to it and time out, so the caller must kill it or abort.
    /// </summary>
    public sealed record RunningButUnreachable(int? Pid) : ExistingBrowserResult;
}

public interface IExistingBrowserResolver
{
    Task<ExistingBrowserResult> ResolveAsync(string profileDir, CancellationToken ct = default);
}

public sealed class ExistingBrowserResolver : IExistingBrowserResolver
{
    private const int ProbeAttempts = 5;
    private static readonly TimeSpan ProbeDelay = TimeSpan.FromMilliseconds(300);

    private readonly IDevToolsActivePortReader _dtapReader;
    private readonly ICdpPortDiscovery _procScan;
    private readonly IProfileLockInspector _lockInspector;
    private readonly ICdpChecker _cdpChecker;
    private readonly ILogger<ExistingBrowserResolver> _logger;

    public ExistingBrowserResolver(
        IDevToolsActivePortReader dtapReader,
        ICdpPortDiscovery procScan,
        IProfileLockInspector lockInspector,
        ICdpChecker cdpChecker,
        ILogger<ExistingBrowserResolver> logger)
    {
        _dtapReader = dtapReader;
        _procScan = procScan;
        _lockInspector = lockInspector;
        _cdpChecker = cdpChecker;
        _logger = logger;
    }

    public async Task<ExistingBrowserResult> ResolveAsync(string profileDir, CancellationToken ct = default)
    {
        // 1. Chromium's own DevToolsActivePort file — the canonical, browser-agnostic signal.
        var dtap = _dtapReader.Read(profileDir);
        if (dtap is not null)
        {
            if (await ProbeWithRetryAsync(dtap.Port, ct))
            {
                _logger.LogInformation("Reconnecting to existing browser via DevToolsActivePort (port {Port})", dtap.Port);
                return new ExistingBrowserResult.Found(dtap.Port);
            }
            _logger.LogWarning(
                "DevToolsActivePort names port {Port} but CDP is not responding — stale file or wedged browser",
                dtap.Port);
        }

        // 2. /proc scan, constrained to our profile (covers a manually deleted DevToolsActivePort).
        var scanned = await _procScan.FindExistingPortAsync(profileDir);
        if (scanned is not null)
        {
            _logger.LogInformation("Reconnecting to existing browser found via /proc scan (port {Port})", scanned);
            return new ExistingBrowserResult.Found(scanned.Value);
        }

        // 3. No reachable port. Is a browser nonetheless holding the profile?
        var lockResult = _lockInspector.Inspect(profileDir);
        if (lockResult.Status == ProfileLockStatus.OwnerAlive)
        {
            _logger.LogWarning("Profile {Profile} is locked by live PID {Pid} but has no reachable CDP port",
                profileDir, lockResult.OwnerPid);
            return new ExistingBrowserResult.RunningButUnreachable(lockResult.OwnerPid);
        }

        return new ExistingBrowserResult.NotRunning();
    }

    private async Task<bool> ProbeWithRetryAsync(int port, CancellationToken ct)
    {
        for (int attempt = 1; attempt <= ProbeAttempts; attempt++)
        {
            if (await _cdpChecker.IsRespondingAsync(port, ct)) return true;
            if (attempt < ProbeAttempts)
                await Task.Delay(ProbeDelay, ct);
        }
        return false;
    }
}
