using AwesomeAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Playwright;
using NoPremium2.Login;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Xunit;

namespace NoPremium2.Tests.Login;

public sealed class LoginStateCheckerTests
{
    private const string SignedText =
        "Pozostały transfer: 2097.52 GB (w tym 22.34 GB transferu Premium + 2075.18 GB transferu dodatkowego)";

    private readonly ILogger<LoginStateChecker> _logger = Substitute.For<ILogger<LoginStateChecker>>();

    // ── IsLoggedIn (pure) ─────────────────────────────────────────────

    [Theory]
    [InlineData("https://www.nopremium.pl/files")]
    [InlineData("https://www.nopremium.pl/voucher")]
    [InlineData("https://nopremium.pl/settings?secure")]
    public void IsLoggedIn_NoPremiumPageWithSignedPanel_ReturnsTrue(string url)
        => LoginStateChecker.IsLoggedIn(url, SignedText).Should().BeTrue();

    [Theory]
    [InlineData("https://www.nopremium.pl/login")]
    [InlineData("https://www.nopremium.pl/login?redirect=files")]
    public void IsLoggedIn_LoginPage_ReturnsFalse(string url)
        => LoginStateChecker.IsLoggedIn(url, SignedText).Should().BeFalse();

    [Theory]
    [InlineData("about:blank")]
    [InlineData("chrome://newtab/")]
    [InlineData("https://example.com/files")]
    [InlineData("https://nopremium.pl.evil.com/files")]
    public void IsLoggedIn_NotNoPremiumPage_ReturnsFalse(string url)
        => LoginStateChecker.IsLoggedIn(url, SignedText).Should().BeFalse();

    [Fact]
    public void IsLoggedIn_SignedPanelMissing_ReturnsFalse()
        => LoginStateChecker.IsLoggedIn("https://www.nopremium.pl/files", null).Should().BeFalse();

    [Fact]
    public void IsLoggedIn_SignedPanelWithoutTransferInfo_ReturnsFalse()
        => LoginStateChecker.IsLoggedIn("https://www.nopremium.pl/files", "Zaloguj się").Should().BeFalse();

    [Fact]
    public void IsLoginUrl_DoesNotMatchLoginInQueryOnly()
        => LoginStateChecker.IsLoginUrl("https://www.nopremium.pl/files?from=/login").Should().BeFalse();

    // ── IsLoggedInAsync ───────────────────────────────────────────────

    [Fact]
    public async Task IsLoggedInAsync_PageWithSignedPanel_ReturnsTrueAndLogsInformation()
    {
        var page = MakePage("https://www.nopremium.pl/files", signedCount: 1, signedText: SignedText);

        var result = await new LoginStateChecker(_logger).IsLoggedInAsync(page, "test");

        result.Should().BeTrue();
        LoggedLevels().Should().Contain(LogLevel.Information).And.NotContain(LogLevel.Warning);
    }

    [Fact]
    public async Task IsLoggedInAsync_RedirectedToLogin_ReturnsFalseAndLogsWarning()
    {
        var page = MakePage("https://www.nopremium.pl/login", signedCount: 0, signedText: null);

        var result = await new LoginStateChecker(_logger).IsLoggedInAsync(page, "test");

        result.Should().BeFalse();
        LoggedLevels().Should().Contain(LogLevel.Warning).And.NotContain(LogLevel.Information);
        page.DidNotReceive().Locator(Arg.Any<string>(), Arg.Any<PageLocatorOptions?>());
    }

    [Fact]
    public async Task IsLoggedInAsync_SignedPanelMissing_ReturnsFalse()
    {
        var page = MakePage("https://www.nopremium.pl/files", signedCount: 0, signedText: null);

        (await new LoginStateChecker(_logger).IsLoggedInAsync(page, "test")).Should().BeFalse();
    }

    [Fact]
    public async Task IsLoggedInAsync_ReadingPanelThrows_ReturnsFalse()
    {
        var page = Substitute.For<IPage>();
        var signed = Substitute.For<ILocator>();
        page.Url.Returns("https://www.nopremium.pl/files");
        page.Locator("#signed", Arg.Any<PageLocatorOptions?>()).Returns(signed);
        signed.CountAsync().Throws(new PlaywrightException("target closed"));

        (await new LoginStateChecker(_logger).IsLoggedInAsync(page, "test")).Should().BeFalse();
    }

    private List<LogLevel> LoggedLevels() => _logger.ReceivedCalls()
        .Where(c => c.GetMethodInfo().Name == nameof(ILogger.Log))
        .Select(c => (LogLevel)c.GetArguments()[0]!)
        .ToList();

    private static IPage MakePage(string url, int signedCount, string? signedText)
    {
        var page = Substitute.For<IPage>();
        var signed = Substitute.For<ILocator>();
        page.Url.Returns(url);
        page.Locator("#signed", Arg.Any<PageLocatorOptions?>()).Returns(signed);
        signed.CountAsync().Returns(signedCount);
        signed.InnerTextAsync(Arg.Any<LocatorInnerTextOptions?>()).Returns(signedText ?? "");
        return page;
    }
}
