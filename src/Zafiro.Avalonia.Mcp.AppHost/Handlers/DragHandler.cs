using System.Text.Json;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Threading;
using Zafiro.Avalonia.Mcp.AppHost.Selectors;
using Zafiro.Avalonia.Mcp.Protocol;
using Zafiro.Avalonia.Mcp.Protocol.Messages;

namespace Zafiro.Avalonia.Mcp.AppHost.Handlers;

/// <summary>
/// Handles "drag" requests: presses a mouse button on an element, moves the pointer along a straight line in
/// several steps and releases it, so thumbs, splitters, sliders and pan surfaces see a real gesture.
/// </summary>
public sealed class DragHandler : IRequestHandler
{
    private const int DefaultSteps = 10;
    private const int MaxSteps = 100;

    public string Method => ProtocolMethods.Drag;

    public async Task<object> Handle(DiagnosticRequest request)
    {
        string? selector = null;
        string? buttonName = null;
        double? deltaX = null, deltaY = null, startX = null, startY = null;
        var steps = DefaultSteps;

        if (request.Params is JsonElement p)
        {
            if (p.TryGetProperty("selector", out var s)) selector = s.GetString();
            if (p.TryGetProperty("button", out var b)) buttonName = b.GetString();
            if (p.TryGetProperty("deltaX", out var dx)) deltaX = dx.GetDouble();
            if (p.TryGetProperty("deltaY", out var dy)) deltaY = dy.GetDouble();
            if (p.TryGetProperty("startX", out var sx)) startX = sx.GetDouble();
            if (p.TryGetProperty("startY", out var sy)) startY = sy.GetDouble();
            if (p.TryGetProperty("steps", out var st)) steps = st.GetInt32();
        }

        if (!MouseButtonParser.TryParse(buttonName, out var button))
        {
            return HandlerResult.Error(
                DiagnosticErrorCodes.InvalidParam,
                $"Unknown mouse button '{buttonName}'.",
                $"Use one of: {MouseButtonParser.Accepted}.");
        }

        if (deltaX is null && deltaY is null)
        {
            return HandlerResult.Error(
                DiagnosticErrorCodes.InvalidParam,
                "A drag needs a distance.",
                "Pass deltaX and/or deltaY (pixels relative to the start point).");
        }

        if (startX.HasValue != startY.HasValue)
        {
            return HandlerResult.Error(
                DiagnosticErrorCodes.InvalidParam,
                "startX and startY must be passed together.",
                "Pass both, or neither to start at the element's centre.");
        }

        var delta = new Vector(deltaX ?? 0, deltaY ?? 0);
        var start = startX.HasValue ? new Point(startX.Value, startY!.Value) : (Point?)null;
        steps = Math.Clamp(steps, 1, MaxSteps);

        return await Dispatcher.UIThread.InvokeAsync<object>(() =>
        {
            var (visual, error) = SelectorRequestHelper.ResolveSingle(selector);
            if (visual is null) return error!;
            return Drag(visual, start, delta, button, steps);
        });
    }

    /// <summary>
    /// Performs the drag on the UI thread. <paramref name="start"/> is relative to the element and defaults to
    /// its centre. Exposed for tests so they don't need to spin a Dispatcher.
    /// </summary>
    internal static object Drag(Visual visual, Point? start, Vector delta, MouseButton button, int steps = DefaultSteps)
    {
        var nodeId = NodeRegistry.GetOrRegister(visual);

        if (visual is not Control control)
        {
            return HandlerResult.Error(
                DiagnosticErrorCodes.UnsupportedOperation,
                $"Cannot drag element type '{visual.GetType().Name}'.",
                null,
                new { nodeId, elementType = visual.GetType().Name });
        }

        if (!control.IsEffectivelyEnabled)
        {
            return HandlerResult.Error(
                DiagnosticErrorCodes.UnsupportedOperation,
                $"Cannot drag disabled element '{control.GetType().Name}'.",
                "Enable the element (including its ancestors) before dragging it.",
                new { nodeId, elementType = control.GetType().Name });
        }

        var from = start ?? PointerGesture.Center(control);
        var to = from + delta;
        var gesture = new PointerGesture(control, button);

        var pressHandled = gesture.Press(from);
        var moveHandled = false;
        for (var i = 1; i <= steps; i++)
            moveHandled |= gesture.Move(from + delta * i / steps);
        var releaseHandled = gesture.Release(to);

        var buttonName = MouseButtonParser.Name(button);
        var warning = pressHandled || moveHandled || releaseHandled
            ? null
            : $"No handler marked the {buttonName}-button press, moves or release as handled; verify the resulting UI state explicitly.";

        return new
        {
            success = true,
            nodeId,
            method = "pointer_drag",
            button = buttonName,
            from = new { x = from.X, y = from.Y },
            to = new { x = to.X, y = to.Y },
            steps,
            handled = new { press = pressHandled, move = moveHandled, release = releaseHandled },
            warning,
        };
    }
}
