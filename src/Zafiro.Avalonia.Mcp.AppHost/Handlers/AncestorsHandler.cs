using System.Text.Json;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Zafiro.Avalonia.Mcp.AppHost.Selectors;
using Zafiro.Avalonia.Mcp.Protocol;
using Zafiro.Avalonia.Mcp.Protocol.Messages;
using Zafiro.Avalonia.Mcp.Protocol.Models;

namespace Zafiro.Avalonia.Mcp.AppHost.Handlers;

public sealed class AncestorsHandler : IRequestHandler
{
    public string Method => ProtocolMethods.GetAncestors;

    public async Task<object> Handle(DiagnosticRequest request)
    {
        string? selector = null;
        if (request.Params is JsonElement p && p.TryGetProperty("selector", out var s))
            selector = s.GetString();

        return await Dispatcher.UIThread.InvokeAsync<object>(() =>
        {
            var (visual, error) = SelectorRequestHelper.ResolveSingle(selector);
            if (visual is null) return error!;
            return GetAncestors(visual);
        });
    }

    internal static object GetAncestors(Visual visual)
    {
        var chain = new List<NodeInfo>();
        Visual? current = visual;
        while (current is not null)
        {
            chain.Add(NodeInfoBuilder.Create(current));
            current = current.GetVisualParent();
        }

        chain.Reverse();
        return chain;
    }
}
