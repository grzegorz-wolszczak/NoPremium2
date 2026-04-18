using AwesomeAssertions;
using NoPremium2.Config;
using Xunit;

namespace NoPremium2.Tests.Config;

public sealed class ConfigLoaderTests
{
    // ── ParseImapServer ───────────────────────────────────────────────
    //
    // [Theory]
    // [InlineData("imap.gmx.com:993",       "imap.gmx.com",     993)]
    // [InlineData("mail.example.com:143",   "mail.example.com", 143)]
    // [InlineData("imap.host.org:465",      "imap.host.org",    465)]
    // [InlineData("host:1",                 "host",               1)]
    // public void ParseImapServer_ValidFormat_ReturnsHostAndPort(
    //     string input, string expectedHost, int expectedPort)
    // {
    //     var (host, port) = ConfigLoader.ParseImapServer(input);
    //
    //     host.Should().Be(expectedHost);
    //     port.Should().Be(expectedPort);
    // }
    //
    // [Fact]
    // public void ParseImapServer_HostWithColonInPort_TakesFirstColon()
    // {
    //     // only split on first colon — "imap.host.com:993" host part must not include port
    //     var (host, port) = ConfigLoader.ParseImapServer("imap.host.com:993");
    //
    //     host.Should().Be("imap.host.com");
    //     port.Should().Be(993);
    // }

    // ── ApplyDefaults ─────────────────────────────────────────────────

    private static BaseConfig MinimalConfig(
        string keepalive = "01:00:00",
        string tcStart = "23:00", string tcEnd = "23:55", int tcInterval = 5, long tcReserve = 1_000_000,
        string vcStart = "23:00", string vcEnd = "23:55", int vcInterval = 5) =>
        new BaseConfig
        {
            NoPremiumUsername    = "u",
            NoPremiumPassword    = "p",
            EmailUsername        = "e",
            EmailPassword        = "ep",
            EmailImapServer      = "imap.example.com:993",
            LinksFilePath        = "links.json",
            KeepaliveInterval    = keepalive,
            TransferConsumer     = new TransferConsumerConfig
                { StartTime = tcStart, EndTime = tcEnd, IntervalMinutes = tcInterval, ReserveTransferBytes = tcReserve },
            VoucherConsumer      = new VoucherConsumerConfig
                { StartTime = vcStart, EndTime = vcEnd, IntervalMinutes = vcInterval },
        };

    [Fact]
    public void ApplyDefaults_PopulatedOptionalFields_PreservesAllValues()
    {
        var config = MinimalConfig(
            keepalive: "02:00:00",
            tcStart: "22:00", tcEnd: "22:50", tcInterval: 10, tcReserve: 5_000_000,
            vcStart: "04:00", vcEnd: "04:30", vcInterval: 15);

        var result = ConfigLoader.ApplyDefaults(config);

        result.KeepaliveInterval.Should().Be("02:00:00");
        result.TransferConsumer.StartTime.Should().Be("22:00");
        result.TransferConsumer.EndTime.Should().Be("22:50");
        result.TransferConsumer.IntervalMinutes.Should().Be(10);
        result.TransferConsumer.ReserveTransferBytes.Should().Be(5_000_000);
        result.VoucherConsumer.StartTime.Should().Be("04:00");
        result.VoucherConsumer.EndTime.Should().Be("04:30");
        result.VoucherConsumer.IntervalMinutes.Should().Be(15);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void ApplyDefaults_EmptyKeepalive_UsesDefault(string? value)
    {
        var config = MinimalConfig(keepalive: value!);

        var result = ConfigLoader.ApplyDefaults(config);

        result.KeepaliveInterval.Should().Be(DefaultConstants.KeepaliveInterval);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void ApplyDefaults_EmptyTransferConsumerStartTime_UsesDefault(string? value)
    {
        var config = MinimalConfig(tcStart: value!);

        var result = ConfigLoader.ApplyDefaults(config);

        result.TransferConsumer.StartTime.Should().Be(DefaultConstants.ScheduleStartTime);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void ApplyDefaults_EmptyTransferConsumerEndTime_UsesDefault(string? value)
    {
        var config = MinimalConfig(tcEnd: value!);

        var result = ConfigLoader.ApplyDefaults(config);

        result.TransferConsumer.EndTime.Should().Be(DefaultConstants.ScheduleEndTime);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-100)]
    public void ApplyDefaults_ZeroOrNegativeTransferInterval_UsesDefault(int value)
    {
        var config = MinimalConfig(tcInterval: value);

        var result = ConfigLoader.ApplyDefaults(config);

        result.TransferConsumer.IntervalMinutes.Should().Be(DefaultConstants.ScheduleIntervalMinutes);
    }

    [Theory]
    [InlineData(0L)]
    [InlineData(-1L)]
    public void ApplyDefaults_ZeroOrNegativeReserveTransferBytes_UsesDefault(long value)
    {
        var config = MinimalConfig(tcReserve: value);

        var result = ConfigLoader.ApplyDefaults(config);

        result.TransferConsumer.ReserveTransferBytes.Should().Be(DefaultConstants.ReserveTransferBytes);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void ApplyDefaults_EmptyVoucherConsumerStartTime_UsesDefault(string? value)
    {
        var config = MinimalConfig(vcStart: value!);

        var result = ConfigLoader.ApplyDefaults(config);

        result.VoucherConsumer.StartTime.Should().Be(DefaultConstants.ScheduleStartTime);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void ApplyDefaults_EmptyVoucherConsumerEndTime_UsesDefault(string? value)
    {
        var config = MinimalConfig(vcEnd: value!);

        var result = ConfigLoader.ApplyDefaults(config);

        result.VoucherConsumer.EndTime.Should().Be(DefaultConstants.ScheduleEndTime);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public void ApplyDefaults_ZeroOrNegativeVoucherInterval_UsesDefault(int value)
    {
        var config = MinimalConfig(vcInterval: value);

        var result = ConfigLoader.ApplyDefaults(config);

        result.VoucherConsumer.IntervalMinutes.Should().Be(DefaultConstants.ScheduleIntervalMinutes);
    }

    [Fact]
    public void ApplyDefaults_DoesNotModifyRequiredFields()
    {
        var config = MinimalConfig();

        var result = ConfigLoader.ApplyDefaults(config);

        result.NoPremiumUsername.Should().Be("u");
        result.NoPremiumPassword.Should().Be("p");
        result.EmailUsername.Should().Be("e");
        result.EmailPassword.Should().Be("ep");
        result.LinksFilePath.Should().Be("links.json");
    }

    // ── NullOrEmpty ───────────────────────────────────────────────────

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void NullOrEmpty_WhenNullOrWhitespace_ReturnsTrue(string? value)
        => ConfigLoader.NullOrEmpty(value).Should().BeTrue();

    [Theory]
    [InlineData("a")]
    [InlineData("hello")]
    [InlineData(" x ")]
    public void NullOrEmpty_WhenNonEmpty_ReturnsFalse(string value)
        => ConfigLoader.NullOrEmpty(value).Should().BeFalse();
}