using System.Text.Json;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Primitives;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Zafiro.Avalonia.Mcp.AppHost.Selectors;
using Zafiro.Avalonia.Mcp.Protocol;
using Zafiro.Avalonia.Mcp.Protocol.Messages;

namespace Zafiro.Avalonia.Mcp.AppHost.Handlers;

public sealed class ScreenTextHandler : IRequestHandler
{
    public string Method => ProtocolMethods.GetScreenText;

    public async Task<object> Handle(DiagnosticRequest request)
    {
        string? selector = null;
        var visibleOnly = false;

        if (request.Params is JsonElement p)
        {
            if (p.TryGetProperty("selector", out var s)) selector = s.GetString();
            if (p.TryGetProperty("visibleOnly", out var vo)) visibleOnly = vo.GetBoolean();
        }

        return await Dispatcher.UIThread.InvokeAsync<object>(() =>
        {
            List<Visual> roots;
            if (!string.IsNullOrWhiteSpace(selector))
            {
                var (visual, error) = SelectorRequestHelper.ResolveSingle(selector);
                if (visual is null) return error!;
                roots = [visual];
            }
            else
            {
                roots = NodeRegistry.GetInspectableRoots().ToList();
                if (roots.Count == 0) return new { error = "No windows found" };
            }

            return BuildResult(roots, visibleOnly);
        });
    }

    /// <summary>
    /// Collects text for the given roots. Coordinates are in device-independent pixels relative to
    /// the top-level root (window or popup) identified by each line's <c>rootId</c>; lines from
    /// different roots are not in a comparable coordinate space.
    /// </summary>
    internal static object BuildResult(IEnumerable<Visual> roots, bool visibleOnly)
    {
        var entries = new List<ScreenTextEntry>();

        foreach (var root in roots)
        {
            var rootVisual = FindRootVisual(root);
            // Same root identity as get_snapshot: the window or popup TopLevel.
            var rootId = NodeRegistry.GetOrRegister(TopLevel.GetTopLevel(root) ?? rootVisual);
            var windowBounds = new Rect(rootVisual.Bounds.Size);
            CollectText(root, rootVisual, rootId, entries, visibleOnly, windowBounds, []);
        }

        // Reading order within each root; roots keep their discovery order.
        var ordered = entries
            .GroupBy(e => e.RootId)
            .SelectMany(g => g.OrderBy(e => e.Y).ThenBy(e => e.X))
            .ToList();

        return new
        {
            coordinateSpace = "root",
            lines = ordered,
            plainText = string.Join("\n", ordered.Select(e => e.Text))
        };
    }

    private static Visual FindRootVisual(Visual visual)
    {
        var current = visual;
        while (current.GetVisualParent() is Visual parent)
            current = parent;
        return current;
    }

    private static void CollectText(Visual visual, Visual rootVisual, int rootId, List<ScreenTextEntry> entries,
        bool visibleOnly, Rect windowBounds, IReadOnlyList<string> ancestorTexts)
    {
        if (!visual.IsVisible) return;

        var text = ExtractText(visual);
        if (!string.IsNullOrEmpty(text))
        {
            // A template part repeating its owner's label (Button → ContentPresenter → TextBlock) is the
            // same visible text; distinct controls with equal text are not ancestors of one another.
            if (IsTemplateRepresentation(visual) && ancestorTexts.Contains(text))
            {
                text = null;
            }
            else if (GetRootBounds(visual, rootVisual) is { } absoluteBounds)
            {
                // When visibleOnly is true, skip elements outside the window viewport
                // and elements clipped by ancestor ScrollViewers
                if (visibleOnly && !IsInViewport(visual, absoluteBounds, rootVisual, windowBounds))
                    return;

                entries.Add(new ScreenTextEntry
                {
                    Text = text,
                    X = Math.Round(absoluteBounds.X, 1),
                    Y = Math.Round(absoluteBounds.Y, 1),
                    NodeId = NodeRegistry.GetOrRegister(visual),
                    RootId = rootId
                });
            }
        }

        var childAncestorTexts = string.IsNullOrEmpty(text) ? ancestorTexts : [.. ancestorTexts, text];
        foreach (var child in visual.GetVisualChildren())
        {
            CollectText(child, rootVisual, rootId, entries, visibleOnly, windowBounds, childAncestorTexts);
        }
    }

    /// <summary>
    /// Bounds of <paramref name="visual"/> in <paramref name="rootVisual"/> coordinates.
    /// <see cref="Visual.Bounds"/> is parent-relative, so only its size is transformed from local space.
    /// </summary>
    private static Rect? GetRootBounds(Visual visual, Visual rootVisual) =>
        visual.TransformToVisual(rootVisual) is { } transform
            ? new Rect(visual.Bounds.Size).TransformToAABB(transform)
            : null;

    private static bool IsTemplateRepresentation(Visual visual) =>
        visual is StyledElement { TemplatedParent: not null }
        || visual.GetVisualParent() is ContentPresenter { Content: string };

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
            if (current is ScrollViewer sv && GetRootBounds(sv, rootVisual) is { } svAbsoluteBounds
                && !svAbsoluteBounds.Intersects(absoluteBounds))
            {
                return false;
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
            HeaderedSelectingItemsControl hsic when hsic.Header is string s => s,
            HeaderedItemsControl hic when hic.Header is string s => s,
            HeaderedContentControl hcc when hcc.Header is string s => s,
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
        public int RootId { get; init; }
    }
}
