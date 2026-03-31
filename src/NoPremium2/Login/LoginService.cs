using Microsoft.Extensions.Logging;
using Microsoft.Playwright;

namespace NoPremium2.Login;

public interface ILoginService
{
    Task<LoginResult> LoginAsync(IPage page, string login, string password);
}

public sealed class LoginService : ILoginService
{
    private readonly AppSettings _settings;
    private readonly ILogger<LoginService> _logger;

    public LoginService(AppSettings settings, ILogger<LoginService> logger)
    {
        _settings = settings;
        _logger = logger;
    }

    public async Task<LoginResult> LoginAsync(IPage page, string login, string password)
    {
        if (!page.Url.Contains("/login"))
        {
            _logger.LogInformation("Navigating to login page: {Url}", _settings.LoginUrl);
            await page.GotoAsync(_settings.LoginUrl, new PageGotoOptions
            {
                WaitUntil = WaitUntilState.DOMContentLoaded,
                Timeout = 60_000,
            });
        }
        else
        {
            _logger.LogInformation("Already on login page: {Url}", page.Url);
        }

        _logger.LogInformation("Waiting for login form (Cloudflare Turnstile)...");
        var loginForm = page.Locator("#login_box_form");
        var loginInput = loginForm.Locator("input[name='login']");
        await loginInput.WaitForAsync(new LocatorWaitForOptions
        {
            State = WaitForSelectorState.Visible,
            Timeout = _settings.TurnstileTimeoutMs,
        });

        _logger.LogInformation("Form visible, filling credentials (login: {Login})", login);
        await loginInput.FillAsync(login);
        await loginForm.Locator("input[name='password']").FillAsync(password);

        _logger.LogInformation("Clicking submit");
        await page.Locator("#button_input").ClickAsync();

        await page.WaitForURLAsync(url => !url.Contains("/login"), new PageWaitForURLOptions
        {
            Timeout = 30_000,
        });

        bool success = !page.Url.Contains("/login");
        var result = new LoginResult(success, page.Url);

        if (success)
            _logger.LogInformation("Login successful, redirected to: {Url}", result.FinalUrl);
        else
            _logger.LogWarning("Login may have failed, still on: {Url}", result.FinalUrl);

        return result;
    }
}
