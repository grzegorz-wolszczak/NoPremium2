using AwesomeAssertions;
using Microsoft.Extensions.Logging;
using NoPremium2.Browser;
using NSubstitute;
using Xunit;

namespace NoPremium2.Tests.Browser;

public sealed class ExistingBrowserResolverTests
{
    private const string Profile = "/home/test/.config/vivaldi-nopremium";

    private readonly IDevToolsActivePortReader _dtap = Substitute.For<IDevToolsActivePortReader>();
    private readonly ICdpPortDiscovery _procScan = Substitute.For<ICdpPortDiscovery>();
    private readonly IProfileLockInspector _lock = Substitute.For<IProfileLockInspector>();
    private readonly ICdpChecker _checker = Substitute.For<ICdpChecker>();

    private ExistingBrowserResolver CreateSut() =>
        new(_dtap, _procScan, _lock, _checker, Substitute.For<ILogger<ExistingBrowserResolver>>());

    public ExistingBrowserResolverTests()
    {
        _procScan.FindExistingPortAsync(Profile).Returns((int?)null);
        _lock.Inspect(Profile).Returns(new ProfileLockResult(ProfileLockStatus.NoLock, null));
    }

    [Fact]
    public async Task DevToolsPortResponds_ReturnsFound()
    {
        _dtap.Read(Profile).Returns(new DevToolsActivePortInfo(44523, null));
        _checker.IsRespondingAsync(44523, Arg.Any<CancellationToken>()).Returns(true);

        (await CreateSut().ResolveAsync(Profile))
            .Should().BeOfType<ExistingBrowserResult.Found>()
            .Which.Port.Should().Be(44523);
    }

    [Fact]
    public async Task DevToolsPortProbeRetriesThenSucceeds_ReturnsFound()
    {
        _dtap.Read(Profile).Returns(new DevToolsActivePortInfo(44523, null));
        _checker.IsRespondingAsync(44523, Arg.Any<CancellationToken>()).Returns(false, false, true);

        (await CreateSut().ResolveAsync(Profile))
            .Should().BeOfType<ExistingBrowserResult.Found>();
    }

    [Fact]
    public async Task StaleDevToolsPort_LockOwnerDead_ReturnsNotRunning()
    {
        _dtap.Read(Profile).Returns(new DevToolsActivePortInfo(44523, null));
        _checker.IsRespondingAsync(Arg.Any<int>(), Arg.Any<CancellationToken>()).Returns(false);
        _lock.Inspect(Profile).Returns(new ProfileLockResult(ProfileLockStatus.OwnerDead, null));

        (await CreateSut().ResolveAsync(Profile))
            .Should().BeOfType<ExistingBrowserResult.NotRunning>();
    }

    [Fact]
    public async Task StaleDevToolsPort_LockOwnerAlive_ReturnsRunningButUnreachable()
    {
        _dtap.Read(Profile).Returns(new DevToolsActivePortInfo(44523, null));
        _checker.IsRespondingAsync(Arg.Any<int>(), Arg.Any<CancellationToken>()).Returns(false);
        _lock.Inspect(Profile).Returns(new ProfileLockResult(ProfileLockStatus.OwnerAlive, 777));

        (await CreateSut().ResolveAsync(Profile))
            .Should().BeOfType<ExistingBrowserResult.RunningButUnreachable>()
            .Which.Pid.Should().Be(777);
    }

    [Fact]
    public async Task NoDevToolsFile_ProcScanFindsPort_ReturnsFound()
    {
        _dtap.Read(Profile).Returns((DevToolsActivePortInfo?)null);
        _procScan.FindExistingPortAsync(Profile).Returns(8888);

        (await CreateSut().ResolveAsync(Profile))
            .Should().BeOfType<ExistingBrowserResult.Found>()
            .Which.Port.Should().Be(8888);
    }

    [Fact]
    public async Task NoSignalsAtAll_ReturnsNotRunning()
    {
        _dtap.Read(Profile).Returns((DevToolsActivePortInfo?)null);

        (await CreateSut().ResolveAsync(Profile))
            .Should().BeOfType<ExistingBrowserResult.NotRunning>();
    }

    [Fact]
    public async Task NoPort_LockOwnerAlive_ReturnsRunningButUnreachableWithPid()
    {
        _dtap.Read(Profile).Returns((DevToolsActivePortInfo?)null);
        _lock.Inspect(Profile).Returns(new ProfileLockResult(ProfileLockStatus.OwnerAlive, 4321));

        (await CreateSut().ResolveAsync(Profile))
            .Should().BeOfType<ExistingBrowserResult.RunningButUnreachable>()
            .Which.Pid.Should().Be(4321);
    }
}
