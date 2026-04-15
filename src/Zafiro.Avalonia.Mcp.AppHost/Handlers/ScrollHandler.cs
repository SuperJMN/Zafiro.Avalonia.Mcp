using System.Text.Json;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Zafiro.Avalonia.Mcp.Protocol;
using Zafiro.Avalonia.Mcp.Protocol.Messages;

namespace Zafiro.Avalonia.Mcp.AppHost.Handlers;

public sealed class ScrollHandler : IRequestHandler
{
    public string Method => ProtocolMethods.Scroll;

    public async Task<object> Handle(DiagnosticRequest request)
    {
        int nodeId = 0;
        string direction = "";
        double amount = 100;

        if (request.Params is JsonElement p)
        {
            if (p.TryGetProperty("nodeId", out var nid)) nodeId = nid.GetInt32();
            if (p.TryGetProperty("direction", out var d)) direction = d.GetString() ?? "";
            if (p.TryGetProperty("amount", out var a)) amount = a.GetDouble();
        }

        return await Dispatcher.UIThread.InvokeAsync<object>(() =>
        {
            var visual = NodeRegistry.Resolve(nodeId);
            if (visual is null) return new { error = $"Node {nodeId} not found" };

            var scrollViewer = visual as ScrollViewer;

            if (scrollViewer is null && visual is Control control)
                scrollViewer = control.GetVisualAncestors().OfType<ScrollViewer>().FirstOrDefault();

            if (scrollViewer is null)
                return new { error = "No ScrollViewer found at or above the specified node" };

            var offset = scrollViewer.Offset;
            var maxX = Math.Max(0, scrollViewer.Extent.Width - scrollViewer.Viewport.Width);
            var maxY = Math.Max(0, scrollViewer.Extent.Height - scrollViewer.Viewport.Height);

            switch (direction.ToLowerInvariant())
            {
                case "up":
                    offset = new Vector(offset.X, Math.Max(0, offset.Y - amount));
                    break;
                case "down":
                    offset = new Vector(offset.X, Math.Min(maxY, offset.Y + amount));
                    break;
                case "left":
                    offset = new Vector(Math.Max(0, offset.X - amount), offset.Y);
                    break;
                case "right":
                    offset = new Vector(Math.Min(maxX, offset.X + amount), offset.Y);
                    break;
                default:
                    return new { error = $"Unknown direction '{direction}'. Use: up, down, left, right" };
            }

            scrollViewer.Offset = offset;

            return new { success = true, offsetX = scrollViewer.Offset.X, offsetY = scrollViewer.Offset.Y };
        });
    }
}
