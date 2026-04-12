using System.Text.Json;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Threading;
using Avalonia.VisualTree;
using AvaloniaMcp.Protocol;
using AvaloniaMcp.Protocol.Messages;

namespace AvaloniaMcp.AppHost.Handlers;

public sealed class ScreenTextHandler : IRequestHandler
{
    public string Method => ProtocolMethods.GetScreenText;

    public async Task<object> Handle(DiagnosticRequest request)
    {
        int? nodeId = null;

        if (request.Params is JsonElement p)
        {
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

            var rootVisual = FindRootVisual(root);
            var entries = new List<ScreenTextEntry>();

            CollectText(root, rootVisual, entries);

            // Deduplicate: templated controls (e.g. Button→AccessText→TextBlock)
            // produce the same text at nearly the same position. Keep only the
            // first (outermost) occurrence within a 5px radius.
            var deduped = new List<ScreenTextEntry>();
            foreach (var entry in entries)
            {
                var isDuplicate = deduped.Any(e =>
                    e.Text == entry.Text &&
                    Math.Abs(e.X - entry.X) < 5 &&
                    Math.Abs(e.Y - entry.Y) < 5);
                if (!isDuplicate)
                    deduped.Add(entry);
            }

            deduped.Sort((a, b) =>
            {
                var yCompare = a.Y.CompareTo(b.Y);
                return yCompare != 0 ? yCompare : a.X.CompareTo(b.X);
            });

            return new
            {
                lines = deduped,
                plainText = string.Join("\n", deduped.Select(e => e.Text))
            };
        });
    }

    private static Visual FindRootVisual(Visual visual)
    {
        var current = visual;
        while (current.GetVisualParent() is Visual parent)
            current = parent;
        return current;
    }

    private static void CollectText(Visual visual, Visual rootVisual, List<ScreenTextEntry> entries)
    {
        if (!visual.IsVisible) return;

        var text = ExtractText(visual);
        if (!string.IsNullOrEmpty(text))
        {
            var transform = visual.TransformToVisual(rootVisual);
            if (transform.HasValue)
            {
                var absoluteBounds = visual.Bounds.TransformToAABB(transform.Value);
                entries.Add(new ScreenTextEntry
                {
                    Text = text,
                    X = Math.Round(absoluteBounds.X, 1),
                    Y = Math.Round(absoluteBounds.Y, 1),
                    NodeId = NodeRegistry.GetOrRegister(visual)
                });
            }
        }

        foreach (var child in visual.GetVisualChildren())
        {
            CollectText(child, rootVisual, entries);
        }
    }

    private static string? ExtractText(Visual visual)
    {
        return visual switch
        {
            TextBlock tb => tb.Text,
            TextBox tb => tb.Text,
            ContentPresenter cp when cp.Content is string s => s,
            ContentControl cc when cc.Content is string s => s,
            _ => null
        };
    }

    private sealed class ScreenTextEntry
    {
        public string Text { get; init; } = "";
        public double X { get; init; }
        public double Y { get; init; }
        public int NodeId { get; init; }
    }
}
