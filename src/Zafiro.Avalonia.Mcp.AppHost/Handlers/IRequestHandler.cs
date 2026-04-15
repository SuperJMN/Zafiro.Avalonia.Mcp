using Zafiro.Avalonia.Mcp.Protocol.Messages;

namespace Zafiro.Avalonia.Mcp.AppHost.Handlers;

public interface IRequestHandler
{
    string Method { get; }
    Task<object> Handle(DiagnosticRequest request);
}
