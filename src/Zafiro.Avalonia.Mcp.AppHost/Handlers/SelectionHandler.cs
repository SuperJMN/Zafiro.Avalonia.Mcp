using System.Text.Json;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Zafiro.Avalonia.Mcp.Protocol;
using Zafiro.Avalonia.Mcp.Protocol.Messages;

namespace Zafiro.Avalonia.Mcp.AppHost.Handlers;

public sealed class SelectionHandler : IRequestHandler
{
    public string Method => ProtocolMethods.SelectItem;

    public async Task<object> Handle(DiagnosticRequest request)
    {
        int nodeId = 0;
        int? index = null;
        string? text = null;

        if (request.Params is JsonElement p)
        {
            if (p.TryGetProperty("nodeId", out var nid)) nodeId = nid.GetInt32();
            if (p.TryGetProperty("index", out var idx)) index = idx.GetInt32();
            if (p.TryGetProperty("text", out var t)) text = t.GetString();
        }

        return await Dispatcher.UIThread.InvokeAsync<object>(() =>
        {
            var visual = NodeRegistry.Resolve(nodeId);
            if (visual is null) return new { error = $"Node {nodeId} not found" };

            var selector = visual as SelectingItemsControl;

            if (selector is null && visual is Control control)
                selector = control.GetVisualAncestors().OfType<SelectingItemsControl>().FirstOrDefault();

            if (selector is null)
                return new { error = "Node is not a SelectingItemsControl and no parent SelectingItemsControl found" };

            if (index is not null)
            {
                if (index < 0 || index >= selector.ItemCount)
                    return new { error = $"Index {index} out of range (0..{selector.ItemCount - 1})" };

                selector.SelectedIndex = index.Value;
            }
            else if (text is not null)
            {
                var found = false;
                for (var i = 0; i < selector.ItemCount; i++)
                {
                    var item = selector.Items[i];
                    var itemText = item?.ToString() ?? "";

                    if (item is ContentControl cc)
                        itemText = cc.Content?.ToString() ?? itemText;

                    if (itemText.Contains(text, StringComparison.OrdinalIgnoreCase))
                    {
                        selector.SelectedIndex = i;
                        found = true;
                        break;
                    }
                }

                if (!found)
                    return new { error = $"No item matching text '{text}' found" };
            }
            else
            {
                return new { error = "Either 'index' or 'text' must be provided" };
            }

            var selectedText = "";
            if (selector.SelectedItem is ContentControl selCc)
                selectedText = selCc.Content?.ToString() ?? selector.SelectedItem?.ToString() ?? "";
            else
                selectedText = selector.SelectedItem?.ToString() ?? "";

            return new { success = true, selectedIndex = selector.SelectedIndex, selectedText };
        });
    }
}
