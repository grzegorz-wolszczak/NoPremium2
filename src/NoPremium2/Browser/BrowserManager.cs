using System.Diagnostics;
using Microsoft.Extensions.Logging;

namespace NoPremium2.Browser;

public interface IBrowserManager
{
    Task<BrowserSession> GetOrLaunchAsync(CancellationToken ct = default);
}

public sealed class BrowserManager : IBrowserManager
{
    private readonly AppSettings _settings;
    private readonly IExistingBrowserResolver _resolver;
    private readonly IStaleBrowserKiller _staleKiller;
    private readonly IPortAllocator _portAllocator;
    private readonly IVivaldiLauncher _launcher;
    private readonly IBrowserConnector _connector;
    private readonly ILogger<BrowserManager> _logger;

    public BrowserManager(
        AppSettings settings,
        IExistingBrowserResolver resolver,
        IStaleBrowserKiller staleKiller,
        IPortAllocator portAllocator,
        IVivaldiLauncher launcher,
        IBrowserConnector connector,
        ILogger<BrowserManager> logger)
    {
        _settings = settings;
        _resolver = resolver;
        _staleKiller = staleKiller;
        _portAllocator = portAllocator;
        _launcher = launcher;
        _connector = connector;
        _logger = logger;
    }

    public async Task<BrowserSession> GetOrLaunchAsync(CancellationToken ct = default)
    {
        var profileDir = _settings.ProfileDir;

        int cdpPort;
        bool isOwned;
        Process? ownedProcess = null;

        switch (await _resolver.ResolveAsync(profileDir, ct))
        {
            case ExistingBrowserResult.Found found:
                cdpPort = found.Port;
                isOwned = false;
                _logger.LogInformation("Using existing browser CDP on port {Port}", cdpPort);
                break;

            case ExistingBrowserResult.RunningButUnreachable unreachable when !_settings.KillStaleBrowser:
                throw new InvalidOperationException(
                    $"A browser is already open for the nopremium profile ({profileDir}) " +
                    $"(process {unreachable.Pid?.ToString() ?? "unknown"}) but its remote-debugging " +
                    "port is not reachable. Zamknij to okno przeglądarki i uruchom NoPremium2 ponownie " +
                    "(albo ustaw KillStaleBrowser: true w configu).");

            case ExistingBrowserResult.RunningButUnreachable unreachable:
                _logger.LogWarning("KillStaleBrowser=true — killing unreachable browser and relaunching");
                if (unreachable.Pid is int pid)
                    _staleKiller.Kill(pid, profileDir);
                else
                    _staleKiller.CleanSingletonFiles(profileDir);
                (cdpPort, isOwned, ownedProcess) = await LaunchAsync(profileDir, ct);
                break;

            default: // NotRunning
                (cdpPort, isOwned, ownedProcess) = await LaunchAsync(profileDir, ct);
                break;
        }

        var (playwright, browser, page) = await _connector.ConnectAsync(cdpPort, ct);
        return new BrowserSession(playwright, browser, page, isOwned, ownedProcess);
    }

    private async Task<(int Port, bool IsOwned, Process? Process)> LaunchAsync(string profileDir, CancellationToken ct)
    {
        int port = _portAllocator.GetFreePort();
        _logger.LogInformation("OS-allocated free port: {Port}", port);
        var process = _launcher.Launch(port, profileDir);
        await _launcher.WaitForCdpAsync(port, profileDir, process, ct);
        return (port, true, process);
    }
}
