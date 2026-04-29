using System.Text.Json;
using Zafiro.Avalonia.Mcp.AppHost.Events;
using Zafiro.Avalonia.Mcp.Protocol;
using Zafiro.Avalonia.Mcp.Protocol.Messages;

namespace Zafiro.Avalonia.Mcp.AppHost.Handlers;

/// <summary>
/// Cancels an event subscription and frees its buffer.
/// </summary>
public sealed class UnsubscribeHandler : IRequestHandler
{
    private readonly EventBus _bus;

    public UnsubscribeHandler() : this(EventBus.Instance) { }
    public UnsubscribeHandler(EventBus bus) { _bus = bus; }

    public string Method => ProtocolMethods.Unsubscribe;

    public Task<object> Handle(DiagnosticRequest request)
    {
        string? id = null;
        if (request.Params is { ValueKind: JsonValueKind.Object } p &&
            p.TryGetProperty("subscriptionId", out var s) && s.ValueKind == JsonValueKind.String)
        {
            id = s.GetString();
        }

        if (string.IsNullOrEmpty(id))
            throw new InvalidOperationException("INVALID_REQUEST: subscriptionId is required");

        var removed = _bus.Unsubscribe(id);
        return Task.FromResult<object>(new { subscriptionId = id, removed });
    }
}
