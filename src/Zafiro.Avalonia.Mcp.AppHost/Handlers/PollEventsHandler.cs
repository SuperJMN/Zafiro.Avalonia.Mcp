using System.Text.Json;
using Zafiro.Avalonia.Mcp.AppHost.Events;
using Zafiro.Avalonia.Mcp.Protocol;
using Zafiro.Avalonia.Mcp.Protocol.Messages;

namespace Zafiro.Avalonia.Mcp.AppHost.Handlers;

/// <summary>
/// Long-poll for events on a previously created subscription. Returns immediately
/// with any buffered events, or waits up to <c>timeoutMs</c> (default 30000, max 60000)
/// for new events to arrive.
/// </summary>
public sealed class PollEventsHandler : IRequestHandler
{
    private readonly EventBus _bus;

    public PollEventsHandler() : this(EventBus.Instance) { }
    public PollEventsHandler(EventBus bus) { _bus = bus; }

    public string Method => ProtocolMethods.PollEvents;

    public async Task<object> Handle(DiagnosticRequest request)
    {
        string? id = null;
        var timeoutMs = 30_000;

        if (request.Params is { ValueKind: JsonValueKind.Object } p)
        {
            if (p.TryGetProperty("subscriptionId", out var s) && s.ValueKind == JsonValueKind.String)
                id = s.GetString();
            if (p.TryGetProperty("timeoutMs", out var t) && t.ValueKind == JsonValueKind.Number)
                timeoutMs = t.GetInt32();
        }

        if (string.IsNullOrEmpty(id))
            throw new InvalidOperationException("INVALID_REQUEST: subscriptionId is required");

        timeoutMs = Math.Clamp(timeoutMs, 0, 60_000);

        var events = await _bus.PollAsync(id, timeoutMs);
        return new { subscriptionId = id, events };
    }
}
