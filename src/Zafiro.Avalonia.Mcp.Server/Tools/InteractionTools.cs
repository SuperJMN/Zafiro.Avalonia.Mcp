using System.ComponentModel;
using Zafiro.Avalonia.Mcp.Protocol;
using Zafiro.Avalonia.Mcp.Server.Connection;
using ModelContextProtocol;
using ModelContextProtocol.Server;

namespace Zafiro.Avalonia.Mcp.Server.Tools;

[McpServerToolType]
public sealed class InteractionTools
{
    [McpServerTool(Name = "select_item"), Description("Select an item in a ListBox, ComboBox, TabControl, or any SelectingItemsControl by index or text content. More reliable than click for item selection — works even if the item is off-screen. Provide either index or text (case-insensitive match), not both. Returns the selected item info.")]
    public static async Task<string> SelectItem(
        ConnectionPool pool,
        [Description("Node ID of the element")] int nodeId,
        [Description("Index of the item to select")] int? index = null,
        [Description("Text of the item to select (matched case-insensitively)")] string? text = null)
    {
        var conn = pool.GetActive();
        var parms = new Dictionary<string, object?> { ["nodeId"] = nodeId };
        if (index is not null) parms["index"] = index;
        if (text is not null) parms["text"] = text;
        var result = await conn.SendAsync(ProtocolMethods.SelectItem, parms);
        return result?.ToString() ?? "No result";
    }

    [McpServerTool(Name = "toggle"), Description("Toggle a CheckBox, RadioButton, ToggleButton, or ToggleSwitch. Omit state to flip the current value, or provide an explicit true/false to set a specific state. More reliable than click for toggling controls. Returns the new checked/unchecked state.")]
    public static async Task<string> Toggle(
        ConnectionPool pool,
        [Description("Node ID of the element")] int nodeId,
        [Description("Explicit state to set (true/false). Omit to toggle.")] bool? state = null)
    {
        var conn = pool.GetActive();
        var parms = new Dictionary<string, object?> { ["nodeId"] = nodeId };
        if (state is not null) parms["state"] = state;
        var result = await conn.SendAsync(ProtocolMethods.Toggle, parms);
        return result?.ToString() ?? "No result";
    }

    [McpServerTool(Name = "set_value"), Description("Set the numeric value of a Slider, NumericUpDown, or ProgressBar. The value is automatically clamped to the control's Minimum/Maximum range. Returns the actual value after clamping.")]
    public static async Task<string> SetValue(
        ConnectionPool pool,
        [Description("Node ID of the element")] int nodeId,
        [Description("Value to set (clamped to control's min/max range)")] double value)
    {
        var conn = pool.GetActive();
        var result = await conn.SendAsync(ProtocolMethods.SetValue, new { nodeId, value });
        return result?.ToString() ?? "No result";
    }

    [McpServerTool(Name = "wait_for"), Description("Wait until a UI condition is met, then return. Eliminates the need for screenshot polling loops. Supported conditions: exists (element appears), not_exists (element removed), visible (element becomes visible), enabled (element becomes enabled), text_equals (exact text match), text_contains (partial text match), count_equals (number of matches equals value). Returns immediately if condition is already met.")]
    public static async Task<string> WaitFor(
        ConnectionPool pool,
        [Description("Search query — matches by type name, x:Name, or text content")] string query,
        [Description("Condition to wait for: exists, not_exists, visible, enabled, text_equals, text_contains, count_equals")] string condition,
        [Description("Value for the condition (e.g., the text to match)")] string? value = null,
        [Description("Timeout in milliseconds (default: 5000, max: 30000)")] int timeoutMs = 5000)
    {
        var conn = pool.GetActive();
        var parms = new Dictionary<string, object?> { ["query"] = query, ["condition"] = condition, ["timeoutMs"] = timeoutMs };
        if (value is not null) parms["value"] = value;
        var result = await conn.SendAsync(ProtocolMethods.WaitFor, parms);
        return result?.ToString() ?? "No result";
    }

    [McpServerTool(Name = "click_and_wait"), Description("Click a UI element and then wait for a condition to be met — combines click + wait_for in a single MCP call, reducing 3 round-trips (click, poll, confirm) to 1. Ideal for buttons that open dialogs, trigger navigation, or load content. Uses the same conditions as wait_for (exists, not_exists, visible, enabled, text_equals, text_contains, count_equals).")]
    public static async Task<string> ClickAndWait(
        ConnectionPool pool,
        [Description("Node ID of the element to click")] int nodeId,
        [Description("Search query for the wait condition")] string waitQuery,
        [Description("Condition to wait for after clicking")] string waitCondition,
        [Description("Value for the wait condition")] string? waitValue = null,
        [Description("Timeout in milliseconds (default: 5000)")] int timeoutMs = 5000)
    {
        var conn = pool.GetActive();
        var parms = new Dictionary<string, object?>
        {
            ["nodeId"] = nodeId, ["waitQuery"] = waitQuery, ["waitCondition"] = waitCondition,
            ["timeoutMs"] = timeoutMs
        };
        if (waitValue is not null) parms["waitValue"] = waitValue;
        var result = await conn.SendAsync(ProtocolMethods.ClickAndWait, parms);
        return result?.ToString() ?? "No result";
    }

    [McpServerTool(Name = "click_by_query"), Description("Atomic search-and-click: finds a visible, enabled control matching the query (by type, x:Name, or text content) and clicks it in a single operation. Eliminates the race condition where nodeIds become stale between separate search and click calls. Use 'role' to disambiguate (e.g., role='button'). Use 'occurrence' to target the Nth match (0-based). Returns the nodeId of the clicked element for follow-up operations.")]
    public static async Task<string> ClickByQuery(
        ConnectionPool pool,
        [Description("Search query — matches by type name, x:Name, or visible text content")] string query,
        [Description("Optional role filter: button, textbox, checkbox, radio, combobox, tab, listitem, menuitem, togglebutton")] string? role = null,
        [Description("0-based index when multiple elements match (default: 0 = first match)")] int occurrence = 0)
    {
        var conn = pool.GetActive();
        var parms = new Dictionary<string, object?> { ["query"] = query, ["occurrence"] = occurrence };
        if (role is not null) parms["role"] = role;
        var result = await conn.SendAsync(ProtocolMethods.ClickByQuery, parms);
        return result?.ToString() ?? "No result";
    }

    [McpServerTool(Name = "scroll"), Description("Scroll a ScrollViewer in a given direction (up, down, left, right). If the target element is not a ScrollViewer, the nearest ancestor ScrollViewer is used. Default scroll amount is 100 pixels per call.")]
    public static async Task<string> Scroll(
        ConnectionPool pool,
        [Description("Node ID of the element")] int nodeId,
        [Description("Scroll direction: up, down, left, right")] string direction,
        [Description("Scroll amount in pixels (default: 100)")] double? amount = null)
    {
        var conn = pool.GetActive();
        var parms = new Dictionary<string, object?> { ["nodeId"] = nodeId, ["direction"] = direction };
        if (amount is not null) parms["amount"] = amount;
        var result = await conn.SendAsync(ProtocolMethods.Scroll, parms);
        return result?.ToString() ?? "No result";
    }
}
