using NSubstitute;
using AwesomeAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Playwright;
using NoPremium2;
using NoPremium2.Browser;
using System.Diagnostics;
using Xunit;

namespace NoPremium2.Tests.Browser;

public sealed class BrowserManagerTests
{
    private const string Profile = "/home/test/.config/vivaldi-nopremium";

    private readonly AppSettings _settings = new() { ProfileDir = Profile };
    private readonly IExistingBrowserResolver _resolver = Substitute.For<IExistingBrowserResolver>();
    private readonly IStaleBrowserKiller _killer = Substitute.For<IStaleBrowserKiller>();
    private readonly IPortAllocator _portAllocator = Substitute.For<IPortAllocator>();
    private readonly IVivaldiLauncher _launcher = Substitute.For<IVivaldiLauncher>();
    private readonly IBrowserConnector _connector = Substitute.For<IBrowserConnector>();
    private readonly ILogger<BrowserManager> _logger = Substitute.For<ILogger<BrowserManager>>();

    public BrowserManagerTests()
    {
        _connector.ConnectAsync(Arg.Any<int>(), Arg.Any<CancellationToken>()).Returns(MakeConnectResult());
    }

    private BrowserManager CreateSut() =>
        new(_settings, _resolver, _killer, _portAllocator, _launcher, _connector, _logger);

    private static (IPlaywright, IBrowser, IPage) MakeConnectResult() =>
        (Substitute.For<IPlaywright>(), Substitute.For<IBrowser>(), Substitute.For<IPage>());

    private void Resolves(ExistingBrowserResult result) =>
        _resolver.ResolveAsync(Profile, Arg.Any<CancellationToken>()).Returns(result);

    [Fact]
    public async Task GetOrLaunchAsync_WhenFound_ConnectsWithoutLaunching()
    {
        Resolves(new ExistingBrowserResult.Found(9222));

        var session = await CreateSut().GetOrLaunchAsync();

        _launcher.DidNotReceive().Launch(Arg.Any<int>(), Arg.Any<string>());
        _portAllocator.DidNotReceive().GetFreePort();
        await _connector.Received(1).ConnectAsync(9222, Arg.Any<CancellationToken>());
        session.IsOwned.Should().BeFalse();
        session.OwnedProcess.Should().BeNull();
    }

    [Fact]
    public async Task GetOrLaunchAsync_WhenNotRunning_AllocatesLaunchesWaits()
    {
        Resolves(new ExistingBrowserResult.NotRunning());
        _portAllocator.GetFreePort().Returns(41769);
        _launcher.Launch(41769, Profile).Returns((Process?)null);

        var session = await CreateSut().GetOrLaunchAsync();

        _launcher.Received(1).Launch(41769, Profile);
        await _launcher.Received(1).WaitForCdpAsync(41769, Profile, Arg.Any<Process?>(), Arg.Any<CancellationToken>());
        await _connector.Received(1).ConnectAsync(41769, Arg.Any<CancellationToken>());
        session.IsOwned.Should().BeTrue();
    }

    [Fact]
    public async Task GetOrLaunchAsync_WhenRunningButUnreachable_AndKillDisabled_ThrowsAndNeverLaunches()
    {
        // _settings has KillStaleBrowser=false by default
        Resolves(new ExistingBrowserResult.RunningButUnreachable(4321));

        var act = () => CreateSut().GetOrLaunchAsync();

        (await act.Should().ThrowAsync<InvalidOperationException>())
            .Which.Message.Should().Contain(Profile).And.Contain("4321");
        _launcher.DidNotReceive().Launch(Arg.Any<int>(), Arg.Any<string>());
        _killer.DidNotReceive().Kill(Arg.Any<int>(), Arg.Any<string>());
    }

    [Fact]
    public async Task GetOrLaunchAsync_WhenRunningButUnreachable_AndKillEnabled_KillsThenLaunches()
    {
        var settings = new AppSettings { ProfileDir = Profile, KillStaleBrowser = true };
        _resolver.ResolveAsync(Profile, Arg.Any<CancellationToken>())
            .Returns(new ExistingBrowserResult.RunningButUnreachable(4321));
        _portAllocator.GetFreePort().Returns(50000);
        _launcher.Launch(50000, Profile).Returns((Process?)null);

        var sut = new BrowserManager(settings, _resolver, _killer, _portAllocator, _launcher, _connector, _logger);
        var session = await sut.GetOrLaunchAsync();

        _killer.Received(1).Kill(4321, Profile);
        _launcher.Received(1).Launch(50000, Profile);
        session.IsOwned.Should().BeTrue();
    }
}
