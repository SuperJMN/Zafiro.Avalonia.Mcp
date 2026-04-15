using System.ComponentModel;
using Zafiro.Avalonia.Mcp.Protocol;
using Zafiro.Avalonia.Mcp.Tool.Connection;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace Zafiro.Avalonia.Mcp.Tool.Tools;

[McpServerToolType]
public sealed class CaptureTools
{
    [McpServerTool(Name = "screenshot"), Description("Capture a PNG screenshot of a specific UI element or the entire window. Returns the image as base64-encoded PNG with dimensions. Prefer get_screen_text or get_interactables when you only need text content or available actions — they are cheaper and faster.")]
    public static async Task<IReadOnlyList<ContentBlock>> Screenshot(
        ConnectionPool pool,
        [Description("Node ID to capture. Omit for the first window.")] int? nodeId = null)
    {
        var conn = pool.GetActive();
        var parms = nodeId.HasValue ? new { nodeId = nodeId.Value } : null;
        var result = await conn.SendAsync(ProtocolMethods.Screenshot, parms);

        if (result is null) return [new TextContentBlock { Text = "No screenshot data" }];

        var data = result.Value.GetProperty("data").GetString();
        var width = result.Value.GetProperty("width").GetInt32();
        var height = result.Value.GetProperty("height").GetInt32();

        if (data is null) return [new TextContentBlock { Text = "Screenshot data was empty" }];

        return
        [
            new TextContentBlock { Text = $"Screenshot captured ({width}×{height})" },
            ImageContentBlock.FromBytes(Convert.FromBase64String(data), "image/png")
        ];
    }

    [McpServerTool(Name = "start_recording"), Description("Start recording frames for an animated GIF. You must call stop_recording to finish and retrieve the GIF. For a simpler workflow, use capture_animation which handles start, wait, and stop in one call.")]
    public static async Task<string> StartRecording(
        ConnectionPool pool,
        [Description("Node ID to record. Omit for the first window.")] int? nodeId = null,
        [Description("Frames per second (1-30, default 15)")] int fps = 15,
        [Description("Maximum recording duration in seconds (1-30, default 10)")] int maxDurationSec = 10)
    {
        var conn = pool.GetActive();
        var result = await conn.SendAsync(ProtocolMethods.StartRecording,
            new { nodeId, fps, maxDurationSec });
        return result?.ToString() ?? "No result";
    }

    [McpServerTool(Name = "stop_recording"), Description("Stop an active GIF recording and return the animated GIF. Returns the image as base64-encoded GIF with frame count and duration metadata. Must be called after start_recording.")]
    public static async Task<IReadOnlyList<ContentBlock>> StopRecording(ConnectionPool pool)
    {
        var conn = pool.GetActive();
        var result = await conn.SendAsync(ProtocolMethods.StopRecording);

        if (result is null) return [new TextContentBlock { Text = "No recording data" }];

        if (result.Value.TryGetProperty("error", out var errProp))
            return [new TextContentBlock { Text = errProp.GetString() ?? "Unknown error" }];

        var data = result.Value.GetProperty("data").GetString();
        var frameCount = result.Value.GetProperty("frameCount").GetInt32();
        var durationMs = result.Value.GetProperty("durationMs").GetInt32();

        if (data is null) return [new TextContentBlock { Text = "Recording data was empty" }];

        return
        [
            new TextContentBlock { Text = $"Recorded {frameCount} frames ({durationMs}ms)" },
            ImageContentBlock.FromBytes(Convert.FromBase64String(data), "image/gif")
        ];
    }

    [McpServerTool(Name = "capture_animation"), Description("Record a short animation and return as animated GIF in a single call (convenience wrapper: start_recording + wait + stop_recording). Use this to capture transitions, animations, or UI changes over a set duration.")]
    public static async Task<IReadOnlyList<ContentBlock>> CaptureAnimation(
        ConnectionPool pool,
        [Description("Duration in seconds to record (1-10)")] int durationSec = 3,
        [Description("Frames per second")] int fps = 15,
        [Description("Node ID to capture. Omit for the first window.")] int? nodeId = null)
    {
        var conn = pool.GetActive();

        durationSec = Math.Clamp(durationSec, 1, 10);

        await conn.SendAsync(ProtocolMethods.StartRecording,
            new { nodeId, fps, maxDurationSec = durationSec });

        await Task.Delay(TimeSpan.FromSeconds(durationSec + 0.5));

        return await StopRecording(pool);
    }
}
