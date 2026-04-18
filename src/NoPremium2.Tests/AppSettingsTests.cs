using AwesomeAssertions;
using NoPremium2.Config;
using Xunit;

namespace NoPremium2.Tests;

public sealed class AppSettingsTests
{
    // ── AppSettings.From ──────────────────────────────────────────────

    [Fact]
    public void From_MapsLoginUrl()
    {
        var config = ConfigWith(loginUrl: "https://www.nopremium.pl/login");

        AppSettings.From(config).LoginUrl.Should().Be("https://www.nopremium.pl/login");
    }

    [Fact]
    public void From_MapsCdpReadyTimeoutMs()
    {
        var config = ConfigWith(cdpTimeout: 15_000);

        AppSettings.From(config).CdpReadyTimeoutMs.Should().Be(15_000);
    }

    [Fact]
    public void From_MapsTurnstileTimeoutMs()
    {
        var config = ConfigWith(turnstileTimeout: 90_000);

        AppSettings.From(config).TurnstileTimeoutMs.Should().Be(90_000);
    }

    [Fact]
    public void From_AllFieldsMappedTogether()
    {
        var config = ConfigWith(
            loginUrl:          "https://example.com/login",
            cdpTimeout:        5_000,
            turnstileTimeout:  60_000);

        var settings = AppSettings.From(config);

        settings.LoginUrl.Should().Be("https://example.com/login");
        settings.CdpReadyTimeoutMs.Should().Be(5_000);
        settings.TurnstileTimeoutMs.Should().Be(60_000);
    }

    private static BaseConfig ConfigWith(
        string loginUrl        = "https://www.nopremium.pl/login",
        int cdpTimeout         = 10_000,
        int turnstileTimeout   = 120_000) =>
        new BaseConfig
        {
            NoPremiumUsername = "u",
            NoPremiumPassword = "p",
            EmailUsername     = "e",
            EmailPassword     = "ep",
            EmailImapServer   = "imap.example.com:993",
            LinksFilePath     = "links.json",
            LoginUrl          = loginUrl,
            CdpReadyTimeoutMs = cdpTimeout,
            TurnstileTimeoutMs = turnstileTimeout,
        };
}
