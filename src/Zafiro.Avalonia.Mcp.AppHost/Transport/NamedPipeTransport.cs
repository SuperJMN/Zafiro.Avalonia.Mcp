using System.IO.Pipes;

namespace Zafiro.Avalonia.Mcp.AppHost.Transport;

public sealed class NamedPipeTransport : IDiagnosticTransport
{
    private readonly string _pipeName;

    public NamedPipeTransport(string pipeName)
    {
        _pipeName = pipeName;
    }

    public string Kind => "pipe";
    public string Endpoint => _pipeName;
    public string? PipeName => _pipeName;

    public Task StartAsync(Func<Stream, CancellationToken, Task> connectionHandler, CancellationToken ct)
    {
        _ = Task.Run(() => ListenLoop(connectionHandler, ct), ct);
        return Task.CompletedTask;
    }

    private async Task ListenLoop(Func<Stream, CancellationToken, Task> connectionHandler, CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            var pipe = new NamedPipeServerStream(
                _pipeName,
                PipeDirection.InOut,
                NamedPipeServerStream.MaxAllowedServerInstances,
                PipeTransmissionMode.Byte,
                PipeOptions.Asynchronous);

            try
            {
                await pipe.WaitForConnectionAsync(ct);
                _ = HandleAsync(pipe, connectionHandler, ct);
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

    private static async Task HandleAsync(NamedPipeServerStream pipe, Func<Stream, CancellationToken, Task> handler, CancellationToken ct)
    {
        try
        {
            await using (pipe)
            {
                await handler(pipe, ct).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) { }
        catch (IOException) { }
    }

    public void Dispose() { }
}
