using System.Text.Json;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Threading;
using Xunit;
using Zafiro.Avalonia.Mcp.AppHost.Handlers;
using Zafiro.Avalonia.Mcp.Protocol;
using Zafiro.Avalonia.Mcp.Protocol.Messages;

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

        var failure = Assert.IsType<HandlerErrorResult>(result);
        Assert.Equal(DiagnosticErrorCodes.UnsupportedOperation, failure.Error.Code);
        Assert.True(pressed);
        Assert.True(released);
    }

    [Fact]
    public void ClickAndWait_PropagatesPointerFallbackFailureWithoutWrappingIt()
    {
        var (window, _) = CreateHostedTarget();

        try
        {
            var result = Handle(new ClickAndWaitHandler(), new
            {
                selector = "#Issue28Card",
                waitQuery = "TextBlock",
                timeoutMs = 5000
            });

            var failure = Assert.IsType<HandlerErrorResult>(result);
            Assert.Equal(DiagnosticErrorCodes.UnsupportedOperation, failure.Error.Code);
        }
        finally
        {
            Dispatcher.UIThread.Invoke(window.Close);
        }
    }

    [Fact]
    public void ClickAndWait_PropagatesSelectorResolutionFailureWithoutWrappingIt()
    {
        var result = Handle(new ClickAndWaitHandler(), new
        {
            selector = "#Missing",
            waitQuery = "TextBlock",
            timeoutMs = 5000
        });

        var json = JsonSerializer.SerializeToElement(result, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });
        Assert.False(json.TryGetProperty("success", out _));
        Assert.Equal(DiagnosticErrorCodes.NoMatch, json.GetProperty("code").GetString());
    }

    [Fact]
    public void ClickByQuery_PropagatesPointerFallbackFailureWithoutClaimingSuccess()
    {
        var (window, _) = CreateHostedTarget();

        try
        {
            var result = Handle(new ClickByQueryHandler(), new { query = "Issue28Card" });

            var failure = Assert.IsType<HandlerErrorResult>(result);
            Assert.Equal(DiagnosticErrorCodes.UnsupportedOperation, failure.Error.Code);
        }
        finally
        {
            Dispatcher.UIThread.Invoke(window.Close);
        }
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

    private static (Window window, PointerAwareControl target) CreateHostedTarget()
    {
        return Dispatcher.UIThread.Invoke(() =>
        {
            var target = CreateTarget();
            var window = new Window
            {
                Width = 120,
                Height = 80,
                IsVisible = true,
                Content = target
            };
            window.ApplyTemplate();
            window.Measure(new Size(120, 80));
            window.Arrange(new Rect(0, 0, 120, 80));
            NodeRegistry.GetOrRegister(window);
            return (window, target);
        });
    }

    private static object Handle(IRequestHandler handler, object parameters)
    {
        var task = handler.Handle(new DiagnosticRequest
        {
            Id = "test",
            Method = handler.Method,
            Params = JsonSerializer.SerializeToElement(parameters)
        });
        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(5);
        while (!task.IsCompleted && DateTime.UtcNow < deadline)
        {
            Dispatcher.UIThread.RunJobs();
            Thread.Sleep(1);
        }

        if (!task.IsCompleted)
            throw new TimeoutException($"{handler.GetType().Name} did not complete.");

        return task.GetAwaiter().GetResult();
    }

    private sealed class PointerAwareControl : Control;
}
