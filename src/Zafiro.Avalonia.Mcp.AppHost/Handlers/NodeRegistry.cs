using System.Runtime.CompilerServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.VisualTree;

namespace Zafiro.Avalonia.Mcp.AppHost.Handlers;

public static class NodeRegistry
{
    private static readonly Dictionary<int, WeakReference<Visual>> Nodes = new();
    private static ConditionalWeakTable<Visual, StrongBox<int>> _reverseMap = new();
    private static int _nextId;

    public static int Register(Visual visual)
    {
        var id = Interlocked.Increment(ref _nextId);
        Nodes[id] = new WeakReference<Visual>(visual);
        _reverseMap.AddOrUpdate(visual, new StrongBox<int>(id));
        return id;
    }

    public static void Clear()
    {
        Nodes.Clear();
        _reverseMap = new ConditionalWeakTable<Visual, StrongBox<int>>();
        _nextId = 0;
    }

    public static Visual? Resolve(int nodeId)
    {
        if (Nodes.TryGetValue(nodeId, out var weakRef) && weakRef.TryGetTarget(out var visual))
            return visual;
        return null;
    }

    /// <summary>
    /// Resolves a node and checks it's still attached to the visual tree.
    /// Returns error info if the node is stale (GC'd or detached).
    /// </summary>
    public static (Visual? visual, string? error) ResolveChecked(int nodeId)
    {
        if (!Nodes.TryGetValue(nodeId, out var weakRef) || !weakRef.TryGetTarget(out var visual))
            return (null, $"Node {nodeId} not found (may have been garbage collected)");

        if (visual.GetVisualParent() is null && visual is not Window)
            return (null, $"Node {nodeId} is stale (detached from visual tree). Re-query with search or get_interactables to get fresh nodeIds.");

        return (visual, null);
    }

    public static int GetOrRegister(Visual visual)
    {
        if (_reverseMap.TryGetValue(visual, out var box))
            return box.Value;
        return Register(visual);
    }

    public static IEnumerable<Window> GetWindows()
    {
        if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            return desktop.Windows;
        return [];
    }

    /// <summary>
    /// Returns all root <see cref="TopLevel"/>s for the running app: <see cref="Window"/>s on desktop,
    /// the single hosted view on Android/iOS/Browser. Use this whenever a handler only needs visual-tree
    /// access (descendants, bounds, focus) and not <see cref="Window"/>-specific API.
    /// </summary>
    public static IEnumerable<TopLevel> GetRoots()
    {
        if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            foreach (var w in desktop.Windows) yield return w;
            yield break;
        }

        if (Application.Current?.ApplicationLifetime is ISingleViewApplicationLifetime singleView
            && singleView.MainView is { } mainView
            && TopLevel.GetTopLevel(mainView) is { } topLevel)
        {
            yield return topLevel;
        }
    }

    public static Visual? FindByQuery(string query)
    {
        foreach (var window in GetRoots())
        {
            // Search by name: #Name
            if (query.StartsWith('#'))
            {
                var name = query[1..];
                var found = window.GetVisualDescendants()
                    .OfType<Control>()
                    .FirstOrDefault(c => string.Equals(c.Name, name, StringComparison.OrdinalIgnoreCase));
                if (found is not null) return found;
            }
            else
            {
                // Search by type name
                var found = window.GetVisualDescendants()
                    .FirstOrDefault(v => v.GetType().Name.Contains(query, StringComparison.OrdinalIgnoreCase));
                if (found is not null) return found;
            }
        }
        return null;
    }
}
