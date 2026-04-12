using AvaloniaMcp.Protocol;
using AvaloniaMcp.Protocol.Messages;

namespace AvaloniaMcp.AppHost.Handlers;

public sealed class PingHandler : IRequestHandler
{
    public string Method => ProtocolMethods.Ping;

    public Task<object> Handle(DiagnosticRequest request) =>
        Task.FromResult<object>(new { status = "ok", timestamp = DateTimeOffset.UtcNow });
}
