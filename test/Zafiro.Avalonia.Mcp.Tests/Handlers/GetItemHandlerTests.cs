using System.Text.Json;
using Avalonia.Controls;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Zafiro.Avalonia.Mcp.AppHost.Handlers;
using Zafiro.Avalonia.Mcp.Protocol.Messages;
using Xunit;

namespace Zafiro.Avalonia.Mcp.Tests.Handlers;

[Collection("Avalonia")]
public class GetItemHandlerTests
{
    private readonly GetItemHandler _handler = new();

    public GetItemHandlerTests(AvaloniaTestFixture _)
    {
        NodeRegistry.Clear();
    }

    private static DiagnosticRequest MakeRequest(object parms) =>
        new() { Id = "test", Method = "get_item", Params = JsonSerializer.SerializeToElement(parms) };

    private static Window BuildListBoxWindow(int itemCount, out ListBox listBox)
    {
        var items = Enumerable.Range(0, itemCount).Select(i => $"Item {i}").ToList();
        var lb = new ListBox { Items = { }, Width = 200, Height = 300 };
        foreach (var item in items) lb.Items.Add(item);
        var window = new Window { Content = lb, Width = 400, Height = 400 };
        window.Show();
        lb.UpdateLayout();
        listBox = lb;
        return window;
    }

    [Fact(Skip = "Window.Show() hangs in headless SetupWithoutStarting() mode — pre-existing project-wide limitation.")]
    public async Task GetItem_ByIndex_ReturnsContainer()
    {
        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            var window = BuildListBoxWindow(10, out var lb);
            var lbId = NodeRegistry.GetOrRegister(lb);
            window.UpdateLayout();
        });

        var lb2 = await Dispatcher.UIThread.InvokeAsync(() =>
        {
            // Grab the listbox via registry to build the selector
            foreach (var w in NodeRegistry.GetWindows())
            {
                var found = w.FindDescendantOfType<ListBox>();
                if (found != null) return found;
            }
            return null;
        });

        Assert.NotNull(lb2);
        var lbNodeId = NodeRegistry.GetOrRegister(lb2!);

        var result = await _handler.Handle(MakeRequest(new { selector = lbNodeId.ToString(), index = 3 }));
        var json = JsonSerializer.Serialize(result);
        var doc = JsonDocument.Parse(json).RootElement;

        Assert.False(doc.TryGetProperty("error", out _), $"Unexpected error: {json}");
        Assert.Equal(3, doc.GetProperty("index").GetInt32());
        Assert.True(doc.GetProperty("isRealized").GetBoolean());
        Assert.True(doc.GetProperty("nodeId").GetInt32() > 0);
    }

    [Fact(Skip = "Window.Show() hangs in headless SetupWithoutStarting() mode — pre-existing project-wide limitation.")]
    public async Task GetItem_ByText_ReturnsMatchingContainer()
    {
        ListBox? capturedLb = null;

        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            var window = BuildListBoxWindow(10, out var lb);
            capturedLb = lb;
            window.UpdateLayout();
        });

        var lbNodeId = NodeRegistry.GetOrRegister(capturedLb!);

        var result = await _handler.Handle(MakeRequest(new { selector = lbNodeId.ToString(), text = "Item 7" }));
        var json = JsonSerializer.Serialize(result);
        var doc = JsonDocument.Parse(json).RootElement;

        Assert.False(doc.TryGetProperty("error", out _), $"Unexpected error: {json}");
        Assert.Equal(7, doc.GetProperty("index").GetInt32());
        Assert.True(doc.GetProperty("isRealized").GetBoolean());
    }

    [Fact(Skip = "Window.Show() hangs in headless SetupWithoutStarting() mode — pre-existing project-wide limitation.")]
    public async Task GetItem_ByDcMatch_ReturnsMatchingContainer()
    {
        ListBox? capturedLb = null;

        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            var lb = new ListBox { Width = 200, Height = 300 };
            lb.Items.Add(new ItemVm { Name = "Alpha", Value = 1 });
            lb.Items.Add(new ItemVm { Name = "Beta", Value = 2 });
            lb.Items.Add(new ItemVm { Name = "Gamma", Value = 3 });
            var window = new Window { Content = lb, Width = 400, Height = 400 };
            window.Show();
            lb.UpdateLayout();
            capturedLb = lb;
        });

        var lbNodeId = NodeRegistry.GetOrRegister(capturedLb!);

        var result = await _handler.Handle(MakeRequest(new
        {
            selector = lbNodeId.ToString(),
            dcMatch = new { path = "Name", value = "Beta" }
        }));
        var json = JsonSerializer.Serialize(result);
        var doc = JsonDocument.Parse(json).RootElement;

        Assert.False(doc.TryGetProperty("error", out _), $"Unexpected error: {json}");
        Assert.Equal(1, doc.GetProperty("index").GetInt32());
        Assert.True(doc.GetProperty("isRealized").GetBoolean());
    }

    [Fact(Skip = "Window.Show() hangs in headless SetupWithoutStarting() mode — pre-existing project-wide limitation.")]
    public async Task GetItem_NotFound_ReturnsError()
    {
        ListBox? capturedLb = null;

        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            var window = BuildListBoxWindow(5, out var lb);
            capturedLb = lb;
            window.UpdateLayout();
        });

        var lbNodeId = NodeRegistry.GetOrRegister(capturedLb!);

        var result = await _handler.Handle(MakeRequest(new { selector = lbNodeId.ToString(), text = "ZZZ_no_match" }));
        var json = JsonSerializer.Serialize(result);
        var doc = JsonDocument.Parse(json).RootElement;

        Assert.True(doc.TryGetProperty("error", out _), $"Expected error but got: {json}");
    }

    [Fact(Skip = "Window.Show() hangs in headless SetupWithoutStarting() mode — pre-existing project-wide limitation.")]
    public async Task GetItem_IndexOutOfRange_ReturnsError()
    {
        ListBox? capturedLb = null;

        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            var window = BuildListBoxWindow(3, out var lb);
            capturedLb = lb;
            window.UpdateLayout();
        });

        var lbNodeId = NodeRegistry.GetOrRegister(capturedLb!);

        var result = await _handler.Handle(MakeRequest(new { selector = lbNodeId.ToString(), index = 999 }));
        var json = JsonSerializer.Serialize(result);
        var doc = JsonDocument.Parse(json).RootElement;

        Assert.True(doc.TryGetProperty("error", out _), $"Expected error but got: {json}");
    }

    [Fact(Skip = "Handler invokes Dispatcher.UIThread.InvokeAsync which deadlocks in headless SetupWithoutStarting() mode — pre-existing project-wide limitation.")]
    public async Task GetItem_MissingSelector_ReturnsError()
    {
        var result = await _handler.Handle(MakeRequest(new { index = 0 }));
        var json = JsonSerializer.Serialize(result);
        var doc = JsonDocument.Parse(json).RootElement;

        Assert.True(doc.TryGetProperty("error", out _), $"Expected error but got: {json}");
    }

    private sealed class ItemVm
    {
        public string Name { get; set; } = "";
        public int Value { get; set; }
        public override string ToString() => Name;
    }
}
