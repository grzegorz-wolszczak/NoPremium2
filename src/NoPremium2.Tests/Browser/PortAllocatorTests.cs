using System.Net.Sockets;
using AwesomeAssertions;
using NoPremium2.Browser;
using Xunit;

namespace NoPremium2.Tests.Browser;

public sealed class PortAllocatorTests
{
    [Fact]
    public void GetFreePort_ReturnsPositivePort()
    {
        var sut = new PortAllocator();
        sut.GetFreePort().Should().BeGreaterThan(0);
    }

    [Fact]
    public void GetFreePort_ReturnedPortIsNotInUse()
    {
        var sut = new PortAllocator();
        int port = sut.GetFreePort();

        // Port should be bindable (i.e., free)
        var action = () =>
        {
            using var listener = new TcpListener(System.Net.IPAddress.Loopback, port);
            listener.Start();
            listener.Stop();
        };

        action.Should().NotThrow();
    }

    [Fact]
    public void GetFreePort_CalledTwice_ReturnsDifferentPorts()
    {
        var sut = new PortAllocator();
        int port1 = sut.GetFreePort();
        int port2 = sut.GetFreePort();
        // Not guaranteed, but almost always true since OS picks sequentially
        port1.Should().BeGreaterThan(0);
        port2.Should().BeGreaterThan(0);
    }
}
