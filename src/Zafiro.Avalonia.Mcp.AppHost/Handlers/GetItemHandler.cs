using System.Reflection;
using System.Text.Json;
using Avalonia.Controls;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Zafiro.Avalonia.Mcp.AppHost.Selectors;
using Zafiro.Avalonia.Mcp.Protocol;
using Zafiro.Avalonia.Mcp.Protocol.Messages;

namespace Zafiro.Avalonia.Mcp.AppHost.Handlers;

/// <summary>
/// Realizes a virtualized item in an ItemsControl by index, text, or data-context property match,
/// forcing layout if needed. Essential for AI agents working with long virtualized lists where
/// the container hasn't been created yet.
/// </summary>
public sealed class GetItemHandler : IRequestHandler
{
    public string Method => ProtocolMethods.GetItem;

    public async Task<object> Handle(DiagnosticRequest request)
    {
        string selector = "";
        int? index = null;
        string? text = null;
        string? dcMatchPath = null;
        string? dcMatchValue = null;

        if (request.Params is JsonElement p)
        {
            if (p.TryGetProperty("selector", out var sel)) selector = sel.GetString() ?? "";
            if (p.TryGetProperty("index", out var idx) && idx.ValueKind == JsonValueKind.Number)
                index = idx.GetInt32();
            if (p.TryGetProperty("text", out var t)) text = t.GetString();
            if (p.TryGetProperty("dcMatch", out var dcm) && dcm.ValueKind == JsonValueKind.Object)
            {
                if (dcm.TryGetProperty("path", out var pathEl)) dcMatchPath = pathEl.GetString();
                if (dcm.TryGetProperty("value", out var valEl)) dcMatchValue = valEl.GetString();
            }
        }

        if (string.IsNullOrWhiteSpace(selector))
            return new { error = "selector is required" };

        if (index is null && text is null && dcMatchPath is null)
            return new { error = "one of index, text, or dcMatch must be provided" };

        return await Dispatcher.UIThread.InvokeAsync<object>(() =>
        {
            var engine = new SelectorEngine();
            var match = engine.ResolveSingle(selector);

            if (match is null)
                return new { error = $"Selector '{selector}' did not match any element" };

            // Resolve to an ItemsControl — direct or closest descendant
            var itemsControl = match as ItemsControl
                ?? match.GetVisualDescendants().OfType<ItemsControl>().FirstOrDefault();

            if (itemsControl is null)
                return new { error = $"No ItemsControl found at or inside the selected element" };

            // Determine target index
            int targetIndex;

            if (index.HasValue)
            {
                targetIndex = index.Value;
            }
            else if (text is not null)
            {
                targetIndex = FindIndexByText(itemsControl, text);
                if (targetIndex < 0)
                    return new { error = $"No item matching text '{text}' found" };
            }
            else
            {
                // dcMatch
                targetIndex = FindIndexByDcMatch(itemsControl, dcMatchPath!, dcMatchValue);
                if (targetIndex < 0)
                    return new { error = $"No item matching dcMatch path='{dcMatchPath}' value='{dcMatchValue}' found" };
            }

            if (targetIndex < 0 || targetIndex >= itemsControl.ItemCount)
                return new { error = $"Index {targetIndex} out of range (0..{itemsControl.ItemCount - 1})" };

            // Realize the container
            itemsControl.ScrollIntoView(targetIndex);
            var container = itemsControl.ContainerFromIndex(targetIndex);

            if (container is null)
            {
                // Force layout and retry once
                itemsControl.UpdateLayout();
                container = itemsControl.ContainerFromIndex(targetIndex);
            }

            if (container is null)
                return new { error = "item_not_realized", index = targetIndex };

            var nodeId = NodeRegistry.GetOrRegister(container);
            var containerText = GetContainerText(container, itemsControl, targetIndex);

            return new
            {
                nodeId,
                type = container.GetType().Name,
                index = targetIndex,
                isRealized = true,
                text = containerText
            };
        });
    }

    private static int FindIndexByText(ItemsControl itemsControl, string needle)
    {
        for (int i = 0; i < itemsControl.ItemCount; i++)
        {
            var item = itemsControl.Items[i];
            var itemText = item?.ToString() ?? "";
            if (item is ContentControl cc)
                itemText = cc.Content?.ToString() ?? itemText;
            if (itemText.Contains(needle, StringComparison.OrdinalIgnoreCase))
                return i;
        }
        return -1;
    }

    private static int FindIndexByDcMatch(ItemsControl itemsControl, string path, string? value)
    {
        for (int i = 0; i < itemsControl.ItemCount; i++)
        {
            var item = itemsControl.Items[i];
            if (item is null) continue;
            var resolved = ResolvePropertyPath(item, path);
            var actual = Convert.ToString(resolved, System.Globalization.CultureInfo.InvariantCulture) ?? "";
            if (string.Equals(actual, value, StringComparison.OrdinalIgnoreCase))
                return i;
        }
        return -1;
    }

    private static object? ResolvePropertyPath(object obj, string path)
    {
        object? current = obj;
        foreach (var segment in path.Split('.'))
        {
            if (current is null) return null;
            var prop = current.GetType().GetProperty(segment,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.IgnoreCase);
            if (prop is null) return null;
            try { current = prop.GetValue(current); }
            catch { return null; }
        }
        return current;
    }

    private static string? GetContainerText(Control container, ItemsControl itemsControl, int index)
    {
        // Try the container visual text first
        if (container is ContentControl cc && cc.Content is string s) return s;
        if (container is TextBlock tb) return tb.Text;

        // Fall back to item ToString
        var item = itemsControl.Items[index];
        return item?.ToString();
    }
}
