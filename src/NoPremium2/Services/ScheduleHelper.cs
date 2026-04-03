using NoPremium2.Config;

namespace NoPremium2.Services;

internal static class ScheduleHelper
{
    /// <summary>
    /// Given the current time and a schedule config, returns when the next run should happen.
    /// Returns TimeSpan.Zero if it should run immediately (within the window).
    /// Returns a positive TimeSpan if it needs to wait (either within window but interval not elapsed,
    /// or outside window waiting for next day's start).
    /// </summary>
    public static TimeSpan TimeUntilNextRun(
        DateTime now,
        TimeOnly startTime,
        TimeOnly endTime,
        TimeSpan interval,
        DateTime? lastRunAt)
    {
        var currentTime = TimeOnly.FromDateTime(now);

        bool inWindow = IsInWindow(currentTime, startTime, endTime);

        if (!inWindow)
        {
            // Outside schedule window — wait until tomorrow's start
            return TimeUntilTomorrow(now, startTime);
        }

        // Inside window
        if (lastRunAt is null)
            return TimeSpan.Zero; // Never run — start immediately

        var sinceLastRun = now - lastRunAt.Value;
        if (sinceLastRun >= interval)
            return TimeSpan.Zero; // Interval elapsed — run now

        // Still within interval — wait for the remainder
        return interval - sinceLastRun;
    }

    private static bool IsInWindow(TimeOnly current, TimeOnly start, TimeOnly end)
    {
        // Handles both normal (23:00–23:55) and midnight-crossing (23:00–00:30) windows
        if (start <= end)
            return current >= start && current <= end;
        else
            return current >= start || current <= end;
    }

    private static TimeSpan TimeUntilTomorrow(DateTime now, TimeOnly targetTime)
    {
        var todayTarget = now.Date.Add(targetTime.ToTimeSpan());

        if (todayTarget > now)
            return todayTarget - now;

        // Target already passed today — next occurrence is tomorrow
        return todayTarget.AddDays(1) - now;
    }

    /// <summary>
    /// Returns true if two time-of-day ranges share any time. Handles midnight-crossing ranges
    /// (e.g. 23:00–01:00 where start &gt; end).
    /// </summary>
    public static bool SchedulesOverlap(TimeOnly s1, TimeOnly e1, TimeOnly s2, TimeOnly e2)
    {
        bool aCrosses = s1 > e1;
        bool bCrosses = s2 > e2;

        if (!aCrosses && !bCrosses)
            // Both normal ranges: overlap iff [s1,e1] ∩ [s2,e2] is non-empty
            return s1 <= e2 && s2 <= e1;

        if (aCrosses && bCrosses)
            // Both cross midnight → both include midnight
            return true;

        if (aCrosses)
            // A covers [s1,24h) ∪ [0,e1]; B covers [s2,e2]
            return e2 >= s1 || s2 <= e1;

        // B crosses, A does not — symmetric
        return e1 >= s2 || s1 <= e2;
    }

    public static TimeOnly ParseTimeOnly(string timeStr, string defaultValue = DefaultConstants.ScheduleStartTime)
    {
        if (string.IsNullOrWhiteSpace(timeStr))
            timeStr = defaultValue;

        if (TimeOnly.TryParseExact(timeStr, "HH:mm", out var result))
            return result;
        if (TimeOnly.TryParseExact(timeStr, "H:mm", out result))
            return result;

        return TimeOnly.Parse(defaultValue);
    }
}
