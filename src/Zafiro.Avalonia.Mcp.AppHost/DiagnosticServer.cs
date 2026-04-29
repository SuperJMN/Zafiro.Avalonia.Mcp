using System.Text;
using Zafiro.Avalonia.Mcp.AppHost.Discovery;
using Zafiro.Avalonia.Mcp.AppHost.Handlers;
using Zafiro.Avalonia.Mcp.AppHost.Transport;

namespace Zafiro.Avalonia.Mcp.AppHost;

public sealed class DiagnosticServer : IDisposable
{
    private readonly CancellationTokenSource _cts = new();
    private readonly RequestDispatcher _dispatcher = new();
    private readonly IDiagnosticTransport _transport;

    public DiagnosticServer() : this(new NamedPipeTransport(DiscoveryWriter.PipeName)) { }

    public DiagnosticServer(IDiagnosticTransport transport)
    {
        _transport = transport;
    }

    public IDiagnosticTransport Transport => _transport;

    public void Start()
    {
        _ = _transport.StartAsync(HandleConnection, _cts.Token);
        DiscoveryWriter.Write(_transport);
    }

    private async Task HandleConnection(Stream stream, CancellationToken ct)
    {
        using var reader = new StreamReader(stream, Encoding.UTF8);
        await using var writer = new StreamWriter(stream, Encoding.UTF8) { AutoFlush = true };

        while (!ct.IsCancellationRequested)
        {
            var line = await reader.ReadLineAsync(ct);
            if (line is null) break;

            var response = await _dispatcher.Dispatch(line);
            await writer.WriteLineAsync(response);
        }
    }

    public void Dispose()
    {
        _cts.Cancel();
        DiscoveryWriter.Remove();
        _transport.Dispose();
        _cts.Dispose();
    }
}
