using System.Text.Json;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.Platform;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Xunit;
using Zafiro.Avalonia.Mcp.AppHost.Handlers;
using Zafiro.Avalonia.Mcp.AppHost.Selectors;
using Zafiro.Avalonia.Mcp.Protocol;
using Zafiro.Avalonia.Mcp.Protocol.Messages;

namespace Zafiro.Avalonia.Mcp.Tests.Handlers;

[Collection("Avalonia")]
public class PopupRootExposureTests
{
    private const string PopupItemText = "Delete From Internal Storage";

    public PopupRootExposureTests(AvaloniaTestFixture _)
    {
        NodeRegistry.Clear();
    }

    [Fact]
    public void GetInspectableRoots_IncludesShownPopupRoot()
    {
        var fixture = Dispatcher.UIThread.Invoke(CreateInspectablePopup);
        var popup = fixture.Popup;

        try
        {
            var roots = Dispatcher.UIThread.Invoke(() => NodeRegistry.GetInspectableRoots().ToArray());

            Assert.Contains(popup, roots);
        }
        finally
        {
            DisposePopup(fixture);
        }
    }

    [Fact]
    public void SelectorDefaultScope_FindsOpenPopupMenuItem()
    {
        var fixture = Dispatcher.UIThread.Invoke(CreateInspectablePopup);
        var popup = fixture.Popup;

        try
        {
            var menuItem = Dispatcher.UIThread.Invoke(() =>
                popup.GetVisualDescendants().OfType<MenuItem>().Single());

            var matches = Dispatcher.UIThread.Invoke(() => SelectorEngine.Default.Resolve("MenuItem"));

            Assert.Contains(menuItem, matches);
        }
        finally
        {
            DisposePopup(fixture);
        }
    }

    [Fact]
    public void SelectorDefaultScope_MatchesOpenPopupMenuItemHeaderText()
    {
        var fixture = Dispatcher.UIThread.Invoke(CreateInspectablePopup);
        var popup = fixture.Popup;

        try
        {
            var menuItem = Dispatcher.UIThread.Invoke(() =>
                popup.GetVisualDescendants().OfType<MenuItem>().Single());

            var matches = Dispatcher.UIThread.Invoke(() =>
                SelectorEngine.Default.Resolve($"MenuItem:has-text(\"{PopupItemText}\")"));

            Assert.Contains(menuItem, matches);
        }
        finally
        {
            DisposePopup(fixture);
        }
    }

    [Fact]
    public void NodeInfoBuilder_UsesMenuItemHeaderAsText()
    {
        var info = Dispatcher.UIThread.Invoke(() =>
            NodeInfoBuilder.Create(new MenuItem { Header = PopupItemText }));

        Assert.Equal("menuitem", info.Role);
        Assert.Equal(PopupItemText, info.Text);
    }

    [Fact]
    public void Search_FindsOpenPopupMenuItemHeaderText()
    {
        var fixture = Dispatcher.UIThread.Invoke(CreateInspectablePopup);

        try
        {
            var result = Handle(new SearchHandler(), Request(ProtocolMethods.Search, new { query = PopupItemText }));
            var json = Serialize(result);

            Assert.Contains(json.EnumerateArray(), item =>
                item.GetProperty("type").GetString() == nameof(MenuItem) &&
                item.GetProperty("text").GetString() == PopupItemText);
        }
        finally
        {
            DisposePopup(fixture);
        }
    }

    [Fact]
    public void GetInteractables_IncludesOpenPopupMenuItemHeaderText()
    {
        var fixture = Dispatcher.UIThread.Invoke(CreateInspectablePopup);

        try
        {
            var result = Handle(new InteractablesHandler(), Request(ProtocolMethods.GetInteractables, new { }));
            var json = Serialize(result);

            Assert.Contains(json.EnumerateArray(), item =>
                item.GetProperty("role").GetString() == "menuitem" &&
                item.GetProperty("text").GetString() == PopupItemText);
        }
        finally
        {
            DisposePopup(fixture);
        }
    }

    [Fact]
    public void ClickByQuery_FindsOpenPopupMenuItemHeaderText()
    {
        var fixture = Dispatcher.UIThread.Invoke(CreateInspectablePopup);

        try
        {
            var result = Handle(new ClickByQueryHandler(), Request(ProtocolMethods.ClickByQuery, new
            {
                query = PopupItemText,
                role = "menuitem"
            }));
            var json = Serialize(result);

            Assert.True(json.GetProperty("success").GetBoolean());
            Assert.Equal(nameof(MenuItem), json.GetProperty("type").GetString());
            Assert.Equal(PopupItemText, json.GetProperty("text").GetString());
            Assert.Equal("menu_click", json.GetProperty("clickResult").GetString());
        }
        finally
        {
            Dispatcher.UIThread.Invoke(fixture.Dispose);
        }
    }

