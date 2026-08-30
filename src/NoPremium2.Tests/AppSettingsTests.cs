using AwesomeAssertions;
using NoPremium2.Config;
using Xunit;

namespace NoPremium2.Tests;

public sealed class AppSettingsTests
{
    private const string ProfileDir = "/home/test/.config/vivaldi-nopremium";

    // ── AppSettings.From ──────────────────────────────────────────────

    [Fact]
    public void From_MapsLoginUrl()
    {
        var config = ConfigWith(loginUrl: "https://www.nopremium.pl/login");

        AppSettings.From(config, ProfileDir).LoginUrl.Should().Be("https://www.nopremium.pl/login");
    }

    [Fact]
    public void From_MapsCdpReadyTimeoutMs()
    {
        var config = ConfigWith(cdpTimeout: 15_000);

        AppSettings.From(config, ProfileDir).CdpReadyTimeoutMs.Should().Be(15_000);
    }

    [Fact]
    public void From_MapsTurnstileTimeoutMs()
    {
        var config = ConfigWith(turnstileTimeout: 90_000);

        AppSettings.From(config, ProfileDir).TurnstileTimeoutMs.Should().Be(90_000);
    }

    [Fact]
    public void From_MapsProfileDirAndBrowserPath()
    {
        var settings = AppSettings.From(ConfigWith(), ProfileDir, "/usr/bin/vivaldi-stable");

        settings.ProfileDir.Should().Be(ProfileDir);
        settings.VivaldiPath.Should().Be("/usr/bin/vivaldi-stable");
    }

    [Fact]
    public void From_MapsKillStaleBrowser()
    {
        AppSettings.From(ConfigWith(killStale: true), ProfileDir).KillStaleBrowser.Should().BeTrue();
        AppSettings.From(ConfigWith(killStale: false), ProfileDir).KillStaleBrowser.Should().BeFalse();
    }

    [Fact]
    public void From_AllFieldsMappedTogether()
    {
        var config = ConfigWith(
            loginUrl:          "https://example.com/login",
            cdpTimeout:        5_000,
            turnstileTimeout:  60_000);

        var settings = AppSettings.From(config, ProfileDir);

        settings.LoginUrl.Should().Be("https://example.com/login");
        settings.CdpReadyTimeoutMs.Should().Be(5_000);
        settings.TurnstileTimeoutMs.Should().Be(60_000);
        settings.ProfileDir.Should().Be(ProfileDir);
    }

    private static BaseConfig ConfigWith(
        string loginUrl        = "https://www.nopremium.pl/login",
        int cdpTimeout         = 10_000,
        int turnstileTimeout   = 120_000,
        bool killStale         = false) =>
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
            KillStaleBrowser  = killStale,
        };
}
