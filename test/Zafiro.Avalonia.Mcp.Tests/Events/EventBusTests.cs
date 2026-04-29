using System.Diagnostics;
using Xunit;
using Zafiro.Avalonia.Mcp.AppHost.Events;

namespace Zafiro.Avalonia.Mcp.Tests.Events;

public class EventBusTests
{
    private static EventBus NewBus(TimeSpan? ttl = null, int maxSubs = 32, int bufferLimit = 1000)
        => new(ttl ?? TimeSpan.FromMinutes(5), maxSubs, bufferLimit);

    [Fact]
    public async Task EventBus_SubscribeAndPoll_DeliversBufferedEvent()
    {
        using var bus = NewBus();
        var (sub, error) = bus.Subscribe(new[] { EventBus.PropertyChanged });
        Assert.Null(error);
        Assert.NotNull(sub);

        bus.Publish(EventBus.PropertyChanged, nodeId: 1, property: "Text", data: new { newValue = "hello" });

        var events = await bus.PollAsync(sub!.Id, timeoutMs: 100);
        Assert.Single(events);
        Assert.Equal(EventBus.PropertyChanged, events[0].Type);
        Assert.Equal(1, events[0].NodeId);
    }

    [Fact]
    public async Task EventBus_LongPoll_ReturnsWhenEventArrives_BeforeTimeout()
    {
        using var bus = NewBus();
        var (sub, _) = bus.Subscribe(new[] { EventBus.PropertyChanged });

        var sw = Stopwatch.StartNew();
        var pollTask = bus.PollAsync(sub!.Id, timeoutMs: 5000);

        _ = Task.Run(async () =>
        {
            await Task.Delay(150);
            bus.Publish(EventBus.PropertyChanged, 7, "IsEnabled", new { v = true });
        });

        var events = await pollTask;
        sw.Stop();

        Assert.Single(events);
        Assert.Equal(7, events[0].NodeId);
        Assert.True(sw.ElapsedMilliseconds < 4500,
            $"Long-poll should return as soon as event arrives, took {sw.ElapsedMilliseconds}ms");
    }

    [Fact]
    public async Task EventBus_LongPoll_ReturnsEmpty_OnTimeout()
    {
        using var bus = NewBus();
        var (sub, _) = bus.Subscribe(new[] { EventBus.PropertyChanged });

        var sw = Stopwatch.StartNew();
        var events = await bus.PollAsync(sub!.Id, timeoutMs: 250);
        sw.Stop();

        Assert.Empty(events);
        Assert.True(sw.ElapsedMilliseconds >= 200,
            $"Long-poll should wait close to the timeout, took {sw.ElapsedMilliseconds}ms");
    }

    [Fact]
    public async Task EventBus_FilterByNodeId_DropsOthers()
    {
        using var bus = NewBus();
        var (sub, _) = bus.Subscribe(new[] { EventBus.PropertyChanged }, filterNodeId: 42);

        bus.Publish(EventBus.PropertyChanged, nodeId: 1, property: "X", data: null);
        bus.Publish(EventBus.PropertyChanged, nodeId: 42, property: "Y", data: null);
        bus.Publish(EventBus.PropertyChanged, nodeId: 99, property: "Z", data: null);

        var events = await bus.PollAsync(sub!.Id, 50);
        Assert.Single(events);
        Assert.Equal(42, events[0].NodeId);
    }

    [Fact]
    public async Task EventBus_FilterByProperty_DropsOthers()
    {
        using var bus = NewBus();
        var (sub, _) = bus.Subscribe(new[] { EventBus.PropertyChanged }, filterProperty: "IsChecked");

        bus.Publish(EventBus.PropertyChanged, nodeId: 1, property: "Text", data: null);
        bus.Publish(EventBus.PropertyChanged, nodeId: 2, property: "IsChecked", data: null);
        bus.Publish(EventBus.PropertyChanged, nodeId: 3, property: "ischecked", data: null); // case-insensitive

        var events = await bus.PollAsync(sub!.Id, 50);
        Assert.Equal(2, events.Count);
        Assert.All(events, e => Assert.Equal("IsChecked", e.Type == EventBus.PropertyChanged ? "IsChecked" : ""));
    }

    [Fact]
    public async Task EventBus_AutoExpires_AfterInactivity()
    {
        using var bus = NewBus(ttl: TimeSpan.FromMilliseconds(150));
        var (sub, _) = bus.Subscribe(new[] { EventBus.PropertyChanged });

        await Task.Delay(300);
        bus.ExpireStale();

        Assert.Null(bus.Get(sub!.Id));
        await Assert.ThrowsAsync<InvalidOperationException>(() => bus.PollAsync(sub.Id, 0));
    }

    [Fact]
    public void EventBus_SubscriptionLimit_ReturnsError()
    {
        using var bus = NewBus(maxSubs: 3);

        for (var i = 0; i < 3; i++)
        {
            var (s, err) = bus.Subscribe(new[] { EventBus.PropertyChanged });
            Assert.Null(err);
            Assert.NotNull(s);
        }

        var (sub, error) = bus.Subscribe(new[] { EventBus.PropertyChanged });
        Assert.Null(sub);
        Assert.Equal("SUBSCRIPTION_LIMIT", error);
    }

    [Fact]
    public async Task EventBus_Unsubscribe_StopsBuffering()
    {
        using var bus = NewBus();
        var (sub, _) = bus.Subscribe(new[] { EventBus.PropertyChanged });

        Assert.True(bus.Unsubscribe(sub!.Id));
        Assert.False(bus.Unsubscribe(sub.Id)); // idempotent / already removed

        bus.Publish(EventBus.PropertyChanged, 1, "X", null);

        await Assert.ThrowsAsync<InvalidOperationException>(() => bus.PollAsync(sub.Id, 0));
    }

    [Fact]
    public async Task Buffer_DropsOldest_WhenFull()
    {
        using var bus = NewBus(bufferLimit: 5);
        var (sub, _) = bus.Subscribe(new[] { EventBus.PropertyChanged });

        for (var i = 0; i < 12; i++)
            bus.Publish(EventBus.PropertyChanged, i, "X", new { i });

        var events = await bus.PollAsync(sub!.Id, 0);
        Assert.Equal(5, events.Count);
        // oldest dropped — first remaining nodeId should be 7 (12 published, 5 retained: 7..11)
        Assert.Equal(7, events[0].NodeId);
        Assert.Equal(11, events[^1].NodeId);
    }

    [Fact]
    public async Task EventBus_UnknownEventType_ReturnsError()
    {
        using var bus = NewBus();
        var (sub, error) = bus.Subscribe(new[] { "bogus_event" });
        Assert.Null(sub);
        Assert.Contains("INVALID_EVENT", error);
        await Task.CompletedTask;
    }

    [Fact]
    public async Task EventBus_Poll_ResetsActivityTimestamp()
    {
        using var bus = NewBus(ttl: TimeSpan.FromMilliseconds(300));
        var (sub, _) = bus.Subscribe(new[] { EventBus.PropertyChanged });

        await Task.Delay(150);
        await bus.PollAsync(sub!.Id, 0);          // should refresh activity
        await Task.Delay(200);                    // total 350ms since subscribe; only 200ms since poll
        bus.ExpireStale();

        Assert.NotNull(bus.Get(sub.Id));          // still alive thanks to the poll
    }
}
