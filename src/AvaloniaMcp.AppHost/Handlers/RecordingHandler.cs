using System.Text.Json;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using AvaloniaMcp.Protocol;
using AvaloniaMcp.Protocol.Messages;

namespace AvaloniaMcp.AppHost.Handlers;

public sealed class RecordingHandler : IRequestHandler
{
    private static FrameRecorder? _activeRecorder;

    public string Method => ProtocolMethods.StartRecording;

    public async Task<object> Handle(DiagnosticRequest request)
    {
        int? nodeId = null;
        var fps = 15;
        var maxDurationSec = 10;

        if (request.Params is JsonElement p)
        {
            if (p.TryGetProperty("nodeId", out var nid)) nodeId = nid.GetInt32();
            if (p.TryGetProperty("fps", out var f)) fps = f.GetInt32();
            if (p.TryGetProperty("maxDurationSec", out var md)) maxDurationSec = md.GetInt32();
        }

        return await Dispatcher.UIThread.InvokeAsync<object>(() =>
        {
            if (_activeRecorder is not null)
                return new { error = "Recording already in progress. Stop it first." };

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
        return await Dispatcher.UIThread.InvokeAsync<object>(() =>
        {
            var recorder = RecordingHandler.GetActiveRecorder();
            if (recorder is null)
                return new { error = "No active recording" };

            recorder.Stop();
            var frames = recorder.GetFrames();
            RecordingHandler.ClearRecorder();

            if (frames.Count == 0)
                return new { error = "No frames captured" };

            // Encode as animated GIF
            var gifBytes = GifEncoder.Encode(frames, recorder.FrameDelayMs);
            var base64 = Convert.ToBase64String(gifBytes);

            foreach (var frame in frames)
                frame.Dispose();

            return new
            {
                data = base64,
                mimeType = "image/gif",
                frameCount = frames.Count,
                durationMs = frames.Count * recorder.FrameDelayMs,
                sizeBytes = gifBytes.Length
            };
        });
    }
}
