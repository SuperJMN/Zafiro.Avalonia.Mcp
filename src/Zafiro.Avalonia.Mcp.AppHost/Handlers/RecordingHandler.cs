using System.Text.Json;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Zafiro.Avalonia.Mcp.AppHost.Selectors;
using Zafiro.Avalonia.Mcp.Protocol;
using Zafiro.Avalonia.Mcp.Protocol.Messages;

namespace Zafiro.Avalonia.Mcp.AppHost.Handlers;

public sealed class RecordingHandler : IRequestHandler
{
    private static FrameRecorder? _activeRecorder;

    public string Method => ProtocolMethods.StartRecording;

    public async Task<object> Handle(DiagnosticRequest request)
    {
        string? selector = null;
        var fps = 15;
        var maxDurationSec = 10;

        if (request.Params is JsonElement p)
        {
            if (p.TryGetProperty("selector", out var s)) selector = s.GetString();
            if (p.TryGetProperty("fps", out var f)) fps = f.GetInt32();
            if (p.TryGetProperty("maxDurationSec", out var md)) maxDurationSec = md.GetInt32();
        }

        return await Dispatcher.UIThread.InvokeAsync<object>(() =>
        {
            if (_activeRecorder is not null)
                return new { error = "Recording already in progress. Stop it first." };

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

            fps = Math.Clamp(fps, 1, 30);
            maxDurationSec = Math.Clamp(maxDurationSec, 1, 30);

            _activeRecorder = new FrameRecorder(target, fps, maxDurationSec);
            _activeRecorder.Start();

            return new { success = true, fps, maxDurationSec, message = "Recording started. Call stop_recording to get the result." };
        });
    }

    public static FrameRecorder? GetActiveRecorder() => _activeRecorder;
    public static void ClearRecorder() => _activeRecorder = null;
}

public sealed class StopRecordingHandler : IRequestHandler
{
    public string Method => ProtocolMethods.StopRecording;

    public async Task<object> Handle(DiagnosticRequest request)
    {
        // Stop recording and collect frames on UI thread
        List<RenderTargetBitmap> frames;
        int frameDelayMs;

        var uiResult = await Dispatcher.UIThread.InvokeAsync<object?>(() =>
        {
            var recorder = RecordingHandler.GetActiveRecorder();
            if (recorder is null)
                return new { error = "No active recording" };

            recorder.Stop();
            return null; // success — proceed with encoding
        });

        if (uiResult is not null)
            return uiResult;

        var recorder2 = RecordingHandler.GetActiveRecorder();
        if (recorder2 is null)
            return new { error = "No active recording" };

        frames = recorder2.GetFrames();
        frameDelayMs = recorder2.FrameDelayMs;
        RecordingHandler.ClearRecorder();

        if (frames.Count == 0)
            return new { error = "No frames captured" };

        // Encode GIF off the UI thread to avoid blocking
        var gifBytes = await Task.Run(() =>
        {
            try
            {
                return GifEncoder.Encode(frames, frameDelayMs);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.TraceError($"GIF encoding failed: {ex}");
                return null;
            }
        });

        foreach (var frame in frames)
            frame.Dispose();

        if (gifBytes is null)
            return new { error = "GIF encoding failed" };

        var base64 = Convert.ToBase64String(gifBytes);

        return new
        {
            data = base64,
            mimeType = "image/gif",
            frameCount = frames.Count,
            durationMs = frames.Count * frameDelayMs,
            sizeBytes = gifBytes.Length
        };
    }
}
