using System.ComponentModel;
using Zafiro.Avalonia.Mcp.Protocol;
using Zafiro.Avalonia.Mcp.Tool.Connection;
using ModelContextProtocol;
using ModelContextProtocol.Server;

namespace Zafiro.Avalonia.Mcp.Tool.Tools;

[McpServerToolType]
public sealed class InputTools
{
    private const string SelectorDescription =
        "Selector identifying the element. Examples: '#42' (nodeId), '#SaveBtn' (x:Name), 'Button[Content=\"Save\"]', 'ListBox >> ListBoxItem:nth(2)'.";

    private const string ButtonDescription = "Mouse button: left (default), right or middle.";

    [McpServerTool(Name = "click"), Description("""
        Click a UI element by selector. A left click (default) handles Button (invokes command), ToggleButton (toggles), ListBoxItem (selects), TabItem (switches), MenuItem (invokes); falls back to pointer simulation for anything else. button='right' or 'middle' always raises a real pointer press/release on the element (right opens its ContextMenu/ContextFlyout through ContextRequested). For lists/tabs prefer select_item, for checkboxes prefer toggle, for "click the X button" prefer click_by_query.
        Returns: confirmation with the semantic invoked (method); right/middle clicks also report handled, contextMenuOpen (right) and a warning when no handler reacted.
        Example: {"success":true,"method":"pointer","button":"right","handled":true,"contextMenuOpen":true}
        """)]
    public static async Task<string> Click(
        ConnectionPool pool,
        [Description(SelectorDescription)] string selector,
        [Description(ButtonDescription)] string? button = null)
    {
        var conn = pool.GetActive();
        var parms = new Dictionary<string, object?> { ["selector"] = selector };
        if (button is not null) parms["button"] = button;
        return await conn.InvokeAsync(ProtocolMethods.Click, parms);
    }

    [McpServerTool(Name = "drag"), Description("""
        Press a mouse button on an element, move the pointer in a straight line and release it (press-and-drag gesture). Use for Thumb, GridSplitter, Slider thumbs, ScrollBar thumbs, resize handles and pan/drag surfaces. Events go to the element and then to whatever captures the pointer, as with real input; OS drag-and-drop (DragDrop.DoDragDrop) is not simulated.
        Returns: start/end points (element-relative), steps, and which phases a handler marked as handled; a warning when none did.
        Example: {"success":true,"method":"pointer_drag","button":"left","from":{"x":4,"y":50},"to":{"x":124,"y":50},"steps":10,"handled":{"press":true,"move":true,"release":true}}
        """)]
    public static async Task<string> Drag(
        ConnectionPool pool,
        [Description(SelectorDescription)] string selector,
        [Description("Horizontal distance to drag, in pixels (positive = right). Omit for 0.")] double? deltaX = null,
        [Description("Vertical distance to drag, in pixels (positive = down). Omit for 0.")] double? deltaY = null,
        [Description("Start X relative to the element's top-left corner. Omit (with startY) to start at the centre.")] double? startX = null,
        [Description("Start Y relative to the element's top-left corner. Omit (with startX) to start at the centre.")] double? startY = null,
        [Description(ButtonDescription)] string? button = null,
        [Description("Number of intermediate pointer moves (1-100, default 10).")] int? steps = null)
    {
        var conn = pool.GetActive();
        var parms = new Dictionary<string, object?> { ["selector"] = selector };
        if (deltaX is not null) parms["deltaX"] = deltaX;
        if (deltaY is not null) parms["deltaY"] = deltaY;
        if (startX is not null) parms["startX"] = startX;
        if (startY is not null) parms["startY"] = startY;
        if (button is not null) parms["button"] = button;
        if (steps is not null) parms["steps"] = steps;
        return await conn.InvokeAsync(ProtocolMethods.Drag, parms);
    }

    [McpServerTool(Name = "tap"), Description("""
        Single-finger touch on an element. Same semantics as click but raises tap/touch events instead of pointer events; use only when testing touch-specific behavior.
        Returns: confirmation string.
        Example: "Tapped Button 'Save'"
        """)]
    public static async Task<string> Tap(
        ConnectionPool pool,
        [Description(SelectorDescription)] string selector)
    {
        var conn = pool.GetActive();
        return await conn.InvokeAsync(ProtocolMethods.Tap, new { selector });
    }

