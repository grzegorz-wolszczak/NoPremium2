using System.Diagnostics;
using AwesomeAssertions;
using Microsoft.Extensions.Logging;
using NoPremium2.Browser;
using NSubstitute;
using Xunit;

namespace NoPremium2.Tests.Browser;

public sealed class CdpWaiterTests
{
    private const string Profile = "/home/test/.config/vivaldi-nopremium";
    private const int Port = 44523;

    private readonly ICdpChecker _checker = Substitute.For<ICdpChecker>();
    private readonly IDevToolsActivePortReader _dtap = Substitute.For<IDevToolsActivePortReader>();
    private readonly ILogger _logger = Substitute.For<ILogger>();

    private Task Wait(int timeoutMs, Process? launched = null) =>
        CdpWaiter.WaitAsync(Port, Profile, launched, _checker, _dtap, timeoutMs,
            TimeSpan.FromMilliseconds(2000), _logger, CancellationToken.None);

    [Fact]
    public async Task CdpRespondsQuickly_Returns()
    {
        _checker.IsRespondingAsync(Port, Arg.Any<CancellationToken>()).Returns(true);

        await Wait(timeoutMs: 10_000); // must not throw
    }

    [Fact]
    public async Task DevToolsActivePortShowsDifferentPort_ThrowsHandoffFast()
    {
        _checker.IsRespondingAsync(Port, Arg.Any<CancellationToken>()).Returns(false);
        _dtap.Read(Profile).Returns(new DevToolsActivePortInfo(59999, null));

        var sw = Stopwatch.StartNew();
        var act = () => Wait(timeoutMs: 10_000);

        (await act.Should().ThrowAsync<InvalidOperationException>())
            .Which.Message.Should().Contain("59999");
        sw.Elapsed.Should().BeLessThan(TimeSpan.FromSeconds(3));
    }

    [Fact]
    public async Task NeverResponds_NoHandoffSignal_ThrowsTimeoutAtDeadline()
    {
        _checker.IsRespondingAsync(Port, Arg.Any<CancellationToken>()).Returns(false);
        _dtap.Read(Profile).Returns((DevToolsActivePortInfo?)null);

        var act = () => Wait(timeoutMs: 1200);

        await act.Should().ThrowAsync<TimeoutException>();
    }
}
