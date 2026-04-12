using Avalonia;
using Avalonia.Media.Imaging;
using Avalonia.Threading;

namespace AvaloniaMcp.AppHost.Handlers;

public sealed class FrameRecorder : IDisposable
{
    private readonly Visual _target;
    private readonly int _fps;
    private readonly int _maxDurationSec;
    private readonly List<RenderTargetBitmap> _frames = [];
    private DispatcherTimer? _timer;
    private readonly int _maxFrames;

    public int FrameDelayMs { get; }

    public FrameRecorder(Visual target, int fps, int maxDurationSec)
    {
        _target = target;
        _fps = fps;
        _maxDurationSec = maxDurationSec;
        _maxFrames = fps * maxDurationSec;
        FrameDelayMs = 1000 / fps;
    }

    public void Start()
    {
        _timer = new DispatcherTimer(DispatcherPriority.Render)
        {
            Interval = TimeSpan.FromMilliseconds(FrameDelayMs)
        };
        _timer.Tick += CaptureFrame;
        _timer.Start();
    }

    private void CaptureFrame(object? sender, EventArgs e)
    {
        if (_frames.Count >= _maxFrames)
        {
            Stop();
            return;
        }

        try
        {
            var bounds = _target.Bounds;
            var w = (int)Math.Max(bounds.Width, 1);
            var h = (int)Math.Max(bounds.Height, 1);
            var rtb = new RenderTargetBitmap(new PixelSize(w, h));
            rtb.Render(_target);
            _frames.Add(rtb);
        }
        catch
        {
            // Skip failed frames
        }
    }

    public void Stop()
    {
        _timer?.Stop();
        _timer = null;
    }

    public List<RenderTargetBitmap> GetFrames() => [.. _frames];

    public void Dispose()
    {
        Stop();
        foreach (var frame in _frames)
            frame.Dispose();
        _frames.Clear();
    }
}
