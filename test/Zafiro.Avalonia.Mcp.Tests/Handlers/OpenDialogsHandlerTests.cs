using Avalonia.Controls;
using Avalonia.Threading;
using Zafiro.Avalonia.Mcp.AppHost.Handlers;
using Zafiro.Avalonia.Mcp.Protocol.Messages;
using System.Text.Json;
using Xunit;

namespace Zafiro.Avalonia.Mcp.Tests.Handlers;

[Collection("Avalonia")]
public class OpenDialogsHandlerTests
{
    private readonly OpenDialogsHandler _handler = new();

    public OpenDialogsHandlerTests(AvaloniaTestFixture _)
    {
        NodeRegistry.Clear();
    }

    [Fact]
    public async Task Handle_ReturnsEmptyArray_WhenNoDialogsOpen()
    {
        var result = await _handler.Handle(new DiagnosticRequest { Method = "get_open_dialogs" });
        var json = JsonSerializer.Serialize(result);

        // Result should be an empty collection or a list
        Assert.NotNull(result);
        var asJson = JsonDocument.Parse(json);
        Assert.Equal(JsonValueKind.Array, asJson.RootElement.ValueKind);
        Assert.Equal(0, asJson.RootElement.GetArrayLength());
    }

    [Fact]
    public async Task Handle_ReturnsDialog_WhenWindowHasOwner()
    {
        Window? owner = null;
        Window? dialog = null;

        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            owner = new Window { Title = "OwnerWindow" };
            dialog = new Window { Title = "DialogWindow" };
            owner.Show();
            // Simulate dialog by setting Owner (mirrors what ShowDialog does internally)
            dialog.Show(owner);
        });

        try
        {
            var result = await _handler.Handle(new DiagnosticRequest { Method = "get_open_dialogs" });
            var json = JsonSerializer.Serialize(result);
            var doc = JsonDocument.Parse(json);

            Assert.Equal(JsonValueKind.Array, doc.RootElement.ValueKind);
            Assert.True(doc.RootElement.GetArrayLength() >= 1, $"Expected at least one dialog, got: {json}");

            var first = doc.RootElement.EnumerateArray().First();
            Assert.True(first.TryGetProperty("title", out var titleProp));
            Assert.Equal("DialogWindow", titleProp.GetString());
            Assert.True(first.TryGetProperty("owner", out _));
            Assert.True(first.TryGetProperty("isModal", out var isModalProp));
            Assert.True(isModalProp.GetBoolean());
        }
        finally
        {
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                dialog?.Close();
                owner?.Close();
            });
        }
    }
}
