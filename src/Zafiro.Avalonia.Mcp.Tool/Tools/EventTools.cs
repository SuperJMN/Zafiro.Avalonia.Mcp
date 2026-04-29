using System.ComponentModel;
using ModelContextProtocol.Server;
using Zafiro.Avalonia.Mcp.Protocol;
using Zafiro.Avalonia.Mcp.Tool.Connection;

namespace Zafiro.Avalonia.Mcp.Tool.Tools;

[McpServerToolType]
public sealed class EventTools
{
    [McpServerTool(Name = "subscribe"), Description("""
        Create a long-poll event subscription. Returns a subscriptionId you must pass to poll_events to receive notifications, and to unsubscribe to clean up. Supported event types: 'property_changed', 'window_opened', 'window_closed', 'focus_changed'. Optionally narrow with filterNodeId and/or filterProperty (case-insensitive). Subscriptions auto-expire after 5 minutes of inactivity (no poll). Max 32 concurrent subscriptions per app — exceeding returns 'SUBSCRIPTION_LIMIT'.
        Returns: {subscriptionId, events:[...], filter:{nodeId?, property?}, bufferLimit}.
        Example: subscribe(events=["property_changed"], filterNodeId=42, filterProperty="IsChecked") → {"subscriptionId":"a1b2c3d4e5f6","events":["property_changed"],"filter":{"nodeId":42,"property":"IsChecked"},"bufferLimit":1000}
        """)]
    public static async Task<string> Subscribe(
        ConnectionPool pool,
        [Description("Event types to subscribe to: 'property_changed', 'window_opened', 'window_closed', 'focus_changed'")] string[] events,
        [Description("Optional nodeId filter (only events for this node are delivered)")] int? filterNodeId = null,
        [Description("Optional Avalonia property name filter (e.g. 'IsChecked', 'Text'; case-insensitive)")] string? filterProperty = null)
    {
        var conn = pool.GetActive();
        var parms = new Dictionary<string, object?> { ["events"] = events };
        if (filterNodeId is not null || filterProperty is not null)
        {
            var filter = new Dictionary<string, object?>();
            if (filterNodeId is not null) filter["nodeId"] = filterNodeId;
            if (filterProperty is not null) filter["property"] = filterProperty;
            parms["filter"] = filter;
        }
        return await conn.InvokeAsync(ProtocolMethods.Subscribe, parms, "No subscription");
    }

    [McpServerTool(Name = "poll_events"), Description("""
        Long-poll for buffered events on a subscription. Returns immediately if any events are queued, otherwise waits up to timeoutMs (default 30000, max 60000) for a new event. Each call drains the buffer. The subscription's TTL resets on every poll — keep polling to stay alive. Buffer caps at 1000 events per subscription; oldest are dropped on overflow.
        Returns: {subscriptionId, events:[{subscriptionId, type, timestamp, nodeId?, data}, ...]} (empty events array on timeout).
        Example: poll_events(subscriptionId="a1b2c3d4e5f6", timeoutMs=5000) → {"subscriptionId":"a1b2c3d4e5f6","events":[{"subscriptionId":"a1b2c3d4e5f6","type":"property_changed","timestamp":"2025-01-15T12:34:56Z","nodeId":42,"data":{"property":"IsChecked","oldValue":"False","newValue":"True"}}]}
        """)]
    public static async Task<string> PollEvents(
        ConnectionPool pool,
        [Description("Subscription ID returned by subscribe")] string subscriptionId,
        [Description("Max wait in milliseconds (default 30000, max 60000)")] int timeoutMs = 30000)
    {
        var conn = pool.GetActive();
        var parms = new Dictionary<string, object?>
        {
            ["subscriptionId"] = subscriptionId,
            ["timeoutMs"] = timeoutMs
        };
        try
        {
            var clientTimeout = TimeSpan.FromMilliseconds(Math.Min(timeoutMs, 60_000) + 5_000);
            var result = await conn.SendAsync(ProtocolMethods.PollEvents, parms, clientTimeout);
            return result?.ToString() ?? "No events";
        }
        catch (Exception ex)
        {
            return $"Error: {ex.Message}";
        }
    }

    [McpServerTool(Name = "unsubscribe"), Description("""
        Cancel an event subscription and free its buffer. Idempotent — unknown ids return removed=false but do not error. Always call this when done; otherwise the subscription auto-expires after 5 minutes of inactivity.
        Returns: {subscriptionId, removed:bool}.
        Example: unsubscribe(subscriptionId="a1b2c3d4e5f6") → {"subscriptionId":"a1b2c3d4e5f6","removed":true}
        """)]
    public static async Task<string> Unsubscribe(
        ConnectionPool pool,
        [Description("Subscription ID returned by subscribe")] string subscriptionId)
    {
        var conn = pool.GetActive();
        return await conn.InvokeAsync(ProtocolMethods.Unsubscribe,
            new { subscriptionId }, "Not removed");
    }
}
