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
        Start capturing frames. Pair with stop_recording, which returns a single static "contact sheet" PNG — a labelled grid of frames sampled across the recording. There is NO GIF/video output: vision models cannot read animation. Use start/stop to capture a transition you trigger between the two calls; for a fixed-duration clip prefer capture_animation.
        Returns: {success, fps, maxDurationSec}.
        Example: {"success":true,"fps":15,"maxDurationSec":10}
        """)]
    public static async Task<string> StartRecording(
        ConnectionPool pool,
        [Description("CSS-like selector to record. Omit for the first window.")] string? selector = null,
        [Description("Frames per second (1-30, default 15)")] int fps = 15,
        [Description("Maximum recording duration in seconds (1-30, default 10)")] int maxDurationSec = 10,
        [Description("Max frames shown in the contact-sheet grid (1-64, default 9)")] int maxCells = 9,
        [Description("Max length of the contact sheet's longest side in pixels — bounds token cost (128-4096, default 1024)")] int maxSheetDimension = 1024)
    {
        var conn = pool.GetActive();
        var parms = new Dictionary<string, object>
        {
            ["fps"] = fps,
            ["maxDurationSec"] = maxDurationSec,
            ["maxCells"] = maxCells,
            ["maxSheetDimension"] = maxSheetDimension
        };
        if (selector is not null) parms["selector"] = selector;
        return await conn.InvokeAsync(ProtocolMethods.StartRecording, parms);
    }

    [McpServerTool(Name = "stop_recording"), Description("""
        Stop the active recording and return a single "contact sheet" PNG: a labelled grid of frames sampled across the recording (no GIF/video). Token cost is bounded by the maxCells and maxSheetDimension set when recording started.
        Returns: text summary + one PNG image.
        Example: "Contact sheet: 9 of 45 frames in a 3×3 grid over 3000ms (1024×640 PNG)" + <image/png>
        """)]
    public static async Task<IReadOnlyList<ContentBlock>> StopRecording(ConnectionPool pool)
    {
        var conn = pool.GetActive();
        return await conn.InvokeRichAsync(ProtocolMethods.StopRecording, null, result =>
        {
            var data = result.GetProperty("data").GetString();
            if (data is null) return [new TextContentBlock { Text = "Recording data was empty" }];

            var width = result.GetProperty("width").GetInt32();
            var height = result.GetProperty("height").GetInt32();
            var columns = result.GetProperty("columns").GetInt32();
            var rows = result.GetProperty("rows").GetInt32();
            var frameCount = result.GetProperty("frameCount").GetInt32();
            var sampledFrames = result.GetProperty("sampledFrames").GetInt32();
            var durationMs = result.GetProperty("durationMs").GetInt32();

            return
            [
                new TextContentBlock { Text = $"Contact sheet: {sampledFrames} of {frameCount} frames in a {columns}×{rows} grid over {durationMs}ms ({width}×{height} PNG)" },
                ImageContentBlock.FromBytes(Convert.FromBase64String(data), "image/png")
            ];
        });
    }

    [McpServerTool(Name = "capture_animation"), Description("""
        Record for a fixed duration and return a single "contact sheet" PNG in one call (start_recording + wait + stop_recording): a labelled grid of frames sampled over time. Use for transitions, loaders, or visualizing a UI change. There is NO GIF/video — vision models cannot read animation, so frames are laid out as one static image. Token cost is bounded by maxCells and maxSheetDimension.
        Returns: text summary + one PNG image.
        Example: "Contact sheet: 9 of 45 frames in a 3×3 grid over 3000ms (1024×640 PNG)" + <image/png>
        """)]
    public static async Task<IReadOnlyList<ContentBlock>> CaptureAnimation(
        ConnectionPool pool,
        [Description("Duration in seconds to record (1-10)")] int durationSec = 3,
        [Description("Frames per second")] int fps = 15,
        [Description("CSS-like selector to capture. Omit for the first window.")] string? selector = null,
        [Description("Max frames shown in the contact-sheet grid (1-64, default 9)")] int maxCells = 9,
        [Description("Max length of the contact sheet's longest side in pixels — bounds token cost (128-4096, default 1024)")] int maxSheetDimension = 1024)
    {
        var conn = pool.GetActive();

        durationSec = Math.Clamp(durationSec, 1, 10);

        var parms = new Dictionary<string, object>
        {
            ["fps"] = fps,
            ["maxDurationSec"] = durationSec,
            ["maxCells"] = maxCells,
            ["maxSheetDimension"] = maxSheetDimension
        };
        if (selector is not null) parms["selector"] = selector;
        await conn.SendAsync(ProtocolMethods.StartRecording, parms);

        await Task.Delay(TimeSpan.FromSeconds(durationSec + 0.5));

        return await StopRecording(pool);
    }
}
