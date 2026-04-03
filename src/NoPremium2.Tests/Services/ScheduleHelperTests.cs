using AwesomeAssertions;
using NoPremium2.Config;
using NoPremium2.Services;
using Xunit;

namespace NoPremium2.Tests.Services;

public sealed class ScheduleHelperTests
{
    private static readonly TimeOnly WindowStart = new(23, 0);
    private static readonly TimeOnly WindowEnd   = new(23, 55);

    // ── TimeUntilNextRun ──────────────────────────────────────────────

    [Fact]
    public void TimeUntilNextRun_InsideWindow_NeverRun_ReturnsZero()
    {
        var now = new DateTime(2024, 1, 15, 23, 30, 0); // 23:30 inside 23:00–23:55

        var result = ScheduleHelper.TimeUntilNextRun(now, WindowStart, WindowEnd,
            TimeSpan.FromMinutes(5), lastRunAt: null);

        result.Should().Be(TimeSpan.Zero);
    }

    [Fact]
    public void TimeUntilNextRun_InsideWindow_IntervalElapsed_ReturnsZero()
    {
        var now     = new DateTime(2024, 1, 15, 23, 30, 0);
        var lastRun = now.AddMinutes(-6); // 6 min ago, interval is 5

        var result = ScheduleHelper.TimeUntilNextRun(now, WindowStart, WindowEnd,
            TimeSpan.FromMinutes(5), lastRunAt: lastRun);

        result.Should().Be(TimeSpan.Zero);
    }

    [Fact]
    public void TimeUntilNextRun_InsideWindow_IntervalNotElapsed_ReturnsRemainder()
    {
        var now     = new DateTime(2024, 1, 15, 23, 30, 0);
        var lastRun = now.AddMinutes(-3); // 3 min ago, interval is 5

        var result = ScheduleHelper.TimeUntilNextRun(now, WindowStart, WindowEnd,
            TimeSpan.FromMinutes(5), lastRunAt: lastRun);

        result.Should().Be(TimeSpan.FromMinutes(2));
    }

    [Fact]
    public void TimeUntilNextRun_OutsideWindow_BeforeStartToday_ReturnsWaitUntilStart()
    {
        // 08:00, window starts at 23:00 → wait ~15 h
        var now      = new DateTime(2024, 1, 15, 8, 0, 0);
        var expected = new DateTime(2024, 1, 15, 23, 0, 0) - now;

        var result = ScheduleHelper.TimeUntilNextRun(now, WindowStart, WindowEnd,
            TimeSpan.FromMinutes(5), lastRunAt: null);

        result.Should().Be(expected);
    }

    [Fact]
    public void TimeUntilNextRun_OutsideWindow_AfterEnd_ReturnsWaitUntilTomorrowStart()
    {
        // 23:58, window ended at 23:55 → next run tomorrow at 23:00
        var now      = new DateTime(2024, 1, 15, 23, 58, 0);
        var expected = new DateTime(2024, 1, 16, 23, 0, 0) - now;

        var result = ScheduleHelper.TimeUntilNextRun(now, WindowStart, WindowEnd,
            TimeSpan.FromMinutes(5), lastRunAt: null);

        result.Should().Be(expected);
    }

    [Fact]
    public void TimeUntilNextRun_AtWindowStartBoundary_ReturnsZero()
    {
        var now = new DateTime(2024, 1, 15, 23, 0, 0); // exactly at start

        var result = ScheduleHelper.TimeUntilNextRun(now, WindowStart, WindowEnd,
            TimeSpan.FromMinutes(5), lastRunAt: null);

        result.Should().Be(TimeSpan.Zero);
    }

    [Fact]
    public void TimeUntilNextRun_AtWindowEndBoundary_ReturnsZero()
    {
        var now = new DateTime(2024, 1, 15, 23, 55, 0); // exactly at end

        var result = ScheduleHelper.TimeUntilNextRun(now, WindowStart, WindowEnd,
            TimeSpan.FromMinutes(5), lastRunAt: null);

        result.Should().Be(TimeSpan.Zero);
    }

    [Fact]
    public void TimeUntilNextRun_MidnightCrossingWindow_AfterMidnight_InWindow_ReturnsZero()
    {
        // Window 23:00–00:30, current time 00:15 (past midnight, still in window)
        var now   = new DateTime(2024, 1, 16, 0, 15, 0);
        var start = new TimeOnly(23, 0);
        var end   = new TimeOnly(0, 30);

        var result = ScheduleHelper.TimeUntilNextRun(now, start, end,
            TimeSpan.FromMinutes(5), lastRunAt: null);

        result.Should().Be(TimeSpan.Zero);
    }

