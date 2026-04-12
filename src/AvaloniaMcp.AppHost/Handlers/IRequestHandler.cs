using AvaloniaMcp.Protocol.Messages;

namespace AvaloniaMcp.AppHost.Handlers;

public interface IRequestHandler
{
    string Method { get; }
    Task<object> Handle(DiagnosticRequest request);
}
