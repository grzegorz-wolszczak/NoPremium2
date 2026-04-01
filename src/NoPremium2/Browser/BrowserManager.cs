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
    private readonly ICdpPortDiscovery _cdpDiscovery;
    private readonly IPortAllocator _portAllocator;
    private readonly IVivaldiLauncher _launcher;
    private readonly IBrowserConnector _connector;
    private readonly ILogger<BrowserManager> _logger;

    public BrowserManager(
        AppSettings settings,
        ICdpPortDiscovery cdpDiscovery,
        IPortAllocator portAllocator,
        IVivaldiLauncher launcher,
        IBrowserConnector connector,
        ILogger<BrowserManager> logger)
    {
        _settings = settings;
        _cdpDiscovery = cdpDiscovery;
        _portAllocator = portAllocator;
        _launcher = launcher;
        _connector = connector;
        _logger = logger;
    }

    public async Task<BrowserSession> GetOrLaunchAsync(CancellationToken ct = default)
    {
        int? existingPort = await _cdpDiscovery.FindExistingPortAsync();

        int cdpPort;
        bool isOwned;
        Process? ownedProcess = null;

        if (existingPort.HasValue)
        {
            cdpPort = existingPort.Value;
            isOwned = false;
            _logger.LogInformation("Using existing browser CDP on port {Port}", cdpPort);
        }
        else
        {
            cdpPort = _portAllocator.GetFreePort();
            isOwned = true;
            _logger.LogInformation("OS-allocated free port: {Port}", cdpPort);
            ownedProcess = _launcher.Launch(cdpPort, _settings.ProfileDir, _settings.LoginUrl);
            await _launcher.WaitForCdpAsync(cdpPort, ct);
        }

        var (playwright, browser, page) = await _connector.ConnectAsync(cdpPort, ct);
        return new BrowserSession(playwright, browser, page, isOwned, ownedProcess);
    }
}
