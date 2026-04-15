using System.Text.Json;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Zafiro.Avalonia.Mcp.Protocol;
using Zafiro.Avalonia.Mcp.Protocol.Messages;

namespace Zafiro.Avalonia.Mcp.AppHost.Handlers;

public sealed class ScreenTextHandler : IRequestHandler
{
    public string Method => ProtocolMethods.GetScreenText;

    public async Task<object> Handle(DiagnosticRequest request)
    {
        int? nodeId = null;
        var visibleOnly = false;

        if (request.Params is JsonElement p)
        {
            if (p.TryGetProperty("nodeId", out var nid)) nodeId = nid.GetInt32();
            if (p.TryGetProperty("visibleOnly", out var vo)) visibleOnly = vo.GetBoolean();
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
            var windowBounds = new Rect(0, 0, rootVisual.Bounds.Width, rootVisual.Bounds.Height);
            var entries = new List<ScreenTextEntry>();

            CollectText(root, rootVisual, entries, visibleOnly, windowBounds);

            // Deduplicate: templated controls (e.g. Button→AccessText→TextBlock)
            // produce the same text at nearly the same position. Keep only the
            // first (outermost) occurrence within a 20px radius (accounts for padding).
            var deduped = new List<ScreenTextEntry>();
            foreach (var entry in entries)
            {
                var isDuplicate = deduped.Any(e =>
                    e.Text == entry.Text &&
                    Math.Abs(e.X - entry.X) < 20 &&
                    Math.Abs(e.Y - entry.Y) < 20);
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

    private static void CollectText(Visual visual, Visual rootVisual, List<ScreenTextEntry> entries,
        bool visibleOnly, Rect windowBounds)
    {
        if (!visual.IsVisible) return;

        var text = ExtractText(visual);
        if (!string.IsNullOrEmpty(text))
        {
            var transform = visual.TransformToVisual(rootVisual);
            if (transform.HasValue)
            {
                var absoluteBounds = visual.Bounds.TransformToAABB(transform.Value);

                // When visibleOnly is true, skip elements outside the window viewport
                // and elements clipped by ancestor ScrollViewers
                if (visibleOnly && !IsInViewport(visual, absoluteBounds, rootVisual, windowBounds))
                    return;

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
            CollectText(child, rootVisual, entries, visibleOnly, windowBounds);
        }
    }

    /// <summary>
    /// Check if the element's absolute bounds intersect the visible viewport.
    /// Walks up the ancestor chain to find ScrollViewers and tests against their clip regions.
    /// </summary>
    private static bool IsInViewport(Visual visual, Rect absoluteBounds, Visual rootVisual, Rect windowBounds)
    {
        // Must be within the window bounds
        if (!windowBounds.Intersects(absoluteBounds))
            return false;

        // Check against each ancestor ScrollViewer's viewport
        var current = visual.GetVisualParent();
        while (current is not null)
        {
            if (current is ScrollViewer sv)
            {
                var svTransform = sv.TransformToVisual(rootVisual);
                if (svTransform.HasValue)
                {
                    var svAbsoluteBounds = sv.Bounds.TransformToAABB(svTransform.Value);
                    if (!svAbsoluteBounds.Intersects(absoluteBounds))
                        return false;
                }
            }

            current = current.GetVisualParent();
        }

        return true;
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