    [Fact]
    public void GetScreenText_IncludesOpenPopupMenuItemHeaderText()
    {
        var fixture = Dispatcher.UIThread.Invoke(CreateInspectablePopup);

        try
        {
            var result = Handle(new ScreenTextHandler(), Request(ProtocolMethods.GetScreenText, new { visibleOnly = false }));
            var json = Serialize(result);

            Assert.Contains(PopupItemText, json.GetProperty("plainText").GetString());
        }
        finally
        {
            DisposePopup(fixture);
        }
    }

    [Fact]
    public void GetTree_IncludesOpenPopupRoot()
    {
        var fixture = Dispatcher.UIThread.Invoke(CreateInspectablePopup);

        try
        {
            var result = Handle(new TreeHandler(), Request(ProtocolMethods.GetTree, new { treeKind = "Merged", depth = 4 }));
            var json = Serialize(result);

            Assert.Contains(json.EnumerateArray(), item =>
                item.GetProperty("type").GetString() == nameof(PopupRoot));
            Assert.Contains(PopupItemText, json.ToString());
        }
        finally
        {
            DisposePopup(fixture);
        }
    }

    [Fact]
    public void ScreenshotWithoutSelector_CapturesOpenPopupRootAboveWindow()
    {
        var fixture = Dispatcher.UIThread.Invoke(CreateInspectableWindowAndPopupRoot);
        var popup = fixture.Popup;

        try
        {
            var result = Handle(new ScreenshotHandler(), new DiagnosticRequest
            {
                Id = "test",
                Method = ProtocolMethods.Screenshot
            });
            var json = Serialize(result);

            Assert.False(json.TryGetProperty("error", out var error), error.ToString());
            Assert.Equal(NodeRegistry.GetOrRegister(popup), json.GetProperty("nodeId").GetInt32());
        }
        finally
        {
            DisposePopup(fixture);
        }
    }

    private static void DisposePopup(PopupFixture fixture)
    {
        Dispatcher.UIThread.Invoke(fixture.Dispose);
        Dispatcher.UIThread.RunJobs();
    }

    private static PopupFixture CreateInspectableWindowAndPopupRoot()
    {
        var window = new Window
        {
            Width = 240,
            Height = 120,
            IsVisible = true,
            Content = new Button { Content = "Actions" }
        };
        window.ApplyTemplate();
        window.Measure(new Size(240, 120));
        window.Arrange(new Rect(0, 0, 240, 120));
        NodeRegistry.GetOrRegister(window);

        return CreateInspectablePopup(window);
    }

    private static PopupFixture CreateInspectablePopup()
    {
        return CreateInspectablePopup(new Window { Width = 240, Height = 120 });
    }

    private static PopupFixture CreateInspectablePopup(Window window)
    {
        var popupImpl = CreateHeadlessPopupImpl();
        var popup = new PopupRoot(window, popupImpl)
        {
            Width = 220,
            Height = 80,
            IsVisible = true
        };
        popup.SetChild(new MenuItem { Header = PopupItemText, Focusable = true });
        popup.ApplyTemplate();
        popup.Measure(new Size(220, 80));
        popup.Arrange(new Rect(0, 0, 220, 80));
        NodeRegistry.GetOrRegister(popup);
        return new PopupFixture(window, popup, popupImpl);
    }

    private static JsonElement Serialize(object result) =>
        JsonSerializer.SerializeToElement(result, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

    private static object Handle(IRequestHandler handler, DiagnosticRequest request)
    {
        var task = handler.Handle(request);
        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(5);
        while (!task.IsCompleted && DateTime.UtcNow < deadline)
        {
            Dispatcher.UIThread.RunJobs();
            Thread.Sleep(1);
        }

        if (!task.IsCompleted)
        {
            throw new TimeoutException($"{handler.GetType().Name} did not complete.");
        }

        return task.GetAwaiter().GetResult();
    }

    private static DiagnosticRequest Request(string method, object parameters) => new()
    {
        Id = "test",
        Method = method,
        Params = JsonSerializer.SerializeToElement(parameters)
    };

    private static IPopupImpl CreateHeadlessPopupImpl()
    {
        var type = typeof(AvaloniaHeadlessPlatform).Assembly.GetType("Avalonia.Headless.HeadlessWindowImpl", throwOnError: true)!;
        return (IPopupImpl)Activator.CreateInstance(
            type,
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic,
            binder: null,
            args: [true, PixelFormat.Rgba8888],
            culture: null)!;
    }

    private sealed class PopupFixture(Window owner, PopupRoot popup, IPopupImpl popupImpl) : IDisposable
    {
        public PopupRoot Popup { get; } = popup;

        public void Dispose()
        {
            Popup.Dispose();

            if (popupImpl is IDisposable disposablePopup)
            {
                disposablePopup.Dispose();
            }

            owner.Close();
        }
    }
}