    [McpServerTool(Name = "key_down"), Description("""
        Send a key down event to a focused element. Use for keyboard shortcuts (modifiers='ctrl+shift', key='S') or navigation keys. For typing text into a TextBox, use text_input instead.
        Returns: confirmation string.
        Example: "KeyDown sent: Ctrl+S"
        """)]
    public static async Task<string> KeyDown(
        ConnectionPool pool,
        [Description(SelectorDescription)] string selector,
        [Description("Key name (e.g., Enter, Tab, Escape, A, B, Space)")] string key,
        [Description("Key modifiers (e.g., ctrl, shift, alt, ctrl+shift). Omit for none.")] string? modifiers = null)
    {
        var conn = pool.GetActive();
        var parms = new Dictionary<string, object?> { ["selector"] = selector, ["key"] = key };
        if (modifiers is not null) parms["modifiers"] = modifiers;
        return await conn.InvokeAsync(ProtocolMethods.KeyDown, parms);
    }

    [McpServerTool(Name = "key_up"), Description("""
        Send a key up event. Pair with key_down to simulate held keys; for a simple keypress, key_down alone is usually enough since most controls react on key down.
        Returns: confirmation string.
        Example: "KeyUp sent: Shift"
        """)]
    public static async Task<string> KeyUp(
        ConnectionPool pool,
        [Description(SelectorDescription)] string selector,
        [Description("Key name")] string key,
        [Description("Key modifiers (e.g., ctrl, shift, alt, ctrl+shift). Omit for none.")] string? modifiers = null)
    {
        var conn = pool.GetActive();
        var parms = new Dictionary<string, object?> { ["selector"] = selector, ["key"] = key };
        if (modifiers is not null) parms["modifiers"] = modifiers;
        return await conn.InvokeAsync(ProtocolMethods.KeyUp, parms);
    }

    [McpServerTool(Name = "text_input"), Description("""
        Replace the text of a TextBox (or container that hosts one, e.g. AutoCompleteBox). Set pressEnter=true for search boxes/forms that submit on Enter. For keyboard shortcuts use key_down instead.
        Returns: confirmation string with the value set.
        Example: "Set text to 'hello@example.com' on TextBox#12"
        """)]
    public static async Task<string> TextInput(
        ConnectionPool pool,
        [Description(SelectorDescription)] string selector,
        [Description("Text to enter")] string text,
        [Description("Whether to simulate pressing Enter after text input")] bool pressEnter = false)
    {
        var conn = pool.GetActive();
        return await conn.InvokeAsync(ProtocolMethods.TextInput, new { selector, text, pressEnter });
    }

    [McpServerTool(Name = "action"), Description("""
        Perform a non-input UI action on an element: Focus | Enable | Disable | BringIntoView. Use BringIntoView before interacting with off-screen items, Focus before sending key_down.
        Returns: confirmation string.
        Example: "Action 'BringIntoView' performed on ListBoxItem#42"
        """)]
    public static async Task<string> Action(
        ConnectionPool pool,
        [Description(SelectorDescription)] string selector,
        [Description("Action to perform: Focus, Enable, Disable, BringIntoView")] string action)
    {
        var conn = pool.GetActive();
        return await conn.InvokeAsync(ProtocolMethods.Action, new { selector, action });
    }

    [McpServerTool(Name = "pseudo_class"), Description("""
        Inspect or override pseudo-classes on an element (:pointerover, :pressed, :focus, :disabled, :checked, etc.). Use to verify visual states without real input. Omit pseudoClass to list active ones.
        Returns: array of active pseudo-class names, or confirmation when setting.
        Example listing: [":pointerover",":focus"]
        Example set: "Activated ':pressed' on Button#15"
        """)]
    public static async Task<string> PseudoClass(
        ConnectionPool pool,
        [Description(SelectorDescription)] string selector,
        [Description("Pseudo-class name (e.g., pointerover, pressed, focus). Omit to list all.")] string? pseudoClass = null,
        [Description("Activate (true) or deactivate (false) the pseudo-class")] bool? isActive = null)
    {
        var conn = pool.GetActive();
        var parms = new Dictionary<string, object?> { ["selector"] = selector };
        if (pseudoClass is not null) parms["pseudoClass"] = pseudoClass;
        if (isActive is not null) parms["isActive"] = isActive;

        return await conn.InvokeAsync(ProtocolMethods.GetPseudoClasses, parms);
    }
}
