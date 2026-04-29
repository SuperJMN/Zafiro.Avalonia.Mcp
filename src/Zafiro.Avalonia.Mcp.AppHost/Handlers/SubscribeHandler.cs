using System.Text.Json;
using Zafiro.Avalonia.Mcp.AppHost.Events;
using Zafiro.Avalonia.Mcp.Protocol;
using Zafiro.Avalonia.Mcp.Protocol.Messages;

namespace Zafiro.Avalonia.Mcp.AppHost.Handlers;

/// <summary>
/// Creates a long-poll event subscription. Returns a subscriptionId that the
/// client uses with <see cref="PollEventsHandler"/> and <see cref="UnsubscribeHandler"/>.
/// </summary>
public sealed class SubscribeHandler : IRequestHandler
{
    private readonly EventBus _bus;

    public SubscribeHandler() : this(EventBus.Instance) { }
    public SubscribeHandler(EventBus bus) { _bus = bus; }

    public string Method => ProtocolMethods.Subscribe;

    public Task<object> Handle(DiagnosticRequest request)
    {
        var events = new List<string>();
        int? nodeId = null;
        string? property = null;

        if (request.Params is { ValueKind: JsonValueKind.Object } p)
        {
            if (p.TryGetProperty("events", out var evArr) && evArr.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in evArr.EnumerateArray())
                {
                    if (item.ValueKind == JsonValueKind.String)
                    {
                        var s = item.GetString();
                        if (!string.IsNullOrWhiteSpace(s)) events.Add(s);
                    }
                }
            }
            if (p.TryGetProperty("filter", out var filter) && filter.ValueKind == JsonValueKind.Object)
            {
                if (filter.TryGetProperty("nodeId", out var n) && n.ValueKind == JsonValueKind.Number)
                    nodeId = n.GetInt32();
                if (filter.TryGetProperty("property", out var pr) && pr.ValueKind == JsonValueKind.String)
                    property = pr.GetString();
            }
        }

        var (sub, error) = _bus.Subscribe(events, nodeId, property);
        if (sub is null)
            throw new InvalidOperationException(error ?? "SUBSCRIBE_FAILED");

        return Task.FromResult<object>(new
        {
            subscriptionId = sub.Id,
            events = sub.Events.ToArray(),
            filter = new { nodeId = sub.FilterNodeId, property = sub.FilterProperty },
            bufferLimit = sub.BufferLimit
        });
    }
}
