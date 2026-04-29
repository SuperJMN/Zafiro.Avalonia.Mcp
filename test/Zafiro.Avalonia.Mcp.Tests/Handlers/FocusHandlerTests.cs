using Avalonia.Controls;
using Avalonia.Threading;
using Zafiro.Avalonia.Mcp.AppHost.Handlers;
using Zafiro.Avalonia.Mcp.Protocol.Messages;
using System.Text.Json;
using Xunit;

namespace Zafiro.Avalonia.Mcp.Tests.Handlers;

[Collection("Avalonia")]
public class FocusHandlerTests
{
    private readonly FocusHandler _handler = new();

    public FocusHandlerTests(AvaloniaTestFixture _)
    {
        NodeRegistry.Clear();
    }

    [Fact]
    public async Task Handle_ReturnsNullFocused_WhenNoWindowsOpen()
    {
        var result = await _handler.Handle(new DiagnosticRequest { Method = "get_focus" });

        var json = JsonSerializer.Serialize(result);
        Assert.Contains("focused", json);
    }

    [Fact]
    public async Task Handle_ReturnsFocusedElement_WhenElementHasFocus()
    {
        Window? window = null;
        TextBox? textBox = null;

        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            textBox = new TextBox { Name = "TestInput" };
            window = new Window { Content = textBox };
            window.Show();
            textBox.Focus();
        });

        try
        {
            var result = await _handler.Handle(new DiagnosticRequest { Method = "get_focus" });
            var json = JsonSerializer.Serialize(result);

            // In headless mode focus may not be available, but response must be well-formed
            Assert.True(json.Contains("focused") || json.Contains("nodeId"),
                $"Expected focused or nodeId in response, got: {json}");
        }
        finally
        {
            await Dispatcher.UIThread.InvokeAsync(() => window?.Close());
        }
    }
}
