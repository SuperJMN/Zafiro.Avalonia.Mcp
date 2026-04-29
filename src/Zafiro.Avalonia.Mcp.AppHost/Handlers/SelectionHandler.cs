using System.Text.Json;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Zafiro.Avalonia.Mcp.AppHost.Selectors;
using Zafiro.Avalonia.Mcp.Protocol;
using Zafiro.Avalonia.Mcp.Protocol.Messages;

namespace Zafiro.Avalonia.Mcp.AppHost.Handlers;

public sealed class SelectionHandler : IRequestHandler
{
    public string Method => ProtocolMethods.SelectItem;

    public async Task<object> Handle(DiagnosticRequest request)
    {
        string? selector = null;
        int? index = null;
        string? text = null;

        if (request.Params is JsonElement p)
        {
            if (p.TryGetProperty("selector", out var s)) selector = s.GetString();
            if (p.TryGetProperty("index", out var idx)) index = idx.GetInt32();
            if (p.TryGetProperty("text", out var t)) text = t.GetString();
        }

        return await Dispatcher.UIThread.InvokeAsync<object>(() =>
        {
            var (visual, error) = SelectorRequestHelper.ResolveSingle(selector);
            if (visual is null) return error!;
            return Select(visual, index, text);
        });
    }

    internal static object Select(Visual visual, int? index, string? text)
    {
        var nodeId = NodeRegistry.GetOrRegister(visual);
        var items = visual as SelectingItemsControl;

        if (items is null && visual is Control control)
            items = control.GetVisualAncestors().OfType<SelectingItemsControl>().FirstOrDefault();

        if (items is null)
            return new { error = "selector did not resolve to a SelectingItemsControl and no parent SelectingItemsControl found", nodeId };

        if (index is not null)
        {
            if (index < 0 || index >= items.ItemCount)
                return new { error = $"Index {index} out of range (0..{items.ItemCount - 1})", nodeId };

            items.SelectedIndex = index.Value;
        }
        else if (text is not null)
        {
            var found = false;
            for (var i = 0; i < items.ItemCount; i++)
            {
                var item = items.Items[i];
                var itemText = item?.ToString() ?? "";

                if (item is ContentControl cc)
                    itemText = cc.Content?.ToString() ?? itemText;

                if (itemText.Contains(text, StringComparison.OrdinalIgnoreCase))
                {
                    items.SelectedIndex = i;
                    found = true;
                    break;
                }
            }

            if (!found)
                return new { error = $"No item matching text '{text}' found", nodeId };
        }
        else
        {
            return new { error = "Either 'index' or 'text' must be provided", nodeId };
        }

        var selectedText = items.SelectedItem is ContentControl selCc
            ? (selCc.Content?.ToString() ?? items.SelectedItem?.ToString() ?? "")
            : (items.SelectedItem?.ToString() ?? "");

        return new { success = true, nodeId, selectedIndex = items.SelectedIndex, selectedText };
    }
}
