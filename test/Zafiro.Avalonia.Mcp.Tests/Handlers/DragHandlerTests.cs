using System.Text.Json;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Threading;
using Xunit;
using Zafiro.Avalonia.Mcp.AppHost.Handlers;
using Zafiro.Avalonia.Mcp.Protocol;
using Zafiro.Avalonia.Mcp.Protocol.Messages;

namespace Zafiro.Avalonia.Mcp.Tests.Handlers;

[Collection("Avalonia")]
public class DragHandlerTests
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    public DragHandlerTests(AvaloniaTestFixture _)
    {
        NodeRegistry.Clear();
    }

    [Fact]
    public void Drag_Thumb_RaisesDragEventsEndingAtTheWholeDistance() => Dispatcher.UIThread.Invoke(() =>
    {
        var thumb = new Thumb { Width = 10, Height = 10 };
        var started = false;
        var completed = false;
        Vector? lastDelta = null;
        thumb.DragStarted += (_, _) => started = true;
        thumb.DragDelta += (_, e) => lastDelta = e.Vector;
        thumb.DragCompleted += (_, _) => completed = true;
        var window = new Window { Width = 200, Height = 100, Content = new Canvas { Children = { thumb } } };
        window.Show();

        try
        {
            var result = DragHandler.Drag(thumb, start: null, new Vector(30, -12), MouseButton.Left, steps: 3);

            var json = JsonSerializer.SerializeToElement(result, JsonOptions);
            Assert.True(started);
            Assert.True(completed);
            // Thumb reports each delta from the press point, so the last one is the whole drag.
            Assert.Equal(new Vector(30, -12), lastDelta);
            Assert.Equal("pointer_drag", json.GetProperty("method").GetString());
            Assert.Equal(35, json.GetProperty("to").GetProperty("x").GetDouble());
        }
        finally
        {
            window.Close();
        }
    });

    [Fact]
    public void Drag_RoutesMovesAndReleaseToTheElementThatCapturedThePointer() => Dispatcher.UIThread.Invoke(() =>
    {
        var handle = new Border { Width = 20, Height = 20 };
        var surface = new Border { Width = 100, Height = 100, Child = handle };
        surface.AddHandler(InputElement.PointerPressedEvent, (_, e) => e.Pointer.Capture(surface));
        var handleMoves = 0;
        var surfaceMoves = 0;
        object? releaseSource = null;
        handle.AddHandler(InputElement.PointerMovedEvent, (_, _) => handleMoves++);
        surface.AddHandler(InputElement.PointerMovedEvent, (_, _) => surfaceMoves++);
        surface.AddHandler(InputElement.PointerReleasedEvent, (_, e) => releaseSource = e.Source);

        DragHandler.Drag(handle, start: null, new Vector(40, 0), MouseButton.Left, steps: 4);

        Assert.Equal(0, handleMoves);
        Assert.Equal(4, surfaceMoves);
        Assert.Same(surface, releaseSource);
    });

    [Fact]
    public void Drag_WithRightButton_KeepsTheRightButtonPressedWhileMoving() => Dispatcher.UIThread.Invoke(() =>
    {
        var target = new Border { Width = 50, Height = 50 };
        var movesWithRight = 0;
        MouseButton? releasedButton = null;
        target.AddHandler(InputElement.PointerMovedEvent, (_, e) =>
        {
            if (e.GetCurrentPoint(target).Properties.IsRightButtonPressed) movesWithRight++;
        });
        target.AddHandler(InputElement.PointerReleasedEvent, (_, e) => releasedButton = e.InitialPressMouseButton);

        DragHandler.Drag(target, new Point(5, 5), new Vector(10, 10), MouseButton.Right, steps: 2);

        Assert.Equal(2, movesWithRight);
        Assert.Equal(MouseButton.Right, releasedButton);
    });

    [Fact]
    public void Drag_PositionsAreRelativeToTheElement() => Dispatcher.UIThread.Invoke(() =>
    {
        var target = new Border { Width = 50, Height = 50 };
        var positions = new List<Point>();
        target.AddHandler(InputElement.PointerPressedEvent, (_, e) => positions.Add(e.GetPosition(target)));
        target.AddHandler(InputElement.PointerMovedEvent, (_, e) => positions.Add(e.GetPosition(target)));
        target.AddHandler(InputElement.PointerReleasedEvent, (_, e) => positions.Add(e.GetPosition(target)));
        var window = new Window
        {
            Width = 200,
            Height = 200,
            Content = new Border { Padding = new Thickness(30, 20), Child = target, HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top }
        };
        window.Show();

        try
        {
            DragHandler.Drag(target, new Point(5, 5), new Vector(20, 10), MouseButton.Left, steps: 2);

            Assert.Equal(new[] { new Point(5, 5), new Point(15, 10), new Point(25, 15), new Point(25, 15) }, positions);
        }
        finally
        {
            window.Close();
        }
    });

    [Fact]
    public void Drag_WithoutDistance_ReturnsInvalidParam()
    {
        var result = Handle(new { selector = "#Anything" });

        var failure = Assert.IsType<HandlerErrorResult>(result);
        Assert.Equal(DiagnosticErrorCodes.InvalidParam, failure.Error.Code);
    }

    [Fact]
    public void Drag_WithOnlyOneStartCoordinate_ReturnsInvalidParam()
    {
        var result = Handle(new { selector = "#Anything", deltaX = 10, startX = 3 });

        var failure = Assert.IsType<HandlerErrorResult>(result);
        Assert.Equal(DiagnosticErrorCodes.InvalidParam, failure.Error.Code);
    }

    private static object Handle(object parameters) =>
        new DragHandler().Handle(new DiagnosticRequest
        {
            Id = "test",
            Method = ProtocolMethods.Drag,
            Params = JsonSerializer.SerializeToElement(parameters)
        }).WaitAsync(TimeSpan.FromSeconds(5)).GetAwaiter().GetResult();
}
