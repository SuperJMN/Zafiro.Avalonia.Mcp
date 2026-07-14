using System.Text.Json;
using Avalonia;
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
        var maxCells = 9;
        var maxSheetDimension = 1024;

        if (request.Params is JsonElement p)
        {
            if (p.TryGetProperty("selector", out var s)) selector = s.GetString();
            if (p.TryGetProperty("fps", out var f)) fps = f.GetInt32();
            if (p.TryGetProperty("maxDurationSec", out var md)) maxDurationSec = md.GetInt32();
            if (p.TryGetProperty("maxCells", out var mc)) maxCells = mc.GetInt32();
            if (p.TryGetProperty("maxSheetDimension", out var msd)) maxSheetDimension = msd.GetInt32();
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

            _activeRecorder = new FrameRecorder(target, fps, maxDurationSec, maxCells, maxSheetDimension);
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
        var recorder = RecordingHandler.GetActiveRecorder();
        if (recorder is null)
            return new { error = "No active recording" };

        var frameDelayMs = recorder.FrameDelayMs;
        var maxCells = recorder.MaxCells;
        var maxSheetDimension = recorder.MaxSheetDimension;

        // Stop the recorder and build the contact sheet on the UI thread: reading the captured
        // RenderTargetBitmaps and drawing them into a new one is an Avalonia render operation.
        return await Dispatcher.UIThread.InvokeAsync<object>(() =>
        {
            recorder.Stop();
            var frames = recorder.GetFrames();
            RecordingHandler.ClearRecorder();

            if (frames.Count == 0)
                return new { error = "No frames captured" };

            try
            {
                var sheet = ContactSheetComposer.Compose(frames, frameDelayMs, maxCells, maxSheetDimension);

                return new
                {
                    data = Convert.ToBase64String(sheet.Png),
                    mimeType = "image/png",
                    width = sheet.Width,
                    height = sheet.Height,
                    columns = sheet.Columns,
                    rows = sheet.Rows,
                    frameCount = frames.Count,
                    sampledFrames = sheet.SampledFrames,
                    durationMs = frames.Count * frameDelayMs
                };
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.TraceError($"Contact sheet composition failed: {ex}");
                return new { error = $"Contact sheet composition failed: {ex.Message}" };
            }
            finally
            {
                foreach (var frame in frames)
                    frame.Dispose();
            }
        });
    }
}
