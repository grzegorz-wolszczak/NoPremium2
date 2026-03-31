using Microsoft.Extensions.Logging;
using Microsoft.Playwright;

namespace NoPremium2.Browser;

public interface IBrowserConnector
{
    Task<(IPlaywright Playwright, IBrowser Browser, IPage Page)> ConnectAsync(int port, CancellationToken ct = default);
}

public sealed class PlaywrightBrowserConnector : IBrowserConnector
{
    private readonly ILogger<PlaywrightBrowserConnector> _logger;

    public PlaywrightBrowserConnector(ILogger<PlaywrightBrowserConnector> logger) => _logger = logger;

    public async Task<(IPlaywright Playwright, IBrowser Browser, IPage Page)> ConnectAsync(int port, CancellationToken ct = default)
    {
        var playwright = await Playwright.CreateAsync();
        var browser = await playwright.Chromium.ConnectOverCDPAsync($"http://localhost:{port}");
        _logger.LogInformation("Connected to browser: {Name} v{Version}", browser.BrowserType.Name, browser.Version);

        var context = await WaitForContextAsync(browser, ct);
        var page = context.Pages.Count > 0 ? context.Pages[0] : await context.NewPageAsync();
        _logger.LogDebug("Page ready, URL: {Url}", page.Url);

        return (playwright, browser, page);
    }

    private static async Task<IBrowserContext> WaitForContextAsync(IBrowser browser, CancellationToken ct)
    {
        for (int i = 0; i < 20; i++)
        {
            if (browser.Contexts.Count > 0) return browser.Contexts[0];
            await Task.Delay(300, ct);
        }
        throw new TimeoutException("No browser context available after CDP connection");
    }
}
