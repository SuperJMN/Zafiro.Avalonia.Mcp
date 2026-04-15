using System.ComponentModel;
using Zafiro.Avalonia.Mcp.Protocol;
using Zafiro.Avalonia.Mcp.Server.Connection;
using ModelContextProtocol;
using ModelContextProtocol.Server;

namespace Zafiro.Avalonia.Mcp.Server.Tools;

[McpServerToolType]
public sealed class InputTools
{
    [McpServerTool(Name = "click"), Description("Click a UI element. Handles Button (invokes command/event), ToggleButton (toggles state), ListBoxItem (selects it), TabItem (switches tab), and MenuItem (invokes). Falls back to pointer simulation for other controls. For more reliable results, prefer select_item for list/tab selection, or toggle for checkboxes and toggle buttons.")]
    public static async Task<string> Click(
        ConnectionPool pool,
        [Description("Node ID of the element to click")] int nodeId)
    {
        var conn = pool.GetActive();
        var result = await conn.SendAsync(ProtocolMethods.Click, new { nodeId });
        return result?.ToString() ?? "No result";
    }

    [McpServerTool(Name = "key_down"), Description("Send a key down event to a focused element. Use the modifiers parameter for key combinations like Ctrl+C, Shift+Tab, Alt+F4, etc. Returns the key event result.")]
    public static async Task<string> KeyDown(
        ConnectionPool pool,
        [Description("Node ID of the element")] int nodeId,
        [Description("Key name (e.g., Enter, Tab, Escape, A, B, Space)")] string key,
        [Description("Key modifiers (e.g., ctrl, shift, alt, ctrl+shift). Omit for none.")] string? modifiers = null)
    {
        var conn = pool.GetActive();
        var parms = new Dictionary<string, object?> { ["nodeId"] = nodeId, ["key"] = key };
        if (modifiers is not null) parms["modifiers"] = modifiers;
        var result = await conn.SendAsync(ProtocolMethods.KeyDown, parms);
        return result?.ToString() ?? "No result";
    }

    [McpServerTool(Name = "key_up"), Description("Send a key up event to a focused element. Pair with key_down for key press sequences. Use the modifiers parameter for combinations like Ctrl+C, Shift+Tab, etc.")]
    public static async Task<string> KeyUp(
        ConnectionPool pool,
        [Description("Node ID of the element")] int nodeId,
        [Description("Key name")] string key,
        [Description("Key modifiers (e.g., ctrl, shift, alt, ctrl+shift). Omit for none.")] string? modifiers = null)
    {
        var conn = pool.GetActive();
        var parms = new Dictionary<string, object?> { ["nodeId"] = nodeId, ["key"] = key };
        if (modifiers is not null) parms["modifiers"] = modifiers;
        var result = await conn.SendAsync(ProtocolMethods.KeyUp, parms);
        return result?.ToString() ?? "No result";
    }

    [McpServerTool(Name = "text_input"), Description("Enter text into a TextBox or similar text-editing control. If the target nodeId is a container (e.g., AutoCompleteBox), it automatically finds the child TextBox. Replaces any existing text. Set pressEnter to simulate pressing Enter after input (useful for search boxes or forms).")]
    public static async Task<string> TextInput(
        ConnectionPool pool,
        [Description("Node ID of the element")] int nodeId,
        [Description("Text to enter")] string text,
        [Description("Whether to simulate pressing Enter after text input")] bool pressEnter = false)
    {
        var conn = pool.GetActive();
        var result = await conn.SendAsync(ProtocolMethods.TextInput, new { nodeId, text, pressEnter });
        return result?.ToString() ?? "No result";
    }

    [McpServerTool(Name = "action"), Description("Perform a UI action on an element: Focus (give keyboard focus), Enable/Disable (change IsEnabled), or BringIntoView (scroll element into viewport). Returns confirmation of the action performed.")]
    public static async Task<string> Action(
        ConnectionPool pool,
        [Description("Node ID of the element")] int nodeId,
        [Description("Action to perform: Focus, Enable, Disable, BringIntoView")] string action)
    {
        var conn = pool.GetActive();
        var result = await conn.SendAsync(ProtocolMethods.Action, new { nodeId, action });
        return result?.ToString() ?? "No result";
    }

    [McpServerTool(Name = "pseudo_class"), Description("Get, set, or list pseudo-classes on a UI element (e.g., :pointerover, :pressed, :focus, :disabled). Useful for testing visual states without actual user input. Omit pseudoClass to list all active pseudo-classes on the element.")]
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

        var result = await conn.SendAsync(ProtocolMethods.GetPseudoClasses, parms);
        return result?.ToString() ?? "No result";
    }
}