    [Fact]
    public void TimeUntilNextRun_MidnightCrossingWindow_Midday_OutsideWindow()
    {
        // Window 23:00–00:30, current time 12:00 → wait until 23:00
        var now      = new DateTime(2024, 1, 15, 12, 0, 0);
        var start    = new TimeOnly(23, 0);
        var end      = new TimeOnly(0, 30);
        var expected = new DateTime(2024, 1, 15, 23, 0, 0) - now;

        var result = ScheduleHelper.TimeUntilNextRun(now, start, end,
            TimeSpan.FromMinutes(5), lastRunAt: null);

        result.Should().Be(expected);
    }

    // ── ParseTimeOnly ─────────────────────────────────────────────────

    [Theory]
    [InlineData("23:00", 23, 0)]
    [InlineData("00:00",  0, 0)]
    [InlineData("09:30",  9, 30)]
    [InlineData("9:30",   9, 30)]
    [InlineData("23:55", 23, 55)]
    public void ParseTimeOnly_ValidString_ReturnsParsedTime(string input, int hour, int minute)
        => ScheduleHelper.ParseTimeOnly(input).Should().Be(new TimeOnly(hour, minute));

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void ParseTimeOnly_EmptyOrWhitespace_ReturnsDefault(string input)
        => ScheduleHelper.ParseTimeOnly(input)
            .Should().Be(TimeOnly.Parse(DefaultConstants.ScheduleStartTime));

    [Fact]
    public void ParseTimeOnly_InvalidFormat_ReturnsDefault()
        => ScheduleHelper.ParseTimeOnly("not-a-time")
            .Should().Be(TimeOnly.Parse(DefaultConstants.ScheduleStartTime));

    [Fact]
    public void ParseTimeOnly_CustomDefault_UsedWhenInputEmpty()
        => ScheduleHelper.ParseTimeOnly("", "08:00")
            .Should().Be(new TimeOnly(8, 0));

    // ── SchedulesOverlap ──────────────────────────────────────────────

    [Fact]
    public void SchedulesOverlap_IdenticalRanges_ReturnsTrue()
        => ScheduleHelper.SchedulesOverlap(
            new TimeOnly(23, 0), new TimeOnly(23, 55),
            new TimeOnly(23, 0), new TimeOnly(23, 55))
           .Should().BeTrue();

    [Fact]
    public void SchedulesOverlap_PartialOverlap_ReturnsTrue()
        => ScheduleHelper.SchedulesOverlap(
            new TimeOnly(18, 0), new TimeOnly(22, 0),
            new TimeOnly(21, 0), new TimeOnly(23, 0))
           .Should().BeTrue();

    [Fact]
    public void SchedulesOverlap_NonOverlapping_SeparateWindows_ReturnsFalse()
        => ScheduleHelper.SchedulesOverlap(
            new TimeOnly(18, 0), new TimeOnly(22, 0),
            new TimeOnly(23, 0), new TimeOnly(23, 55))
           .Should().BeFalse();

    [Fact]
    public void SchedulesOverlap_AdjacentRanges_ReturnsFalse()
        // 18:00–22:00 and 22:01–23:00 — touch but don't share any minute
        => ScheduleHelper.SchedulesOverlap(
            new TimeOnly(18, 0), new TimeOnly(22, 0),
            new TimeOnly(22, 1), new TimeOnly(23, 0))
           .Should().BeFalse();

    [Fact]
    public void SchedulesOverlap_TouchingAtBoundary_ReturnsTrue()
        // 18:00–22:00 and 22:00–23:00 — share 22:00
        => ScheduleHelper.SchedulesOverlap(
            new TimeOnly(18, 0), new TimeOnly(22, 0),
            new TimeOnly(22, 0), new TimeOnly(23, 0))
           .Should().BeTrue();

    [Fact]
    public void SchedulesOverlap_BothCrossMidnight_ReturnsTrue()
        // Both cross midnight → both cover midnight
        => ScheduleHelper.SchedulesOverlap(
            new TimeOnly(23, 0), new TimeOnly(1, 0),
            new TimeOnly(22, 0), new TimeOnly(2, 0))
           .Should().BeTrue();

    [Fact]
    public void SchedulesOverlap_OneCrossesMidnight_OtherDoesNot_Overlapping_ReturnsTrue()
        // A = 23:00–01:00 (crosses midnight), B = 00:30–02:00 (normal, inside A's post-midnight part)
        => ScheduleHelper.SchedulesOverlap(
            new TimeOnly(23, 0), new TimeOnly(1, 0),
            new TimeOnly(0, 30), new TimeOnly(2, 0))
           .Should().BeTrue();

    [Fact]
    public void SchedulesOverlap_OneCrossesMidnight_OtherDoesNot_NonOverlapping_ReturnsFalse()
        // A = 23:00–01:00 (crosses midnight), B = 02:00–20:00 (normal, outside A entirely)
        => ScheduleHelper.SchedulesOverlap(
            new TimeOnly(23, 0), new TimeOnly(1, 0),
            new TimeOnly(2, 0), new TimeOnly(20, 0))
           .Should().BeFalse();
}
