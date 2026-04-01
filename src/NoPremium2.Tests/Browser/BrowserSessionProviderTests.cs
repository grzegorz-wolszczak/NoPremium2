using AwesomeAssertions;
using Microsoft.Playwright;
using NoPremium2.Browser;
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
