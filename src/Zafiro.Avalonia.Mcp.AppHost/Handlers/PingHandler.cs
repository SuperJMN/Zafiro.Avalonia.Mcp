using Zafiro.Avalonia.Mcp.Protocol;
using Zafiro.Avalonia.Mcp.Protocol.Messages;

namespace Zafiro.Avalonia.Mcp.AppHost.Handlers;

public sealed class PingHandler : IRequestHandler
{
    public string Method => ProtocolMethods.Ping;

    public Task<object> Handle(DiagnosticRequest request) =>
        Task.FromResult<object>(new { status = "ok", timestamp = DateTimeOffset.UtcNow });
}
