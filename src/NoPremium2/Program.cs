using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NoPremium2;
using NoPremium2.Browser;
using NoPremium2.Login;

string login = GetRequiredEnv("NPREMIUM_P");
string password = GetRequiredEnv("NPREMIUM_U");

var services = new ServiceCollection();

services.AddLogging(b => b
    .SetMinimumLevel(LogLevel.Debug)
    .AddSimpleConsole(o => { o.TimestampFormat = "[HH:mm:ss] "; o.SingleLine = true; }));

var http = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };

services.AddSingleton(new AppSettings());
services.AddSingleton<HttpClient>(http);
services.AddSingleton<ICdpChecker, HttpCdpChecker>();
services.AddSingleton<IProcessCmdlineReader, LinuxProcessCmdlineReader>();
services.AddSingleton<ICdpPortDiscovery, CdpPortDiscovery>();
services.AddSingleton<IPortAllocator, PortAllocator>();
services.AddSingleton<IVivaldiLauncher, VivaldiLauncher>();
services.AddSingleton<IBrowserConnector, PlaywrightBrowserConnector>();
services.AddSingleton<IBrowserManager, BrowserManager>();
services.AddSingleton<ILoginService, LoginService>();

await using var provider = services.BuildServiceProvider();

var logger = provider.GetRequiredService<ILogger<Program>>();
var browserManager = provider.GetRequiredService<IBrowserManager>();
var loginService = provider.GetRequiredService<ILoginService>();

logger.LogInformation("Starting NoPremium2");

BrowserSession? session = null;
try
{
    session = await browserManager.GetOrLaunchAsync();
    var result = await loginService.LoginAsync(session.Page, login, password);

    if (result.Success)
    {
        logger.LogInformation("Login successful — browser stays open");
        // Not killing the browser — user can continue working with it
    }
    else
    {
        logger.LogWarning("Login failed at: {Url}", result.FinalUrl);
        if (session.IsOwned)
        {
            logger.LogInformation("Closing owned browser");
            session.KillOwnedBrowser();
        }
    }
}
catch (Exception ex)
{
    logger.LogError(ex, "Unexpected error");
    session?.KillOwnedBrowser();
}
finally
{
    session?.Dispose(); // Disposes Playwright connection; does NOT kill browser process
}

static string GetRequiredEnv(string name) =>
    Environment.GetEnvironmentVariable(name)
    ?? throw new InvalidOperationException($"Missing environment variable: {name}");
