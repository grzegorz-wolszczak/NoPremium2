using NSubstitute;
using AwesomeAssertions;
using Microsoft.Extensions.Logging;
using NoPremium2.Browser;
using Xunit;

namespace NoPremium2.Tests.Browser;

public sealed class CdpPortDiscoveryTests
{
    // ParsePort tests — pure function, no mocks needed

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

    [Fact]
    public async Task FindExistingPortAsync_WhenVivaldiRunningWithCdp_ReturnsPort()
    {
        var reader = Substitute.For<IProcessCmdlineReader>();
        reader.GetByName("vivaldi").Returns(new[] { (Pid: 1234, Cmdline: (string?)"vivaldi\0--remote-debugging-port=9222\0") });

        var checker = Substitute.For<ICdpChecker>();
        checker.IsRespondingAsync(9222).Returns(true);

        var sut = new CdpPortDiscovery(reader, checker, Substitute.For<ILogger<CdpPortDiscovery>>());

        var result = await sut.FindExistingPortAsync();

        result.Should().Be(9222);
    }

    [Fact]
    public async Task FindExistingPortAsync_WhenCdpNotResponding_ReturnsNull()
    {
        var reader = Substitute.For<IProcessCmdlineReader>();
        reader.GetByName("vivaldi").Returns(new[] { (Pid: 1234, Cmdline: (string?)"vivaldi\0--remote-debugging-port=9222\0") });

        var checker = Substitute.For<ICdpChecker>();
        checker.IsRespondingAsync(9222).Returns(false);

        var sut = new CdpPortDiscovery(reader, checker, Substitute.For<ILogger<CdpPortDiscovery>>());

        var result = await sut.FindExistingPortAsync();

        result.Should().BeNull();
    }

    [Fact]
    public async Task FindExistingPortAsync_WhenNoVivaldiProcesses_ReturnsNull()
    {
        var reader = Substitute.For<IProcessCmdlineReader>();
        reader.GetByName("vivaldi").Returns(Array.Empty<(int, string?)>());

        var sut = new CdpPortDiscovery(reader, Substitute.For<ICdpChecker>(), Substitute.For<ILogger<CdpPortDiscovery>>());

        var result = await sut.FindExistingPortAsync();

        result.Should().BeNull();
    }
}
