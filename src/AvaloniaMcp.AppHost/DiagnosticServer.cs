using System.IO.Pipes;
using System.Text;
using AvaloniaMcp.AppHost.Discovery;
using AvaloniaMcp.AppHost.Handlers;
using AvaloniaMcp.Protocol;
using AvaloniaMcp.Protocol.Messages;

namespace AvaloniaMcp.AppHost;

public sealed class DiagnosticServer : IDisposable
{
    private readonly CancellationTokenSource _cts = new();
    private readonly RequestDispatcher _dispatcher = new();
    private Task? _listenTask;

    public void Start()
    {
        DiscoveryWriter.Write();
        _listenTask = Task.Run(() => ListenLoop(_cts.Token));
    }

    private async Task ListenLoop(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            var pipe = new NamedPipeServerStream(
                DiscoveryWriter.PipeName,
                PipeDirection.InOut,
                NamedPipeServerStream.MaxAllowedServerInstances,
                PipeTransmissionMode.Byte,
                PipeOptions.Asynchronous);

            try
            {
                await pipe.WaitForConnectionAsync(ct);
                _ = HandleConnection(pipe, ct);
            }
            catch (OperationCanceledException)
            {
                await pipe.DisposeAsync();
                break;
            }
            catch
            {
                await pipe.DisposeAsync();
            }
        }
    }

    private async Task HandleConnection(NamedPipeServerStream pipe, CancellationToken ct)
    {
        try
        {
            using (pipe)
            {
                using var reader = new StreamReader(pipe, Encoding.UTF8);
                using var writer = new StreamWriter(pipe, Encoding.UTF8) { AutoFlush = true };

                while (!ct.IsCancellationRequested && pipe.IsConnected)
                {
                    var line = await reader.ReadLineAsync(ct);
                    if (line is null) break;

                    var response = await _dispatcher.Dispatch(line);
                    await writer.WriteLineAsync(response);
                }
            }
        }
        catch (OperationCanceledException) { }
        catch (IOException) { }
    }

    public void Dispose()
    {
        _cts.Cancel();
        DiscoveryWriter.Remove();
        _cts.Dispose();
    }
}
