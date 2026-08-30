using AwesomeAssertions;
using Microsoft.Extensions.Logging;
using NoPremium2.Browser;
using NSubstitute;
using Xunit;

namespace NoPremium2.Tests.Browser;

public sealed class ProfileLockInspectorTests
{
    private const string Profile = "/home/test/.config/vivaldi-nopremium";

    private readonly ISingletonLockReader _lock = Substitute.For<ISingletonLockReader>();
    private readonly IProcessCmdlineReader _procs = Substitute.For<IProcessCmdlineReader>();

    private ProfileLockInspector CreateSut() =>
        new(_lock, _procs, Substitute.For<ILogger<ProfileLockInspector>>());

    private static string Cmdline(string profile) =>
        $"vivaldi-bin\0--user-data-dir={profile}\0";

    // ── ParseSingletonLockTarget ─────────────────────────────────────

    [Fact]
    public void ParseSingletonLockTarget_HostWithDashes_SplitsOnLastDash()
    {
        ProfileLockInspector.ParseSingletonLockTarget("my-host-name-21898")
            .Should().Be(("my-host-name", 21898));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("nohostorpid")]
    [InlineData("host-")]
    [InlineData("host-abc")]
    [InlineData("-123")]
    public void ParseSingletonLockTarget_Garbage_ReturnsNull(string? target)
    {
        ProfileLockInspector.ParseSingletonLockTarget(target).Should().BeNull();
    }

    // ── Inspect ──────────────────────────────────────────────────────

    [Fact]
    public void Inspect_NoLockFile_ReturnsNoLock()
    {
        _lock.ReadTarget(Profile).Returns((string?)null);

        CreateSut().Inspect(Profile).Status.Should().Be(ProfileLockStatus.NoLock);
    }

    [Fact]
    public void Inspect_OwnerRunningWithMatchingProfile_ReturnsOwnerAlive()
    {
        _lock.ReadTarget(Profile).Returns($"{Environment.MachineName}-4242");
        _procs.GetAll().Returns(new[] { (Pid: 4242, Cmdline: (string?)Cmdline(Profile)) });

        var result = CreateSut().Inspect(Profile);

        result.Status.Should().Be(ProfileLockStatus.OwnerAlive);
        result.OwnerPid.Should().Be(4242);
    }

    [Fact]
    public void Inspect_OwnerPidNotInProc_ReturnsOwnerDead()
    {
        _lock.ReadTarget(Profile).Returns($"{Environment.MachineName}-4242");
        _procs.GetAll().Returns(Array.Empty<(int, string?)>());

        CreateSut().Inspect(Profile).Status.Should().Be(ProfileLockStatus.OwnerDead);
    }

    [Fact]
    public void Inspect_HostnameMismatch_ReturnsOwnerDead()
    {
        _lock.ReadTarget(Profile).Returns("some-other-box-4242");

        CreateSut().Inspect(Profile).Status.Should().Be(ProfileLockStatus.OwnerDead);
    }

    [Fact]
    public void Inspect_PidReusedByUnrelatedProcess_ReturnsOwnerDead()
    {
        _lock.ReadTarget(Profile).Returns($"{Environment.MachineName}-4242");
        _procs.GetAll().Returns(new[] { (Pid: 4242, Cmdline: (string?)"/usr/bin/some-daemon\0--flag\0") });

        CreateSut().Inspect(Profile).Status.Should().Be(ProfileLockStatus.OwnerDead);
    }
}
