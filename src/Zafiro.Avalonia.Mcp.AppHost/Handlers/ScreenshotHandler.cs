using System.Text.Json;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Zafiro.Avalonia.Mcp.AppHost.Selectors;
using Zafiro.Avalonia.Mcp.Protocol;
using Zafiro.Avalonia.Mcp.Protocol.Messages;

namespace Zafiro.Avalonia.Mcp.AppHost.Handlers;

public sealed class ScreenshotHandler : IRequestHandler
{
    public string Method => ProtocolMethods.Screenshot;

    public async Task<object> Handle(DiagnosticRequest request)
    {
        string? selector = null;
        if (request.Params is JsonElement p && p.TryGetProperty("selector", out var s))
            selector = s.GetString();

        return await Dispatcher.UIThread.InvokeAsync<object>(() =>
        {
            Visual? target;
            if (!string.IsNullOrWhiteSpace(selector))
            {
                var (visual, error) = SelectorRequestHelper.ResolveSingle(selector);
                if (visual is null) return error!;
                target = visual;
            }
            else
            {
                target = NodeRegistry.GetRoots().FirstOrDefault();
                if (target is null) return new { error = "No windows available" };
            }

            return Capture(target);
        });
    }

    internal static object Capture(Visual target)
    {
        var nodeId = NodeRegistry.GetOrRegister(target);
        var bounds = target.Bounds;
        var pixelSize = new PixelSize((int)Math.Max(bounds.Width, 1), (int)Math.Max(bounds.Height, 1));
        var rtb = new RenderTargetBitmap(pixelSize);
        rtb.Render(target);

        using var ms = new MemoryStream();
        rtb.Save(ms);
        var base64 = Convert.ToBase64String(ms.ToArray());

        return new
        {
            nodeId,
            data = base64,
            mimeType = "image/png",
            width = pixelSize.Width,
            height = pixelSize.Height,
            sizeBytes = ms.Length
        };
    }
}
