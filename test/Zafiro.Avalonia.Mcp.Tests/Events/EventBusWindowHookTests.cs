using Avalonia.Controls;
using Avalonia.Threading;
using Xunit;
using Zafiro.Avalonia.Mcp.AppHost.Events;
using Zafiro.Avalonia.Mcp.AppHost.Handlers;

namespace Zafiro.Avalonia.Mcp.Tests.Events;

[Collection("Avalonia")]
public class EventBusWindowHookTests
{
    [Fact]
    public async Task HookWindow_PublishesWindowOpenedAndFocusChanged()
    {
        using var bus = new EventBus(TimeSpan.FromMinutes(5), maxSubs: 32, bufferLimit: 1000);
        var (sub, error) = bus.Subscribe(new[] { EventBus.WindowOpened, EventBus.FocusChanged });
        Assert.Null(error);

        var textBoxId = Dispatcher.UIThread.Invoke(() =>
        {
            var textBox = new TextBox();
            var window = new Window { Content = textBox };
            window.Show();
            try
            {
                bus.HookWindow(window);
                textBox.Focus();
                return NodeRegistry.GetOrRegister(textBox);
            }
            finally
            {
                window.Close();
            }
        });

        var events = await bus.PollAsync(sub!.Id, timeoutMs: 0);

        Assert.Contains(events, e => e.Type == EventBus.WindowOpened);
        Assert.Contains(events, e => e.Type == EventBus.FocusChanged && e.NodeId == textBoxId);
    }
}
