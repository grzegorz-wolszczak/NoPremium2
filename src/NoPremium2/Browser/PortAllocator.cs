using System.Net;
using System.Net.Sockets;

namespace NoPremium2.Browser;

public interface IPortAllocator
{
    int GetFreePort();
}

public sealed class PortAllocator : IPortAllocator
{
    public int GetFreePort()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        int port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }
}
