namespace Zafiro.Avalonia.Mcp.AppHost.Events;

/// <summary>
/// Envelope wrapping every event delivered to a subscriber.
/// </summary>
public sealed record EventEnvelope(
    string SubscriptionId,
    string Type,
    DateTimeOffset Timestamp,
    int? NodeId,
    object? Data);

/// <summary>
/// State for a single event subscription: bounded queue, optional filter,
/// long-poll waiter, last-activity timestamp for TTL expiration.
/// </summary>
public sealed class EventSubscription
{
    private readonly Queue<EventEnvelope> _buffer = new();
    private readonly object _lock = new();
    private TaskCompletionSource<bool>? _waiter;

    public string Id { get; }
    public IReadOnlySet<string> Events { get; }
    public int? FilterNodeId { get; }
    public string? FilterProperty { get; }
    public int BufferLimit { get; }
    public DateTimeOffset LastActivity { get; private set; }

    public EventSubscription(
        string id,
        IEnumerable<string> events,
        int? filterNodeId,
        string? filterProperty,
        int bufferLimit)
    {
        Id = id;
        Events = new HashSet<string>(events, StringComparer.OrdinalIgnoreCase);
        FilterNodeId = filterNodeId;
        FilterProperty = filterProperty;
        BufferLimit = Math.Max(1, bufferLimit);
        LastActivity = DateTimeOffset.UtcNow;
    }

    public bool Matches(string type, int? nodeId, string? property)
    {
        if (!Events.Contains(type)) return false;
        if (FilterNodeId.HasValue && FilterNodeId.Value != nodeId) return false;
        if (FilterProperty is not null &&
            !string.Equals(FilterProperty, property, StringComparison.OrdinalIgnoreCase))
            return false;
        return true;
    }

    public void Enqueue(EventEnvelope envelope)
    {
        TaskCompletionSource<bool>? waiter;
        lock (_lock)
        {
            while (_buffer.Count >= BufferLimit)
                _buffer.Dequeue();
            _buffer.Enqueue(envelope);
            waiter = _waiter;
            _waiter = null;
        }
        waiter?.TrySetResult(true);
    }

    public IReadOnlyList<EventEnvelope> Drain()
    {
        lock (_lock)
        {
            LastActivity = DateTimeOffset.UtcNow;
            if (_buffer.Count == 0) return Array.Empty<EventEnvelope>();
            var arr = _buffer.ToArray();
            _buffer.Clear();
            return arr;
        }
    }

    public Task<bool> WaitForEventAsync(CancellationToken ct)
    {
        TaskCompletionSource<bool> tcs;
        lock (_lock)
        {
            if (_buffer.Count > 0) return Task.FromResult(true);
            _waiter ??= new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            tcs = _waiter;
        }
        ct.Register(() => tcs.TrySetResult(false));
        return tcs.Task;
    }

    public void TouchActivity()
    {
        lock (_lock) LastActivity = DateTimeOffset.UtcNow;
    }

    internal int BufferCount
    {
        get { lock (_lock) return _buffer.Count; }
    }
}
