using NSubstitute;
using AwesomeAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Playwright;
using NoPremium2.Login;
using Xunit;

namespace NoPremium2.Tests.Login;

public sealed class LoginServiceTests
{
    private readonly AppSettings _settings = new();
    private readonly ILogger<LoginService> _logger = Substitute.For<ILogger<LoginService>>();

    private LoginService CreateSut() => new(_settings, _logger);

    private static IPage SetupPage(string currentUrl, string postLoginUrl)
    {
        var page = Substitute.For<IPage>();

        var loginForm = Substitute.For<ILocator>();
        var loginInput = Substitute.For<ILocator>();
        var passwordInput = Substitute.For<ILocator>();
        var submitButton = Substitute.For<ILocator>();

        page.Locator("#login_box_form").Returns(loginForm);
        loginForm.Locator("input[name='login']").Returns(loginInput);
        loginForm.Locator("input[name='password']").Returns(passwordInput);
        page.Locator("#button_input").Returns(submitButton);

        // Simulate URL change after WaitForURLAsync
        page.Url.Returns(currentUrl);
        page.WaitForURLAsync(Arg.Any<Func<string, bool>>(), Arg.Any<PageWaitForURLOptions?>())
            .Returns(Task.CompletedTask)
            .AndDoes(_ => page.Url.Returns(postLoginUrl));

        return page;
    }

    [Fact]
    public async Task LoginAsync_WhenAlreadyOnLoginPage_DoesNotNavigate()
    {
        var page = SetupPage("https://www.nopremium.pl/login", "https://www.nopremium.pl/settings?secure");

        await CreateSut().LoginAsync(page, "user", "pass");

        await page.DidNotReceive().GotoAsync(Arg.Any<string>(), Arg.Any<PageGotoOptions?>());
    }

    [Fact]
    public async Task LoginAsync_WhenNotOnLoginPage_NavigatesToLoginUrl()
    {
        var page = SetupPage("https://www.nopremium.pl/", "https://www.nopremium.pl/settings?secure");

        await CreateSut().LoginAsync(page, "user", "pass");

        await page.Received(1).GotoAsync(_settings.LoginUrl, Arg.Any<PageGotoOptions?>());
    }

    [Fact]
    public async Task LoginAsync_FillsCredentialsAndSubmits()
    {
        var page = Substitute.For<IPage>();
        var loginForm = Substitute.For<ILocator>();
        var loginInput = Substitute.For<ILocator>();
        var passwordInput = Substitute.For<ILocator>();
        var submitButton = Substitute.For<ILocator>();

        page.Url.Returns("https://www.nopremium.pl/login");
        page.Locator("#login_box_form").Returns(loginForm);
        loginForm.Locator("input[name='login']").Returns(loginInput);
        loginForm.Locator("input[name='password']").Returns(passwordInput);
        page.Locator("#button_input").Returns(submitButton);
        page.WaitForURLAsync(Arg.Any<Func<string, bool>>(), Arg.Any<PageWaitForURLOptions?>())
            .Returns(Task.CompletedTask)
            .AndDoes(_ => page.Url.Returns("https://www.nopremium.pl/settings?secure"));

        await CreateSut().LoginAsync(page, "testuser", "testpass");

        await loginInput.Received(1).FillAsync("testuser", Arg.Any<LocatorFillOptions?>());
        await passwordInput.Received(1).FillAsync("testpass", Arg.Any<LocatorFillOptions?>());
        await submitButton.Received(1).ClickAsync(Arg.Any<LocatorClickOptions?>());
    }

    [Fact]
    public async Task LoginAsync_WhenRedirectedAwayFromLogin_ReturnsSuccess()
    {
        var page = SetupPage("https://www.nopremium.pl/login", "https://www.nopremium.pl/settings?secure");

        var result = await CreateSut().LoginAsync(page, "user", "pass");

        result.Success.Should().BeTrue();
        result.FinalUrl.Should().Be("https://www.nopremium.pl/settings?secure");
    }

    [Fact]
    public async Task LoginAsync_WhenStillOnLoginPage_ReturnsFailure()
    {
        var page = SetupPage("https://www.nopremium.pl/login", "https://www.nopremium.pl/login?error=1");

        var result = await CreateSut().LoginAsync(page, "wrong", "creds");

        result.Success.Should().BeFalse();
    }
}
