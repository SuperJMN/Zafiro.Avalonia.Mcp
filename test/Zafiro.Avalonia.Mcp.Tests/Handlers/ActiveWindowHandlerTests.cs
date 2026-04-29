using Avalonia.Controls;
using Avalonia.Threading;
using Zafiro.Avalonia.Mcp.AppHost.Handlers;
using Zafiro.Avalonia.Mcp.Protocol.Messages;
using System.Text.Json;
using Xunit;

namespace Zafiro.Avalonia.Mcp.Tests.Handlers;

[Collection("Avalonia")]
public class ActiveWindowHandlerTests
{
    private readonly ActiveWindowHandler _handler = new();

    public ActiveWindowHandlerTests(AvaloniaTestFixture _)
    {
        NodeRegistry.Clear();
    }

    [Fact]
    public async Task Handle_ReturnsOpenWindows_WithExpectedFields()
    {
        Window? window = null;

        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            window = new Window { Title = "TestWindow" };
            window.Show();
        });

        try
        {
            var result = await _handler.Handle(new DiagnosticRequest { Id = "test", Method = "get_active_window" });
            var json = JsonSerializer.Serialize(result);

            Assert.Contains("openWindows", json);
            Assert.Contains("TestWindow", json);
        }
        finally
        {
            await Dispatcher.UIThread.InvokeAsync(() => window?.Close());
        }
    }

    [Fact]
    public async Task Handle_ReturnsActiveWindowField()
    {
        var result = await _handler.Handle(new DiagnosticRequest { Id = "test", Method = "get_active_window" });
        var json = JsonSerializer.Serialize(result);

        Assert.Contains("activeWindow", json);
        Assert.Contains("openWindows", json);
    }

    [Fact]
    public async Task Handle_ListsAllOpenWindows()
    {
        Window? w1 = null, w2 = null;

        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            w1 = new Window { Title = "Window1" };
            w2 = new Window { Title = "Window2" };
            w1.Show();
            w2.Show();
        });

        try
        {
            var result = await _handler.Handle(new DiagnosticRequest { Id = "test", Method = "get_active_window" });
            var json = JsonSerializer.Serialize(result);

            Assert.Contains("Window1", json);
            Assert.Contains("Window2", json);
        }
        finally
        {
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                w1?.Close();
                w2?.Close();
            });
        }
    }
}
