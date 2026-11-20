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
    private readonly ILoginStateChecker _loginStateChecker;
    private readonly BaseConfig _config;
    //private readonly NoPremium2.Infrastructure.SessionPageSaver _sessionPageSaver;
    private readonly ILogger<BrowserSessionProvider> _logger;

    public BrowserSessionProvider(
        IBrowserManager browserManager,
        ILoginService loginService,
        ILoginStateChecker loginStateChecker,
        BaseConfig config,
        //NoPremium2.Infrastructure.SessionPageSaver sessionPageSaver,
        ILogger<BrowserSessionProvider> logger)
    {
        _browserManager = browserManager;
        _loginService = loginService;
        _loginStateChecker = loginStateChecker;
        _config = config;
        //_sessionPageSaver = sessionPageSaver;
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
            try
            {
                return await action(page);
            }
            catch (SessionExpiredException ex) when (!ct.IsCancellationRequested)
            {
                // Logged out in the middle of the action (detected after a navigation).
                // Log in again and retry the action once; a second expiry propagates.
                _logger.LogWarning("Session expired during action ({Message}), logging in again and retrying once",
                    ex.Message);
                await LoginOrThrowAsync(page, "re-login after session expiry");
                return await action(page);
            }
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
        {
            // Browser is alive — but the site may have logged us out. Check the current page.
            // NOTE: the DOM may be stale (last loaded page); a fresh check happens after each
            // navigation in NoPremiumBrowserClient, which throws SessionExpiredException.
            if (await _loginStateChecker.IsLoggedInAsync(_session.Page, "before using page"))
                return _session.Page;

            await LoginOrThrowAsync(_session.Page, "re-login before using page");
            return _session.Page;
        }

        if (_session is not null)
        {
            _logger.LogWarning("Browser session is dead, reconnecting...");
            try { _session.Dispose(); } catch { }
            _session = null;
        }

        _logger.LogInformation("Starting browser session...");
        _session = await _browserManager.GetOrLaunchAsync(ct);
        //_sessionPageSaver.AttachToPage(_session.Page);

        await LoginOrThrowAsync(_session.Page, "login after browser (re)connect");
        return _session.Page;
    }

    private async Task LoginOrThrowAsync(IPage page, string context)
    {
        _logger.LogInformation("Logging in to nopremium.pl ({Context})...", context);
        var result = await _loginService.LoginAsync(page, _config.NoPremiumUsername, _config.NoPremiumPassword);

        if (!result.Success)
            throw new InvalidOperationException(
                $"Login to nopremium.pl failed ({context}). Final URL: {result.FinalUrl}");

        _logger.LogInformation("Login successful ({Context})", context);

        // Diagnostic only: log whether the page we landed on really shows a logged-in session.
        await _loginStateChecker.IsLoggedInAsync(page, $"after {context}");
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
        // Browser is intentionally left running — user may want to keep it open after shutdown.
        try { session.Dispose(); }
        catch (Exception ex) { _logger.LogWarning(ex, "Error disposing browser session"); }
    }
}