using System.ComponentModel;
using Zafiro.Avalonia.Mcp.Protocol;
using Zafiro.Avalonia.Mcp.Tool.Connection;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace Zafiro.Avalonia.Mcp.Tool.Tools;

[McpServerToolType]
public sealed class CaptureTools
{
    [McpServerTool(Name = "screenshot"), Description("""
        Capture a PNG screenshot of an element (or whole window if selector omitted). EXPENSIVE — prefer get_snapshot/get_screen_text/get_interactables when you only need text or actions. Use screenshots only for visual verification.
        Returns: {width, height, base64} PNG.
        Example: {"width":1280,"height":720,"base64":"iVBORw0KGgo..."}
        """)]
    public static async Task<IReadOnlyList<ContentBlock>> Screenshot(
        ConnectionPool pool,
        [Description("CSS-like selector identifying the element to capture. Omit for the first window.")] string? selector = null)
    {
        var conn = pool.GetActive();
        var parms = selector is not null ? new { selector } : null;
        return await conn.InvokeRichAsync(ProtocolMethods.Screenshot, parms, result =>
        {
            var data = result.GetProperty("data").GetString();
            var width = result.GetProperty("width").GetInt32();
            var height = result.GetProperty("height").GetInt32();

            if (data is null) return [new TextContentBlock { Text = "Screenshot data was empty" }];

            return
            [
                new TextContentBlock { Text = $"Screenshot captured ({width}×{height})" },
                ImageContentBlock.FromBytes(Convert.FromBase64String(data), "image/png")
            ];
        });
    }

    [McpServerTool(Name = "start_recording"), Description("""
        Start recording frames for an animated GIF. Pair with stop_recording. For most cases prefer capture_animation which combines start+wait+stop atomically.
        Returns: {recordingId, fps, maxDurationSec}.
        Example: {"recordingId":"rec-1","fps":15,"maxDurationSec":10}
        """)]
    public static async Task<string> StartRecording(
        ConnectionPool pool,
        [Description("CSS-like selector to record. Omit for the first window.")] string? selector = null,
        [Description("Frames per second (1-30, default 15)")] int fps = 15,
        [Description("Maximum recording duration in seconds (1-30, default 10)")] int maxDurationSec = 10)
    {
        var conn = pool.GetActive();
        var parms = new Dictionary<string, object> { ["fps"] = fps, ["maxDurationSec"] = maxDurationSec };
        if (selector is not null) parms["selector"] = selector;
        return await conn.InvokeAsync(ProtocolMethods.StartRecording, parms);
    }

    [McpServerTool(Name = "stop_recording"), Description("""
        Stop the active GIF recording started by start_recording and return the GIF.
        Returns: {frames, durationMs, base64} GIF.
        Example: {"frames":45,"durationMs":3000,"base64":"R0lGODlh..."}
        """)]
    public static async Task<IReadOnlyList<ContentBlock>> StopRecording(ConnectionPool pool)
    {
        var conn = pool.GetActive();
        return await conn.InvokeRichAsync(ProtocolMethods.StopRecording, null, result =>
        {
            if (result.TryGetProperty("error", out var errProp))
                return [new TextContentBlock { Text = errProp.GetString() ?? "Unknown error" }];

            var data = result.GetProperty("data").GetString();
            var frameCount = result.GetProperty("frameCount").GetInt32();
            var durationMs = result.GetProperty("durationMs").GetInt32();

            if (data is null) return [new TextContentBlock { Text = "Recording data was empty" }];

            return
            [
                new TextContentBlock { Text = $"Recorded {frameCount} frames ({durationMs}ms)" },
                ImageContentBlock.FromBytes(Convert.FromBase64String(data), "image/gif")
            ];
        });
    }

    [McpServerTool(Name = "capture_animation"), Description("""
        Record a short animated GIF in a single call (start_recording + wait + stop_recording). Use for transitions, loaders, or visualizing a UI change over a fixed duration.
        Returns: {frames, durationMs, base64} GIF.
        Example: {"frames":45,"durationMs":3000,"base64":"R0lGODlh..."}
        """)]
    public static async Task<IReadOnlyList<ContentBlock>> CaptureAnimation(
        ConnectionPool pool,
        [Description("Duration in seconds to record (1-10)")] int durationSec = 3,
        [Description("Frames per second")] int fps = 15,
        [Description("CSS-like selector to capture. Omit for the first window.")] string? selector = null)
    {
        var conn = pool.GetActive();

        durationSec = Math.Clamp(durationSec, 1, 10);

        var parms = new Dictionary<string, object> { ["fps"] = fps, ["maxDurationSec"] = durationSec };
        if (selector is not null) parms["selector"] = selector;
        await conn.SendAsync(ProtocolMethods.StartRecording, parms);

        await Task.Delay(TimeSpan.FromSeconds(durationSec + 0.5));

        return await StopRecording(pool);
    }
}
