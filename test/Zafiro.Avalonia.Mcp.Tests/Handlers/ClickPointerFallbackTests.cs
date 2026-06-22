using System.Reflection;
using System.Text.Json;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Xunit;
using Zafiro.Avalonia.Mcp.AppHost.Handlers;

namespace Zafiro.Avalonia.Mcp.Tests.Handlers;

[Collection("Avalonia")]
public class ClickPointerFallbackTests
{
    public ClickPointerFallbackTests(AvaloniaTestFixture _)
    {
        NodeRegistry.Clear();
    }

    [Fact]
    public void ClickAndWait_DoesNotThrow_WhenPointerFallbackTargetsNonButtonControl()
    {
        var target = CreateTarget();
        var pressed = false;
        var released = false;

        target.AddHandler(InputElement.PointerPressedEvent, (_, e) =>
        {
            var pointerArgs = Assert.IsType<PointerPressedEventArgs>(e);
            _ = pointerArgs.GetCurrentPoint(target);
            pressed = true;
        });
        target.AddHandler(InputElement.PointerReleasedEvent, (_, e) =>
        {
            var pointerArgs = Assert.IsType<PointerReleasedEventArgs>(e);
            Assert.Equal(MouseButton.Left, pointerArgs.InitialPressMouseButton);
            released = true;
        });

        var result = ClickAndWaitHandler.PerformClick(target);

        var json = JsonSerializer.SerializeToElement(result, JsonOptions);
        Assert.True(json.GetProperty("success").GetBoolean());
        Assert.Equal("pointer_simulation", json.GetProperty("method").GetString());
        Assert.True(pressed);
        Assert.True(released);
    }

    [Fact]
    public void ClickByQuery_DoesNotThrow_WhenPointerFallbackTargetsNonButtonControl()
    {
        var target = CreateTarget();
        var pressed = false;
        var released = false;

        target.AddHandler(InputElement.PointerPressedEvent, (_, e) =>
        {
            var pointerArgs = Assert.IsType<PointerPressedEventArgs>(e);
            _ = pointerArgs.GetCurrentPoint(target);
            pressed = true;
        });
        target.AddHandler(InputElement.PointerReleasedEvent, (_, e) =>
        {
            var pointerArgs = Assert.IsType<PointerReleasedEventArgs>(e);
            Assert.Equal(MouseButton.Left, pointerArgs.InitialPressMouseButton);
            released = true;
        });

        var result = InvokeClickByQueryPerformClick(target);

        Assert.Equal("pointer_simulation", result);
        Assert.True(pressed);
        Assert.True(released);
    }

    private static PointerAwareControl CreateTarget()
    {
        var target = new PointerAwareControl
        {
            Name = "Issue28Card",
            Focusable = true,
            Width = 40,
            Height = 40
        };

        target.Measure(new Size(40, 40));
        target.Arrange(new Rect(0, 0, 40, 40));

        return target;
    }

    private static string InvokeClickByQueryPerformClick(PointerAwareControl target)
    {
        var method = typeof(ClickByQueryHandler).GetMethod("PerformClick", BindingFlags.Static | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("ClickByQueryHandler.PerformClick was not found.");
        var click = method.CreateDelegate<Func<Visual, string>>();
        return click(target);
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private sealed class PointerAwareControl : Control;
}
