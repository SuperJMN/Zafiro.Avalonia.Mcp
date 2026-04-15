using System.Text.Json;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Zafiro.Avalonia.Mcp.Protocol;
using Zafiro.Avalonia.Mcp.Protocol.Messages;

namespace Zafiro.Avalonia.Mcp.AppHost.Handlers;

/// <summary>
/// Efficient screen diff: takes a snapshot of visible text (or tree structure) and returns
/// a compact diff against a previous snapshot. Designed for AI agents that need to detect
/// what changed after an action — saves tokens vs. re-reading the full screen.
///
/// Usage: call with action="snapshot" to capture, then action="diff" to compare with the
/// last snapshot. Returns only the lines that changed.
/// </summary>
public sealed class DiffTreeHandler : IRequestHandler
{
    public string Method => ProtocolMethods.DiffTree;

    // Keep last snapshot per-window so diffs work across requests
    private static readonly Dictionary<int, List<string>> Snapshots = new();

    public async Task<object> Handle(DiagnosticRequest request)
    {
        string action = "diff"; // default: auto-snapshot + diff
        int? nodeId = null;

        if (request.Params is JsonElement p)
        {
            if (p.TryGetProperty("action", out var a)) action = a.GetString() ?? "diff";
            if (p.TryGetProperty("nodeId", out var nid)) nodeId = nid.GetInt32();
        }

        return await Dispatcher.UIThread.InvokeAsync<object>(() =>
        {
            Visual? root;
            if (nodeId.HasValue)
            {
                root = NodeRegistry.Resolve(nodeId.Value);
                if (root is null) return new { error = $"Node {nodeId} not found" };
            }
            else
            {
                root = NodeRegistry.GetWindows().FirstOrDefault();
                if (root is null) return new { error = "No windows found" };
            }

            var rootId = NodeRegistry.GetOrRegister(root);
            var currentLines = CollectTextLines(root);

            if (action == "snapshot")
            {
                Snapshots[rootId] = currentLines;
                return new { action = "snapshot", rootId, lineCount = currentLines.Count, message = "Snapshot captured" };
            }

            // action == "diff" (auto-snapshot if none exists)
            if (!Snapshots.TryGetValue(rootId, out var previousLines))
            {
                // First call — just capture and return full text
                Snapshots[rootId] = currentLines;
                return new
                {
                    action = "diff",
                    rootId,
                    isFirstSnapshot = true,
                    lines = currentLines,
                };
            }

            // Compute a simple line-by-line diff
            var added = currentLines.Except(previousLines).ToList();
            var removed = previousLines.Except(currentLines).ToList();

            // Update snapshot to current
            Snapshots[rootId] = currentLines;

            if (added.Count == 0 && removed.Count == 0)
            {
                return new
                {
                    action = "diff",
                    rootId,
                    hasChanges = false,
                    message = "No changes detected"
                };
            }

            return new
            {
                action = "diff",
                rootId,
                hasChanges = true,
                added,
                removed,
            };
        });
    }

    private static List<string> CollectTextLines(Visual root)
    {
        var lines = new List<string>();
        CollectTextRecursive(root, lines);
        return lines;
    }

    private static void CollectTextRecursive(Visual visual, List<string> lines)
    {
        if (!visual.IsVisible) return;

        var text = visual switch
        {
            TextBlock tb => tb.Text,
            TextBox tb => tb.Text,
            ContentPresenter cp when cp.Content is string s => s,
            ContentControl cc when cc.Content is string s => s,
            _ => null
        };

        if (!string.IsNullOrWhiteSpace(text))
            lines.Add(text);

        foreach (var child in visual.GetVisualChildren())
            CollectTextRecursive(child, lines);
    }
}
