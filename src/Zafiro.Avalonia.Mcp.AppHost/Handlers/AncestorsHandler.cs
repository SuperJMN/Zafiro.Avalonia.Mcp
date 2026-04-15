using System.Text.Json;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Zafiro.Avalonia.Mcp.Protocol;
using Zafiro.Avalonia.Mcp.Protocol.Messages;
using Zafiro.Avalonia.Mcp.Protocol.Models;

namespace Zafiro.Avalonia.Mcp.AppHost.Handlers;

public sealed class AncestorsHandler : IRequestHandler
{
    public string Method => ProtocolMethods.GetAncestors;

    public async Task<object> Handle(DiagnosticRequest request)
    {
        int nodeId = 0;
        if (request.Params is JsonElement p && p.TryGetProperty("nodeId", out var nid))
            nodeId = nid.GetInt32();

        return await Dispatcher.UIThread.InvokeAsync<object>(() =>
        {
            var visual = NodeRegistry.Resolve(nodeId);
            if (visual is null) return new { error = $"Node {nodeId} not found" };

            var chain = new List<NodeInfo>();
            Visual? current = visual;
            while (current is not null)
            {
                chain.Add(NodeInfoBuilder.Create(current));
                current = current.GetVisualParent();
            }

            chain.Reverse();
            return chain;
        });
    }
}
