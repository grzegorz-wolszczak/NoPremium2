using AwesomeAssertions;
using NoPremium2.Infrastructure;
using Xunit;

namespace NoPremium2.Tests.Infrastructure;

public sealed class LogFileHelperTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());

    public LogFileHelperTests() => Directory.CreateDirectory(_dir);
    public void Dispose() => Directory.Delete(_dir, recursive: true);

    private void Touch(string fileName) =>
        File.WriteAllText(Path.Combine(_dir, fileName), "");

    // ── ResolveLogFilePath ────────────────────────────────────────────

    [Fact]
    public void ResolveLogFilePath_NoExistingFiles_Returns01()
    {
        var result = LogFileHelper.ResolveLogFilePath(_dir, new DateTime(2026, 4, 1));

        result.Should().Be(Path.Combine(_dir, "logs_20260401.01.log"));
    }

    [Fact]
    public void ResolveLogFilePath_OneExistingFileToday_Returns02()
    {
        Touch("logs_20260401.01.log");

        var result = LogFileHelper.ResolveLogFilePath(_dir, new DateTime(2026, 4, 1));

        result.Should().Be(Path.Combine(_dir, "logs_20260401.02.log"));
    }

    [Fact]
    public void ResolveLogFilePath_TwoExistingFilesToday_Returns03()
    {
        Touch("logs_20260401.01.log");
        Touch("logs_20260401.02.log");

        var result = LogFileHelper.ResolveLogFilePath(_dir, new DateTime(2026, 4, 1));

        result.Should().Be(Path.Combine(_dir, "logs_20260401.03.log"));
    }

    [Fact]
    public void ResolveLogFilePath_OnlyFilesFromOtherDay_Returns01()
    {
        Touch("logs_20260331.01.log"); // yesterday

        var result = LogFileHelper.ResolveLogFilePath(_dir, new DateTime(2026, 4, 1));

        result.Should().Be(Path.Combine(_dir, "logs_20260401.01.log"));
    }

    [Fact]
    public void ResolveLogFilePath_RunNumberIsTwoDigitZeroPadded()
    {
        for (int i = 1; i <= 9; i++)
            Touch($"logs_20260401.{i:D2}.log");

        var result = LogFileHelper.ResolveLogFilePath(_dir, new DateTime(2026, 4, 1));

        result.Should().Be(Path.Combine(_dir, "logs_20260401.10.log"));
    }

    [Fact]
    public void ResolveLogFilePath_DateInNameMatchesNow()
    {
        var now = new DateTime(2025, 12, 31);

        var result = LogFileHelper.ResolveLogFilePath(_dir, now);

        Path.GetFileName(result).Should().StartWith("logs_20251231.");
    }

    // ── DeleteOldLogs ─────────────────────────────────────────────────

    [Fact]
    public void DeleteOldLogs_FileOlderThanRetention_IsDeleted()
    {
        Touch("logs_20260101.01.log"); // 90 days before 2026-04-01
        var now = new DateTime(2026, 4, 1);

        LogFileHelper.DeleteOldLogs(_dir, now, retentionDays: 30);

        File.Exists(Path.Combine(_dir, "logs_20260101.01.log")).Should().BeFalse();
    }

    [Fact]
    public void DeleteOldLogs_RecentFile_IsKept()
    {
        Touch("logs_20260328.01.log"); // 4 days before 2026-04-01
        var now = new DateTime(2026, 4, 1);

        LogFileHelper.DeleteOldLogs(_dir, now, retentionDays: 30);

        File.Exists(Path.Combine(_dir, "logs_20260328.01.log")).Should().BeTrue();
    }

    [Fact]
    public void DeleteOldLogs_FileExactlyAtCutoffBoundary_IsKept()
    {
        // cutoff = now - 30 days; file at exactly that date should be kept (not strictly less than)
        var now = new DateTime(2026, 4, 1);
        var cutoffDate = now.AddDays(-30); // 2026-03-02
        Touch($"logs_{cutoffDate:yyyyMMdd}.01.log");

        LogFileHelper.DeleteOldLogs(_dir, now, retentionDays: 30);

        File.Exists(Path.Combine(_dir, $"logs_{cutoffDate:yyyyMMdd}.01.log")).Should().BeTrue();
    }

    [Fact]
    public void DeleteOldLogs_FileOneDayBeforeCutoff_IsDeleted()
    {
        var now = new DateTime(2026, 4, 1);
        var justBefore = now.AddDays(-31); // 2026-03-01
        Touch($"logs_{justBefore:yyyyMMdd}.01.log");

        LogFileHelper.DeleteOldLogs(_dir, now, retentionDays: 30);

        File.Exists(Path.Combine(_dir, $"logs_{justBefore:yyyyMMdd}.01.log")).Should().BeFalse();
    }

    [Fact]
    public void DeleteOldLogs_FileWithUnparsableDateInName_IsIgnored()
    {
        Touch("logs_invalid.01.log");
        var now = new DateTime(2026, 4, 1);

        var act = () => LogFileHelper.DeleteOldLogs(_dir, now, retentionDays: 30);

        act.Should().NotThrow();
        File.Exists(Path.Combine(_dir, "logs_invalid.01.log")).Should().BeTrue();
    }

    [Fact]
    public void DeleteOldLogs_MultipleOldAndRecentFiles_DeletesOnlyOld()
    {
        Touch("logs_20260101.01.log"); // old
        Touch("logs_20260101.02.log"); // old
        Touch("logs_20260328.01.log"); // recent
        var now = new DateTime(2026, 4, 1);

        LogFileHelper.DeleteOldLogs(_dir, now, retentionDays: 30);

        File.Exists(Path.Combine(_dir, "logs_20260101.01.log")).Should().BeFalse();
        File.Exists(Path.Combine(_dir, "logs_20260101.02.log")).Should().BeFalse();
        File.Exists(Path.Combine(_dir, "logs_20260328.01.log")).Should().BeTrue();
    }
}
