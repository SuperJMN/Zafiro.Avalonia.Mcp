using System.Text.Json;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using Avalonia.VisualTree;
using AvaloniaMcp.Protocol;
using AvaloniaMcp.Protocol.Messages;
using AvaloniaMcp.Protocol.Models;

namespace AvaloniaMcp.AppHost.Handlers;

public sealed class TreeHandler : IRequestHandler
{
    public string Method => ProtocolMethods.GetTree;

    public async Task<object> Handle(DiagnosticRequest request)
    {
        var treeKind = "Visual";
        var maxDepth = 10;
        int? nodeId = null;

        if (request.Params is JsonElement p)
        {
            if (p.TryGetProperty("treeKind", out var tk)) treeKind = tk.GetString() ?? "Visual";
            if (p.TryGetProperty("depth", out var d)) maxDepth = d.GetInt32();
            if (p.TryGetProperty("nodeId", out var nid)) nodeId = nid.GetInt32();
        }

        return await Dispatcher.UIThread.InvokeAsync<object>(() =>
        {
            if (nodeId.HasValue)
            {
                var visual = NodeRegistry.Resolve(nodeId.Value);
                if (visual is null) return new { error = $"Node {nodeId} not found" };
                return SerializeNode(visual, treeKind, maxDepth, 0);
            }

            var results = new List<NodeInfo>();
            foreach (var window in NodeRegistry.GetWindows())
            {
                results.Add(SerializeNode(window, treeKind, maxDepth, 0));
            }
            return results;
        });
    }

    private static NodeInfo SerializeNode(Visual visual, string treeKind, int maxDepth, int currentDepth)
    {
        List<NodeInfo>? children = null;

        if (currentDepth < maxDepth)
        {
            var childVisuals = treeKind switch
            {
                "Logical" => (visual as ILogical)?.LogicalChildren.OfType<Visual>() ?? [],
                "Merged" => GetMergedChildren(visual),
                _ => visual.GetVisualChildren()
            };

            var childList = childVisuals.ToList();
            if (childList.Count > 0)
            {
                children = [];
                foreach (var child in childList)
                    children.Add(SerializeNode(child, treeKind, maxDepth, currentDepth + 1));
            }
        }

        return NodeInfoBuilder.Create(visual, children);
    }

    private static IEnumerable<Visual> GetMergedChildren(Visual visual)
    {
        var visualChildren = visual.GetVisualChildren().ToHashSet();
        var logicalChildren = (visual as ILogical)?.LogicalChildren.OfType<Visual>().ToList() ?? [];

        foreach (var child in logicalChildren)
            yield return child;

        foreach (var child in visualChildren)
        {
            if (!logicalChildren.Contains(child))
                yield return child;
        }
    }
}

