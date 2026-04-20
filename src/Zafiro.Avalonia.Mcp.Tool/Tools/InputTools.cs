using System.ComponentModel;
using Zafiro.Avalonia.Mcp.Protocol;
using Zafiro.Avalonia.Mcp.Tool.Connection;
using ModelContextProtocol;
using ModelContextProtocol.Server;

namespace Zafiro.Avalonia.Mcp.Tool.Tools;

[McpServerToolType]
public sealed class InputTools
{
    [McpServerTool(Name = "click"), Description("""
        Click a UI element by nodeId. Handles Button (invokes command), ToggleButton (toggles), ListBoxItem (selects), TabItem (switches), MenuItem (invokes); falls back to pointer simulation for anything else. For lists/tabs prefer select_item, for checkboxes prefer toggle, for "click the X button" prefer click_by_query.
        Returns: confirmation string with the actual semantic invoked.
        Example: "Clicked Button 'Save' (invoked Command)"
        """)]
    public static async Task<string> Click(
        ConnectionPool pool,
        [Description("Node ID of the element to click")] int nodeId)
    {
        var conn = pool.GetActive();
        return await conn.InvokeAsync(ProtocolMethods.Click, new { nodeId });
    }

    [McpServerTool(Name = "tap"), Description("""
        Single-finger touch on an element. Same semantics as click but raises tap/touch events instead of pointer events; use only when testing touch-specific behavior.
        Returns: confirmation string.
        Example: "Tapped Button 'Save'"
        """)]
    public static async Task<string> Tap(
        ConnectionPool pool,
        [Description("Node ID of the element to tap")] int nodeId)
    {
        var conn = pool.GetActive();
        return await conn.InvokeAsync(ProtocolMethods.Tap, new { nodeId });
    }

    [McpServerTool(Name = "key_down"), Description("""
        Send a key down event to a focused element. Use for keyboard shortcuts (modifiers='ctrl+shift', key='S') or navigation keys. For typing text into a TextBox, use text_input instead.
        Returns: confirmation string.
        Example: "KeyDown sent: Ctrl+S"
        """)]
    public static async Task<string> KeyDown(
        ConnectionPool pool,
        [Description("Node ID of the element")] int nodeId,
        [Description("Key name (e.g., Enter, Tab, Escape, A, B, Space)")] string key,
        [Description("Key modifiers (e.g., ctrl, shift, alt, ctrl+shift). Omit for none.")] string? modifiers = null)
    {
        var conn = pool.GetActive();
        var parms = new Dictionary<string, object?> { ["nodeId"] = nodeId, ["key"] = key };
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
        [Description("Node ID of the element")] int nodeId,
        [Description("Key name")] string key,
        [Description("Key modifiers (e.g., ctrl, shift, alt, ctrl+shift). Omit for none.")] string? modifiers = null)
    {
        var conn = pool.GetActive();
        var parms = new Dictionary<string, object?> { ["nodeId"] = nodeId, ["key"] = key };
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
        [Description("Node ID of the element")] int nodeId,
        [Description("Text to enter")] string text,
        [Description("Whether to simulate pressing Enter after text input")] bool pressEnter = false)
    {
        var conn = pool.GetActive();
        return await conn.InvokeAsync(ProtocolMethods.TextInput, new { nodeId, text, pressEnter });
    }

    [McpServerTool(Name = "action"), Description("""
        Perform a non-input UI action on an element: Focus | Enable | Disable | BringIntoView. Use BringIntoView before interacting with off-screen items, Focus before sending key_down.
        Returns: confirmation string.
        Example: "Action 'BringIntoView' performed on ListBoxItem#42"
        """)]
    public static async Task<string> Action(
        ConnectionPool pool,
        [Description("Node ID of the element")] int nodeId,
        [Description("Action to perform: Focus, Enable, Disable, BringIntoView")] string action)
    {
        var conn = pool.GetActive();
        return await conn.InvokeAsync(ProtocolMethods.Action, new { nodeId, action });
    }

    [McpServerTool(Name = "pseudo_class"), Description("""
        Inspect or override pseudo-classes on an element (:pointerover, :pressed, :focus, :disabled, :checked, etc.). Use to verify visual states without real input. Omit pseudoClass to list active ones.
        Returns: array of active pseudo-class names, or confirmation when setting.
        Example listing: [":pointerover",":focus"]
        Example set: "Activated ':pressed' on Button#15"
        """)]
    public static async Task<string> PseudoClass(
        ConnectionPool pool,
        [Description("Node ID of the element")] int nodeId,
        [Description("Pseudo-class name (e.g., pointerover, pressed, focus). Omit to list all.")] string? pseudoClass = null,
        [Description("Activate (true) or deactivate (false) the pseudo-class")] bool? isActive = null)
    {
        var conn = pool.GetActive();
        var parms = new Dictionary<string, object?> { ["nodeId"] = nodeId };
        if (pseudoClass is not null) parms["pseudoClass"] = pseudoClass;
        if (isActive is not null) parms["isActive"] = isActive;

        return await conn.InvokeAsync(ProtocolMethods.GetPseudoClasses, parms);
    }
}
