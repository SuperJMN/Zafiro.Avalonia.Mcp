namespace Zafiro.Avalonia.Mcp.AppHost.Transport;

public interface IDiagnosticTransport : IDisposable
{
    string Kind { get; }
    string Endpoint { get; }
    string? PipeName { get; }
    Task StartAsync(Func<Stream, CancellationToken, Task> connectionHandler, CancellationToken ct);
}
