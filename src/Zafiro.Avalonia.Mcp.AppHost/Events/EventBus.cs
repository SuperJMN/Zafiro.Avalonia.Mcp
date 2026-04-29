using System.Collections.Concurrent;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Zafiro.Avalonia.Mcp.AppHost.Handlers;

namespace Zafiro.Avalonia.Mcp.AppHost.Events;

/// <summary>
/// Singleton event bus that holds subscriptions, fans out matching events,
/// expires idle subscriptions, and best-effort wires Avalonia event sources.
/// Tests can construct their own instance with custom TTL/limits.
/// </summary>
public sealed class EventBus : IDisposable
{
    public const string PropertyChanged = "property_changed";
    public const string WindowOpened = "window_opened";
    public const string WindowClosed = "window_closed";
    public const string FocusChanged = "focus_changed";

    public static readonly IReadOnlySet<string> KnownEvents =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            PropertyChanged, WindowOpened, WindowClosed, FocusChanged
        };

    public static EventBus Instance { get; } = new();

    private readonly ConcurrentDictionary<string, EventSubscription> _subs = new();
    private readonly TimeSpan _ttl;
    private readonly int _maxSubs;
    private readonly int _bufferLimit;
    private readonly Timer? _expirationTimer;

    private readonly object _hookLock = new();
    private readonly HashSet<Window> _hookedWindows = new();
    private readonly HashSet<Visual> _hookedVisuals = new();
    private bool _wired;
    private Timer? _windowScanTimer;

    public EventBus() : this(TimeSpan.FromMinutes(5), maxSubs: 32, bufferLimit: 1000, runExpirationTimer: true) { }

    public EventBus(TimeSpan ttl, int maxSubs, int bufferLimit, bool runExpirationTimer = false)
    {
        _ttl = ttl;
        _maxSubs = maxSubs;
        _bufferLimit = bufferLimit;
        if (runExpirationTimer)
        {
            _expirationTimer = new Timer(_ => ExpireStale(), null, TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(30));
        }
    }

    public int Count => _subs.Count;
    public int MaxSubscriptions => _maxSubs;
    public int BufferLimit => _bufferLimit;
    public TimeSpan Ttl => _ttl;

    public IReadOnlyCollection<EventSubscription> Subscriptions => _subs.Values.ToArray();

    public (EventSubscription? sub, string? error) Subscribe(
        IReadOnlyList<string> events,
        int? filterNodeId = null,
        string? filterProperty = null)
    {
        ExpireStale();

        if (events.Count == 0)
            return (null, "INVALID_REQUEST: events must not be empty");

        foreach (var e in events)
        {
            if (!KnownEvents.Contains(e))
                return (null, $"INVALID_EVENT: unknown event type '{e}'");
        }

        if (_subs.Count >= _maxSubs)
            return (null, "SUBSCRIPTION_LIMIT");

        var id = Guid.NewGuid().ToString("N")[..12];
        var sub = new EventSubscription(id, events, filterNodeId, filterProperty, _bufferLimit);
        _subs[id] = sub;

        TryWireAvaloniaSources();

        return (sub, null);
    }

    public bool Unsubscribe(string id) => _subs.TryRemove(id, out _);

    public EventSubscription? Get(string id) =>
        _subs.TryGetValue(id, out var s) ? s : null;

    /// <summary>
    /// Publishes an event to every matching subscription. Safe to call from any thread.
    /// </summary>
    public void Publish(string type, int? nodeId, string? property, object? data)
    {
        if (_subs.IsEmpty) return;
        var ts = DateTimeOffset.UtcNow;
        foreach (var sub in _subs.Values)
        {
            if (sub.Matches(type, nodeId, property))
            {
                sub.Enqueue(new EventEnvelope(sub.Id, type, ts, nodeId, data));
            }
        }
    }

    public async Task<IReadOnlyList<EventEnvelope>> PollAsync(
        string subscriptionId,
        int timeoutMs,
        CancellationToken ct = default)
    {
        if (!_subs.TryGetValue(subscriptionId, out var sub))
            throw new InvalidOperationException($"UNKNOWN_SUBSCRIPTION: {subscriptionId}");

        sub.TouchActivity();

        var initial = sub.Drain();
        if (initial.Count > 0) return initial;
        if (timeoutMs <= 0) return Array.Empty<EventEnvelope>();

        timeoutMs = Math.Min(timeoutMs, 60_000);

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(timeoutMs);
        try
        {
            await sub.WaitForEventAsync(cts.Token);
        }
        catch (OperationCanceledException) { }

        sub.TouchActivity();
        return sub.Drain();
    }

    public int ExpireStale()
    {
        var cutoff = DateTimeOffset.UtcNow - _ttl;
        var removed = 0;
        foreach (var (id, sub) in _subs)
        {
            if (sub.LastActivity < cutoff && _subs.TryRemove(id, out _))
                removed++;
        }
        return removed;
    }

    public void Reset()
    {
        _subs.Clear();
        lock (_hookLock)
        {
            _hookedWindows.Clear();
            _hookedVisuals.Clear();
            _wired = false;
            _windowScanTimer?.Dispose();
            _windowScanTimer = null;
        }
    }

    public void Dispose()
    {
        _expirationTimer?.Dispose();
        _windowScanTimer?.Dispose();
        _subs.Clear();
    }

    private void TryWireAvaloniaSources()
    {
        if (_wired) return;
        if (Application.Current is null) return;
        lock (_hookLock)
        {
            if (_wired) return;
            _wired = true;
        }

        try
        {
            Dispatcher.UIThread.Post(() =>
            {
                ScanAndHookWindows();
                _windowScanTimer ??= new Timer(_ =>
                {
                    try { Dispatcher.UIThread.Post(ScanAndHookWindows); }
                    catch { /* ignore */ }
                }, null, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(1));
            });
        }
        catch
        {
            // best-effort; tests can publish events directly
        }
    }

    private void ScanAndHookWindows()
    {
        try
        {
            if (Application.Current?.ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime desktop)
                return;

            foreach (var window in desktop.Windows)
                HookWindow(window);
        }
        catch { /* ignore */ }
    }

    private void HookWindow(Window window)
    {
        bool firstTime;
        lock (_hookLock)
            firstTime = _hookedWindows.Add(window);

        if (!firstTime) return;

        Publish(WindowOpened, NodeRegistry.GetOrRegister(window), null,
            new { title = window.Title, type = window.GetType().Name });

        window.Closed += (_, _) =>
            Publish(WindowClosed, NodeRegistry.GetOrRegister(window), null,
                new { title = window.Title });

        window.GotFocus += (_, _) =>
        {
            var focused = window.FocusManager?.GetFocusedElement();
            if (focused is Visual visual)
            {
                Publish(FocusChanged, NodeRegistry.GetOrRegister(visual), null,
                    new { type = visual.GetType().Name });
            }
        };

        HookPropertyChanged(window);
        foreach (var descendant in window.GetVisualDescendants())
            HookPropertyChanged(descendant);
    }

    private void HookPropertyChanged(Visual visual)
    {
        bool firstTime;
        lock (_hookLock)
            firstTime = _hookedVisuals.Add(visual);

        if (!firstTime) return;

        visual.PropertyChanged += OnAvaloniaPropertyChanged;
    }

    private void OnAvaloniaPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (sender is not Visual visual) return;
        var nodeId = NodeRegistry.GetOrRegister(visual);
        Publish(PropertyChanged, nodeId, e.Property.Name, new
        {
            property = e.Property.Name,
            oldValue = e.OldValue?.ToString(),
            newValue = e.NewValue?.ToString()
        });
    }
}
