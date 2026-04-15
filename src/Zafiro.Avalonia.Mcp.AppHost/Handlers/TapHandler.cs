using Zafiro.Avalonia.Mcp.Protocol;
using Zafiro.Avalonia.Mcp.Protocol.Messages;

namespace Zafiro.Avalonia.Mcp.AppHost.Handlers;

/// <summary>
/// Handles "tap" requests — semantically identical to "click" (touch alias).
/// </summary>
public sealed class TapHandler : IRequestHandler
{
    private readonly InputHandler _clickHandler = new();

    public string Method => ProtocolMethods.Tap;

    public Task<object> Handle(DiagnosticRequest request) => _clickHandler.Handle(request);
}
