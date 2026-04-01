using Microsoft.Extensions.Logging;
using Microsoft.Playwright;
using NoPremium2.Config;
using NoPremium2.Login;

namespace NoPremium2.Browser;

public interface IBrowserSessionProvider
{
    /// <summary>
    /// Executes an action with exclusive access to the browser page.
    /// Handles reconnection if the browser was closed.
    /// </summary>
    Task<T> UsePageAsync<T>(Func<IPage, Task<T>> action, CancellationToken ct = default);
    Task UsePageAsync(Func<IPage, Task> action, CancellationToken ct = default);

    /// <summary>Called once at startup to establish the initial session and verify login.</summary>
    Task InitializeAsync(CancellationToken ct = default);
}

public sealed class BrowserSessionProvider : IBrowserSessionProvider, IAsyncDisposable
{
    private readonly SemaphoreSlim _semaphore = new(1, 1);
    private BrowserSession? _session;

    private readonly IBrowserManager _browserManager;
    private readonly ILoginService _loginService;
    private readonly AppConfig _config;
    private readonly ILogger<BrowserSessionProvider> _logger;

    public BrowserSessionProvider(
        IBrowserManager browserManager,
        ILoginService loginService,
        AppConfig config,
        ILogger<BrowserSessionProvider> logger)
    {
        _browserManager = browserManager;
        _loginService = loginService;
        _config = config;
        _logger = logger;
    }

    public async Task InitializeAsync(CancellationToken ct = default)
    {
        await _semaphore.WaitAsync(ct);
        try
        {
            await EnsureSessionAsync(ct);
        }
        finally
        {
            _semaphore.Release();
        }
    }

    public async Task<T> UsePageAsync<T>(Func<IPage, Task<T>> action, CancellationToken ct = default)
    {
        await _semaphore.WaitAsync(ct);
        try
        {
            var page = await EnsureSessionAsync(ct);
            return await action(page);
        }
        catch (Microsoft.Playwright.PlaywrightException) when (ct.IsCancellationRequested)
        {
            // Browser was closed as part of graceful shutdown — treat as cancellation.
            throw new OperationCanceledException(ct);
        }
        finally
        {
            _semaphore.Release();
        }
    }

    public async Task UsePageAsync(Func<IPage, Task> action, CancellationToken ct = default)
    {
        await UsePageAsync<bool>(async page =>
        {
            await action(page);
            return true;
        }, ct);
    }

    private async Task<IPage> EnsureSessionAsync(CancellationToken ct)
    {
        if (_session is not null && IsSessionAlive(_session))
            return _session.Page;

        if (_session is not null)
        {
            _logger.LogWarning("Browser session is dead, reconnecting...");
            try { _session.Dispose(); } catch { }
            _session = null;
        }

        _logger.LogInformation("Starting browser session...");
        _session = await _browserManager.GetOrLaunchAsync(ct);

        _logger.LogInformation("Logging in to nopremium.pl...");
        var result = await _loginService.LoginAsync(
            _session.Page,
            _config.NoPremiumUsername,
            _config.NoPremiumPassword);

        if (!result.Success)
            throw new InvalidOperationException(
                $"Login to nopremium.pl failed. Final URL: {result.FinalUrl}");

        _logger.LogInformation("Login successful");
        return _session.Page;
    }

    internal static bool IsSessionAlive(BrowserSession session)
    {
        try { return session.Browser.IsConnected && !session.Page.IsClosed; }
        catch { return false; }
    }

    public async ValueTask DisposeAsync()
    {
        var session = Interlocked.Exchange(ref _session, null);
        if (session is null) return;
        try
        {
            session.KillOwnedBrowser();
            session.Dispose();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error disposing browser session");
        }
    }
}
