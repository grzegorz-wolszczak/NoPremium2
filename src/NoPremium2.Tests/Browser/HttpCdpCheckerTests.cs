using System.Net;
using AwesomeAssertions;
using NoPremium2.Browser;
using Xunit;

namespace NoPremium2.Tests.Browser;

public sealed class HttpCdpCheckerTests
{
    [Fact]
    public async Task IsRespondingAsync_EndpointOn127001_ReturnsTrue()
    {
        using var listener = new HttpListener();
        int port = FreePort();
        listener.Prefixes.Add($"http://127.0.0.1:{port}/");
        listener.Start();
        var serve = Task.Run(async () =>
        {
            var ctx = await listener.GetContextAsync();
            ctx.Response.StatusCode = 200;
            var body = System.Text.Encoding.UTF8.GetBytes("{\"Browser\":\"Vivaldi\"}");
            await ctx.Response.OutputStream.WriteAsync(body, 0, body.Length);
            ctx.Response.Close();
        });

        using var http = new HttpClient();
        var sut = new HttpCdpChecker(http);

        (await sut.IsRespondingAsync(port)).Should().BeTrue();
        await serve;
    }

    [Fact]
    public async Task IsRespondingAsync_ConnectionRefused_ReturnsFalse()
    {
        using var http = new HttpClient();
        var sut = new HttpCdpChecker(http);

        (await sut.IsRespondingAsync(FreePort())).Should().BeFalse();
    }

    private static int FreePort()
    {
        var l = new System.Net.Sockets.TcpListener(IPAddress.Loopback, 0);
        l.Start();
        int p = ((IPEndPoint)l.LocalEndpoint).Port;
        l.Stop();
        return p;
    }
}
