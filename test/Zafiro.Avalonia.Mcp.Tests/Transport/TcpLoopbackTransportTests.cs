using System.Text;
using Xunit;
using Zafiro.Avalonia.Mcp.AppHost.Transport;

namespace Zafiro.Avalonia.Mcp.Tests.Transport;

public class TcpLoopbackTransportTests
{
    [Fact]
    public async Task EchoesLine_From_Client_To_Handler_And_Back()
    {
        using var transport = new TcpLoopbackTransport();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));

        await transport.StartAsync(async (stream, ct) =>
        {
            using var reader = new StreamReader(stream, Encoding.UTF8);
            await using var writer = new StreamWriter(stream, Encoding.UTF8) { AutoFlush = true };
            var line = await reader.ReadLineAsync(ct);
            await writer.WriteLineAsync($"echo:{line}");
        }, cts.Token);

        Assert.Equal("tcp", transport.Kind);
        Assert.True(transport.Port > 0);
        Assert.StartsWith("tcp:127.0.0.1:", transport.Endpoint);
        Assert.Null(transport.PipeName);

        using var client = new System.Net.Sockets.TcpClient();
        await client.ConnectAsync("127.0.0.1", transport.Port, cts.Token);
        await using var net = client.GetStream();
        using var clientReader = new StreamReader(net, Encoding.UTF8);
        await using var clientWriter = new StreamWriter(net, Encoding.UTF8) { AutoFlush = true };

        await clientWriter.WriteLineAsync("hello");
        var response = await clientReader.ReadLineAsync(cts.Token);

        Assert.Equal("echo:hello", response);
    }

    [Fact]
    public async Task EndpointReflects_BoundEphemeralPort()
    {
        using var transport = new TcpLoopbackTransport();
        await transport.StartAsync((_, _) => Task.CompletedTask, CancellationToken.None);
        Assert.Matches(@"^tcp:127\.0\.0\.1:\d+$", transport.Endpoint);
    }
}
