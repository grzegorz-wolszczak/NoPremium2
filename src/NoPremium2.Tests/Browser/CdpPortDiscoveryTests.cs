using NSubstitute;
using AwesomeAssertions;
using Microsoft.Extensions.Logging;
using NoPremium2.Browser;
using Xunit;

namespace NoPremium2.Tests.Browser;

public sealed class CdpPortDiscoveryTests
{
    private const string Profile = "/home/test/.config/vivaldi-nopremium";
    private static string CmdlineFor(int port, string profile) =>
        $"vivaldi-bin\0--remote-debugging-port={port}\0--user-data-dir={profile}\0";

    // ── ParsePort — pure function ─────────────────────────────────────

    [Theory]
    [InlineData("vivaldi\0--remote-debugging-port=9222\0--other", 9222)]
    [InlineData("vivaldi\0--remote-debugging-port=41769\0", 41769)]
    [InlineData("vivaldi\0--remote-debugging-port=1\0", 1)]
    public void ParsePort_ValidCmdline_ReturnsPort(string cmdline, int expected)
    {
        CdpPortDiscovery.ParsePort(cmdline).Should().Be(expected);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("vivaldi\0--no-sandbox\0")]
    [InlineData("vivaldi\0--remote-debugging-port=0\0")]
    [InlineData("vivaldi\0--remote-debugging-port=abc\0")]
    public void ParsePort_InvalidCmdline_ReturnsNull(string? cmdline)
    {
        CdpPortDiscovery.ParsePort(cmdline).Should().BeNull();
    }

    // ── ParseUserDataDir ─────────────────────────────────────────────

    [Fact]
    public void ParseUserDataDir_ValidCmdline_ReturnsDir()
    {
        CdpPortDiscovery.ParseUserDataDir("vivaldi\0--user-data-dir=/home/x/.config/vivaldi-nopremium\0--foo")
            .Should().Be("/home/x/.config/vivaldi-nopremium");
    }

    [Fact]
    public void ParseUserDataDir_QuotedValue_Unquoted()
    {
        CdpPortDiscovery.ParseUserDataDir("vivaldi\0--user-data-dir=\"/home/x/p\"\0")
            .Should().Be("/home/x/p");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("vivaldi\0--remote-debugging-port=9222\0")]
    public void ParseUserDataDir_Missing_ReturnsNull(string? cmdline)
    {
        CdpPortDiscovery.ParseUserDataDir(cmdline).Should().BeNull();
    }

    [Fact]
    public void CmdlineMatchesProfile_TrailingSlashDifference_StillMatches()
    {
        CdpPortDiscovery.CmdlineMatchesProfile(
            "v\0--user-data-dir=/home/x/p/\0", "/home/x/p").Should().BeTrue();
    }

    // ── FindExistingPortAsync ────────────────────────────────────────

    [Fact]
    public async Task FindExistingPortAsync_MatchingProfileAndResponding_ReturnsPort()
    {
        var reader = Substitute.For<IProcessCmdlineReader>();
        reader.GetAll().Returns(new[] { (Pid: 1234, Cmdline: (string?)CmdlineFor(9222, Profile)) });

        var checker = Substitute.For<ICdpChecker>();
        checker.IsRespondingAsync(9222, Arg.Any<CancellationToken>()).Returns(true);

        var sut = new CdpPortDiscovery(reader, checker, Substitute.For<ILogger<CdpPortDiscovery>>());

        (await sut.FindExistingPortAsync(Profile)).Should().Be(9222);
    }

    [Fact]
    public async Task FindExistingPortAsync_ProcessOnDifferentProfile_Ignored()
    {
        var reader = Substitute.For<IProcessCmdlineReader>();
        reader.GetAll().Returns(new[] { (Pid: 1234, Cmdline: (string?)CmdlineFor(9222, "/home/test/.config/vivaldi")) });

        var checker = Substitute.For<ICdpChecker>();
        checker.IsRespondingAsync(Arg.Any<int>(), Arg.Any<CancellationToken>()).Returns(true);

        var sut = new CdpPortDiscovery(reader, checker, Substitute.For<ILogger<CdpPortDiscovery>>());

        (await sut.FindExistingPortAsync(Profile)).Should().BeNull();
        await checker.DidNotReceive().IsRespondingAsync(Arg.Any<int>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task FindExistingPortAsync_MatchingProfileButCdpNotResponding_ReturnsNull()
    {
        var reader = Substitute.For<IProcessCmdlineReader>();
        reader.GetAll().Returns(new[] { (Pid: 1234, Cmdline: (string?)CmdlineFor(9222, Profile)) });

        var checker = Substitute.For<ICdpChecker>();
        checker.IsRespondingAsync(9222, Arg.Any<CancellationToken>()).Returns(false);

        var sut = new CdpPortDiscovery(reader, checker, Substitute.For<ILogger<CdpPortDiscovery>>());

        (await sut.FindExistingPortAsync(Profile)).Should().BeNull();
    }

    [Fact]
    public async Task FindExistingPortAsync_NoProcessesWithCdpPort_ReturnsNull()
    {
        var reader = Substitute.For<IProcessCmdlineReader>();
        reader.GetAll().Returns(new[] { (Pid: 1234, Cmdline: (string?)"some-process\0--no-sandbox\0") });

        var sut = new CdpPortDiscovery(reader, Substitute.For<ICdpChecker>(), Substitute.For<ILogger<CdpPortDiscovery>>());

        (await sut.FindExistingPortAsync(Profile)).Should().BeNull();
    }
}
