using Microsoft.Extensions.DependencyInjection;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace Zafiro.Avalonia.Mcp.Tool.Tools;

/// <summary>
/// Lets tools that return their diagnostic as ordinary text or content blocks flag the
/// current <c>tools/call</c> as failed, so the MCP result carries <c>isError: true</c>
/// and clients never need to parse the text to tell failure from success.
/// </summary>
internal static class ToolFailure
{
    private static readonly AsyncLocal<CallState?> Current = new();

    /// <summary>Marks the tool call running on this async flow as failed. No-op outside a call.</summary>
    public static void Mark()
    {
        if (Current.Value is { } state)
            state.Failed = true;
    }

    /// <summary>
    /// Runs <paramref name="call"/> with a fresh failure scope and sets
    /// <see cref="CallToolResult.IsError"/> when any code inside it called <see cref="Mark"/>.
    /// </summary>
    public static async ValueTask<CallToolResult> Track(Func<ValueTask<CallToolResult>> call)
    {
        var previous = Current.Value;
        var state = new CallState();
        Current.Value = state;
        try
        {
            var result = await call();
            if (state.Failed)
                result.IsError = true;
            return result;
        }
        finally
        {
            Current.Value = previous;
        }
    }

    public static IMcpServerBuilder WithToolFailureTracking(this IMcpServerBuilder builder)
        => builder.WithRequestFilters(filters => filters.AddCallToolFilter(
            next => (context, cancellationToken) => Track(() => next(context, cancellationToken))));

    private sealed class CallState
    {
        public bool Failed { get; set; }
    }
}
