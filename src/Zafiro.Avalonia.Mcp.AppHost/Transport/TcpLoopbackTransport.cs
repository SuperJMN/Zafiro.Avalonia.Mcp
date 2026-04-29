using System.Net;
using System.Net.Sockets;

namespace Zafiro.Avalonia.Mcp.AppHost.Transport;

public sealed class TcpLoopbackTransport : IDiagnosticTransport
{
    private readonly TcpListener _listener;

    public TcpLoopbackTransport()
    {
        _listener = new TcpListener(IPAddress.Loopback, 0);
    }

    public string Kind => "tcp";
    public string Endpoint => $"tcp:{IPAddress.Loopback}:{Port}";
    public string? PipeName => null;
    public int Port => ((IPEndPoint)_listener.LocalEndpoint).Port;

    public Task StartAsync(Func<Stream, CancellationToken, Task> connectionHandler, CancellationToken ct)
    {
        _listener.Start();
        _ = Task.Run(() => AcceptLoop(connectionHandler, ct), ct);
        return Task.CompletedTask;
    }

    private async Task AcceptLoop(Func<Stream, CancellationToken, Task> connectionHandler, CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                var client = await _listener.AcceptTcpClientAsync(ct);
                _ = HandleAsync(client, connectionHandler, ct);
            }
            catch (OperationCanceledException) { break; }
            catch (ObjectDisposedException) { break; }
            catch { /* keep accepting */ }
        }
    }

    private static async Task HandleAsync(TcpClient client, Func<Stream, CancellationToken, Task> handler, CancellationToken ct)
    {
        try
        {
            using (client)
            await using (var stream = client.GetStream())
            {
                await handler(stream, ct).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) { }
        catch (IOException) { }
        catch (SocketException) { }
    }

    public void Dispose()
    {
        try { _listener.Stop(); } catch { }
    }
}
