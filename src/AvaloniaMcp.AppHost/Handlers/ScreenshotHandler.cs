using System.Text.Json;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using AvaloniaMcp.Protocol;
using AvaloniaMcp.Protocol.Messages;

namespace AvaloniaMcp.AppHost.Handlers;

public sealed class ScreenshotHandler : IRequestHandler
{
    public string Method => ProtocolMethods.Screenshot;

    public async Task<object> Handle(DiagnosticRequest request)
    {
        int? nodeId = null;
        if (request.Params is JsonElement p && p.TryGetProperty("nodeId", out var nid))
            nodeId = nid.GetInt32();

        return await Dispatcher.UIThread.InvokeAsync<object>(() =>
        {
            Visual? target;
            if (nodeId.HasValue)
            {
                target = NodeRegistry.Resolve(nodeId.Value);
                if (target is null) return new { error = $"Node {nodeId} not found" };
            }
            else
            {
                target = NodeRegistry.GetWindows().FirstOrDefault();
                if (target is null) return new { error = "No windows available" };
            }

            var bounds = target.Bounds;
            var pixelSize = new PixelSize((int)Math.Max(bounds.Width, 1), (int)Math.Max(bounds.Height, 1));
            var rtb = new RenderTargetBitmap(pixelSize);
            rtb.Render(target);

            using var ms = new MemoryStream();
            rtb.Save(ms);
            var base64 = Convert.ToBase64String(ms.ToArray());

            return new
            {
                data = base64,
                mimeType = "image/png",
                width = pixelSize.Width,
                height = pixelSize.Height,
                sizeBytes = ms.Length
            };
        });
    }
}
