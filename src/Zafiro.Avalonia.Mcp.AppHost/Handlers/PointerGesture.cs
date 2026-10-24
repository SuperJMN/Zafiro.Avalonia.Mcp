using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;

namespace Zafiro.Avalonia.Mcp.AppHost.Handlers;

/// <summary>
/// Raises the pointer events a real mouse produces for one press-move-release gesture with a given button.
/// The press goes to the target; like Avalonia's mouse device, the pointer is then captured (implicitly by the
/// target unless a handler captured it explicitly), so moves and the release go to the capturing element.
/// Positions are relative to the target.
/// </summary>
internal sealed class PointerGesture
{
    private readonly Control _target;
    private readonly MouseButton _button;
    private readonly Pointer _pointer = new(Pointer.GetNextFreeId(), PointerType.Mouse, isPrimary: true);
    private readonly Visual _root;
    private ulong _timestamp = unchecked((ulong)Environment.TickCount64);

    public PointerGesture(Control target, MouseButton button)
    {
        _target = target;
        _button = button;
        _root = FindRootVisual(target);
    }

    public static Point Center(Control control) => new(control.Bounds.Width / 2, control.Bounds.Height / 2);

    /// <summary>Returns whether a handler marked the press as handled.</summary>
    public bool Press(Point position)
    {
        var args = new PointerPressedEventArgs(
            _target,
            _pointer,
            _root,
            ToRoot(position),
            _timestamp++,
            new PointerPointProperties(PressedModifiers(), PressedUpdateKind()),
            KeyModifiers.None);
        _target.RaiseEvent(args);

        if (_pointer.Captured is null)
            _pointer.Capture(_target);

        return args.Handled;
    }

    /// <summary>Returns whether a handler marked the move as handled.</summary>
    public bool Move(Point position)
    {
        var receiver = Receiver();
        var args = new PointerEventArgs(
            InputElement.PointerMovedEvent,
            receiver,
            _pointer,
            _root,
            ToRoot(position),
            _timestamp++,
            new PointerPointProperties(PressedModifiers(), PointerUpdateKind.Other),
            KeyModifiers.None);
        receiver.RaiseEvent(args);
        return args.Handled;
    }

    /// <summary>Returns whether a handler marked the release as handled. Releases the pointer capture.</summary>
    public bool Release(Point position)
    {
        var receiver = Receiver();
        var args = new PointerReleasedEventArgs(
            receiver,
            _pointer,
            _root,
            ToRoot(position),
            _timestamp++,
            new PointerPointProperties(RawInputModifiers.None, ReleasedUpdateKind()),
            KeyModifiers.None,
            _button);

        try
        {
            receiver.RaiseEvent(args);
        }
        finally
        {
            _pointer.Capture(null);
        }

        return args.Handled;
    }

    private Interactive Receiver() => _pointer.Captured as Interactive ?? _target;

    // Positions are relative to the target even after another element captured the pointer, so a drag path
    // stays the one the caller asked for.
    private Point ToRoot(Point position) => _target.TransformToVisual(_root)?.Transform(position) ?? position;

    private RawInputModifiers PressedModifiers() => _button switch
    {
        MouseButton.Right => RawInputModifiers.RightMouseButton,
        MouseButton.Middle => RawInputModifiers.MiddleMouseButton,
        _ => RawInputModifiers.LeftMouseButton,
    };

    private PointerUpdateKind PressedUpdateKind() => _button switch
    {
        MouseButton.Right => PointerUpdateKind.RightButtonPressed,
        MouseButton.Middle => PointerUpdateKind.MiddleButtonPressed,
        _ => PointerUpdateKind.LeftButtonPressed,
    };

    private PointerUpdateKind ReleasedUpdateKind() => _button switch
    {
        MouseButton.Right => PointerUpdateKind.RightButtonReleased,
        MouseButton.Middle => PointerUpdateKind.MiddleButtonReleased,
        _ => PointerUpdateKind.LeftButtonReleased,
    };

    private static Visual FindRootVisual(Visual visual)
    {
        var current = visual;
        while (current.GetVisualParent() is Visual parent)
            current = parent;
        return current;
    }
}

internal static class MouseButtonParser
{
    public const string Accepted = "left, right, middle";

    /// <summary>Parses a "button" argument; a missing value means the left button.</summary>
    public static bool TryParse(string? value, out MouseButton button)
    {
        button = (value ?? "left").Trim().ToLowerInvariant() switch
        {
            "left" => MouseButton.Left,
            "right" => MouseButton.Right,
            "middle" => MouseButton.Middle,
            _ => MouseButton.None,
        };
        return button != MouseButton.None;
    }

    public static string Name(MouseButton button) => button.ToString().ToLowerInvariant();
}
