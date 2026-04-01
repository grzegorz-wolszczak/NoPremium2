using AwesomeAssertions;
using NoPremium2.Config;
using Xunit;

namespace NoPremium2.Tests.Config;

/// <summary>
/// Snapshot tests for DefaultConstants.
/// Their purpose is to catch accidental changes to default values.
/// If you intentionally change a default, update the expected value here too.
/// </summary>
public sealed class DefaultConstantsTests
{
    // ── Scheduling ────────────────────────────────────────────────────

    [Fact]
    public void ScheduleStartTime_Is_2300()
        => DefaultConstants.ScheduleStartTime.Should().Be("23:00");

    [Fact]
    public void ScheduleEndTime_Is_2355()
        => DefaultConstants.ScheduleEndTime.Should().Be("23:55");

    [Fact]
    public void ScheduleIntervalMinutes_Is_5()
        => DefaultConstants.ScheduleIntervalMinutes.Should().Be(5);

    // ── Transfer consumer ─────────────────────────────────────────────

    [Fact]
    public void ReserveTransferBytes_Is_3GB()
    {
        const long threeGigabytes = 3L * 1024 * 1024 * 1024;
        DefaultConstants.ReserveTransferBytes.Should().Be(threeGigabytes);
    }

    // ── Keepalive ─────────────────────────────────────────────────────

    [Fact]
    public void KeepaliveInterval_Is_OneHour()
        => DefaultConstants.KeepaliveInterval.Should().Be("01:00:00");

    [Fact]
    public void KeepaliveInterval_ParsesTo_OneHour()
        => TimeSpan.Parse(DefaultConstants.KeepaliveInterval).Should().Be(TimeSpan.FromHours(1));

    // ── Browser / login ───────────────────────────────────────────────

    [Fact]
    public void LoginUrl_IsNoPremiumLoginPage()
        => DefaultConstants.LoginUrl.Should().Be("https://www.nopremium.pl/login");

    [Fact]
    public void CdpReadyTimeoutMs_Is_10_Seconds()
        => DefaultConstants.CdpReadyTimeoutMs.Should().Be(10_000);

    [Fact]
    public void TurnstileTimeoutMs_Is_120_Seconds()
        => DefaultConstants.TurnstileTimeoutMs.Should().Be(120_000);

    // ── Browser profile dirs ──────────────────────────────────────────

    [Fact]
    public void ChromeProfileDirName_IsCorrect()
        => DefaultConstants.ChromeProfileDirName.Should().Be("chrome-nopremium");

    [Fact]
    public void VivaldiProfileDirName_IsCorrect()
        => DefaultConstants.VivaldiProfileDirName.Should().Be("vivaldi-nopremium");
}
