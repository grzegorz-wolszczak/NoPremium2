using AwesomeAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Playwright;
using NoPremium2.Browser;
using NoPremium2.Config;
using NoPremium2.Login;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Xunit;

namespace NoPremium2.Tests.Browser;

public sealed class BrowserSessionProviderTests
{
    // ── IsSessionAlive ────────────────────────────────────────────────

    [Fact]
    public void IsSessionAlive_BrowserConnectedAndPageOpen_ReturnsTrue()
    {
        var session = MakeSession(isConnected: true, isClosed: false);

        BrowserSessionProvider.IsSessionAlive(session).Should().BeTrue();
    }

    [Fact]
    public void IsSessionAlive_BrowserDisconnected_ReturnsFalse()
    {
        var session = MakeSession(isConnected: false, isClosed: false);

        BrowserSessionProvider.IsSessionAlive(session).Should().BeFalse();
    }

    [Fact]
    public void IsSessionAlive_PageClosed_ReturnsFalse()
    {
        var session = MakeSession(isConnected: true, isClosed: true);

        BrowserSessionProvider.IsSessionAlive(session).Should().BeFalse();
    }

    [Fact]
    public void IsSessionAlive_BrowserThrows_ReturnsFalse()
    {
        var playwright = Substitute.For<IPlaywright>();
        var browser    = Substitute.For<IBrowser>();
        var page       = Substitute.For<IPage>();

        browser.IsConnected.Throws(new InvalidOperationException("browser gone"));

        var session = new BrowserSession(playwright, browser, page, isOwned: false, ownedProcess: null);

        BrowserSessionProvider.IsSessionAlive(session).Should().BeFalse();
    }

    [Fact]
    public void IsSessionAlive_PageThrows_ReturnsFalse()
    {
        var playwright = Substitute.For<IPlaywright>();
        var browser    = Substitute.For<IBrowser>();
        var page       = Substitute.For<IPage>();

        browser.IsConnected.Returns(true);
        page.IsClosed.Throws(new InvalidOperationException("page gone"));

        var session = new BrowserSession(playwright, browser, page, isOwned: false, ownedProcess: null);

        BrowserSessionProvider.IsSessionAlive(session).Should().BeFalse();
    }

    // ── Login state handling ──────────────────────────────────────────

    private readonly IBrowserManager _browserManager = Substitute.For<IBrowserManager>();
    private readonly ILoginService _loginService = Substitute.For<ILoginService>();
    private readonly ILoginStateChecker _checker = Substitute.For<ILoginStateChecker>();
    private readonly BaseConfig _config = new() { NoPremiumUsername = "user", NoPremiumPassword = "pass" };

    private BrowserSessionProvider CreateSut(BrowserSession session)
    {
        _browserManager.GetOrLaunchAsync(Arg.Any<CancellationToken>()).Returns(session);
        _loginService.LoginAsync(Arg.Any<IPage>(), Arg.Any<string>(), Arg.Any<string>())
            .Returns(new LoginResult(true, "https://www.nopremium.pl/files"));
        return new BrowserSessionProvider(_browserManager, _loginService, _checker, _config,
            Substitute.For<ILogger<BrowserSessionProvider>>());
    }

    [Fact]
    public async Task UsePageAsync_FirstUse_LogsInOnce()
    {
        var session = MakeSession(isConnected: true, isClosed: false);
        var sut = CreateSut(session);

        await sut.UsePageAsync(_ => Task.CompletedTask);

        await _loginService.Received(1).LoginAsync(session.Page, "user", "pass");
    }

    [Fact]
    public async Task UsePageAsync_AliveSessionStillLoggedIn_DoesNotLogInAgain()
    {
        var session = MakeSession(isConnected: true, isClosed: false);
        var sut = CreateSut(session);
        await sut.InitializeAsync();
        _loginService.ClearReceivedCalls();
        _checker.IsLoggedInAsync(session.Page, Arg.Any<string>()).Returns(true);

        await sut.UsePageAsync(_ => Task.CompletedTask);

        await _loginService.DidNotReceiveWithAnyArgs().LoginAsync(default!, default!, default!);
        await _checker.Received().IsLoggedInAsync(session.Page, "before using page");
    }

    [Fact]
    public async Task UsePageAsync_AliveSessionLoggedOut_LogsInBeforeAction()
    {
        var session = MakeSession(isConnected: true, isClosed: false);
        var sut = CreateSut(session);
        await sut.InitializeAsync();
        _loginService.ClearReceivedCalls();
        _checker.IsLoggedInAsync(session.Page, "before using page").Returns(false);

        bool actionRan = false;
        await sut.UsePageAsync(_ => { actionRan = true; return Task.CompletedTask; });

        actionRan.Should().BeTrue();
        await _loginService.Received(1).LoginAsync(session.Page, "user", "pass");
    }

    [Fact]
    public async Task UsePageAsync_SessionExpiresDuringAction_LogsInAndRetriesOnce()
    {
        var session = MakeSession(isConnected: true, isClosed: false);
        var sut = CreateSut(session);
        await sut.InitializeAsync();
        _loginService.ClearReceivedCalls();
        _checker.IsLoggedInAsync(session.Page, "before using page").Returns(true);

        int calls = 0;
        var result = await sut.UsePageAsync(_ =>
        {
            calls++;
            if (calls == 1) throw new SessionExpiredException("test", "https://www.nopremium.pl/login");
            return Task.FromResult(42);
        });

        result.Should().Be(42);
        calls.Should().Be(2);
        await _loginService.Received(1).LoginAsync(session.Page, "user", "pass");
    }

    [Fact]
    public async Task UsePageAsync_SessionExpiresTwice_Propagates()
    {
        var session = MakeSession(isConnected: true, isClosed: false);
        var sut = CreateSut(session);
        _checker.IsLoggedInAsync(session.Page, Arg.Any<string>()).Returns(true);

        var act = () => sut.UsePageAsync<int>(_ =>
            throw new SessionExpiredException("test", "https://www.nopremium.pl/login"));

        await act.Should().ThrowAsync<SessionExpiredException>();
    }

    [Fact]
    public async Task UsePageAsync_ReloginFails_Throws()
    {
        var session = MakeSession(isConnected: true, isClosed: false);
        var sut = CreateSut(session);
        await sut.InitializeAsync();
        _checker.IsLoggedInAsync(session.Page, "before using page").Returns(false);
        _loginService.LoginAsync(Arg.Any<IPage>(), Arg.Any<string>(), Arg.Any<string>())
            .Returns(new LoginResult(false, "https://www.nopremium.pl/login"));

        var act = () => sut.UsePageAsync(_ => Task.CompletedTask);

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    // ─────────────────────────────────────────────────────────────────

    private static BrowserSession MakeSession(bool isConnected, bool isClosed)
    {
        var playwright = Substitute.For<IPlaywright>();
        var browser    = Substitute.For<IBrowser>();
        var page       = Substitute.For<IPage>();

        browser.IsConnected.Returns(isConnected);
        page.IsClosed.Returns(isClosed);

        return new BrowserSession(playwright, browser, page, isOwned: false, ownedProcess: null);
    }
}
