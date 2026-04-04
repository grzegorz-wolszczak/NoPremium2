using NSubstitute;
using AwesomeAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Playwright;
using NoPremium2.Browser;
using System.Diagnostics;
using Xunit;

namespace NoPremium2.Tests.Browser;

public sealed class BrowserManagerTests
{
    private readonly AppSettings _settings = new();
    private readonly ICdpPortDiscovery _cdpDiscovery = Substitute.For<ICdpPortDiscovery>();
    private readonly IPortAllocator _portAllocator = Substitute.For<IPortAllocator>();
    private readonly IVivaldiLauncher _launcher = Substitute.For<IVivaldiLauncher>();
    private readonly IBrowserConnector _connector = Substitute.For<IBrowserConnector>();
    private readonly ILogger<BrowserManager> _logger = Substitute.For<ILogger<BrowserManager>>();

    private BrowserManager CreateSut() =>
        new(_settings, _cdpDiscovery, _portAllocator, _launcher, _connector, _logger);

    private static (IPlaywright, IBrowser, IPage) MakeConnectResult()
    {
        var playwright = Substitute.For<IPlaywright>();
        var browser = Substitute.For<IBrowser>();
        var page = Substitute.For<IPage>();
        return (playwright, browser, page);
    }

    [Fact]
    public async Task GetOrLaunchAsync_WhenExistingCdpFound_DoesNotLaunchVivaldi()
    {
        _cdpDiscovery.FindExistingPortAsync().Returns(9222);
        _connector.ConnectAsync(9222, Arg.Any<CancellationToken>()).Returns(MakeConnectResult());

        await CreateSut().GetOrLaunchAsync();

        _launcher.DidNotReceive().Launch(Arg.Any<int>(), Arg.Any<string>());
        _portAllocator.DidNotReceive().GetFreePort();
    }

    [Fact]
    public async Task GetOrLaunchAsync_WhenExistingCdpFound_ReturnIsOwnedFalse()
    {
        _cdpDiscovery.FindExistingPortAsync().Returns(9222);
        _connector.ConnectAsync(9222, Arg.Any<CancellationToken>()).Returns(MakeConnectResult());

        var session = await CreateSut().GetOrLaunchAsync();

        session.IsOwned.Should().BeFalse();
        session.OwnedProcess.Should().BeNull();
    }

    [Fact]
    public async Task GetOrLaunchAsync_WhenNoCdpFound_LaunchesVivaldi()
    {
        _cdpDiscovery.FindExistingPortAsync().Returns((int?)null);
        _portAllocator.GetFreePort().Returns(41769);
        _launcher.Launch(41769, Arg.Any<string>()).Returns((Process?)null);
        _connector.ConnectAsync(41769, Arg.Any<CancellationToken>()).Returns(MakeConnectResult());

        await CreateSut().GetOrLaunchAsync();

        _launcher.Received(1).Launch(41769, _settings.ProfileDir);
        await _launcher.Received(1).WaitForCdpAsync(41769, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetOrLaunchAsync_WhenNoCdpFound_ReturnIsOwnedTrue()
    {
        _cdpDiscovery.FindExistingPortAsync().Returns((int?)null);
        _portAllocator.GetFreePort().Returns(41769);
        _launcher.Launch(Arg.Any<int>(), Arg.Any<string>()).Returns((Process?)null);
        _connector.ConnectAsync(Arg.Any<int>(), Arg.Any<CancellationToken>()).Returns(MakeConnectResult());

        var session = await CreateSut().GetOrLaunchAsync();

        session.IsOwned.Should().BeTrue();
    }

    [Fact]
    public async Task GetOrLaunchAsync_WhenExistingCdpFound_ConnectsToCorrectPort()
    {
        _cdpDiscovery.FindExistingPortAsync().Returns(8888);
        _connector.ConnectAsync(8888, Arg.Any<CancellationToken>()).Returns(MakeConnectResult());

        await CreateSut().GetOrLaunchAsync();

        await _connector.Received(1).ConnectAsync(8888, Arg.Any<CancellationToken>());
    }
}
