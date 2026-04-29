using System.ComponentModel;
using Zafiro.Avalonia.Mcp.Protocol;
using Zafiro.Avalonia.Mcp.Tool.Connection;
using ModelContextProtocol;
using ModelContextProtocol.Server;

namespace Zafiro.Avalonia.Mcp.Tool.Tools;

[McpServerToolType]
public sealed class InteractionTools
{
    [McpServerTool(Name = "select_item"), Description("""
        Select an item in a ListBox/ComboBox/TabControl/SelectingItemsControl by index or text (case-insensitive). MORE RELIABLE than click — works even if the item is virtualized off-screen. Provide either index OR text, not both.
        Returns: {nodeId, index, text} of the selected item.
        Example: {"nodeId":42,"index":2,"text":"Settings"}
        """)]
    public static async Task<string> SelectItem(
        ConnectionPool pool,
        [Description("CSS-like selector identifying the items host (ListBox, ComboBox, TabControl, etc.)")] string selector,
        [Description("Index of the item to select")] int? index = null,
        [Description("Text of the item to select (matched case-insensitively)")] string? text = null)
    {
        var conn = pool.GetActive();
        var parms = new Dictionary<string, object?> { ["selector"] = selector };
        if (index is not null) parms["index"] = index;
        if (text is not null) parms["text"] = text;
        return await conn.InvokeAsync(ProtocolMethods.SelectItem, parms);
    }

    [McpServerTool(Name = "toggle"), Description("""
        Toggle a CheckBox/RadioButton/ToggleButton/ToggleSwitch. Omit state to flip; pass true/false to set explicitly (idempotent). MORE RELIABLE than click for these controls.
        Returns: {isChecked} after the operation.
        Example: {"isChecked":true}
        """)]
    public static async Task<string> Toggle(
        ConnectionPool pool,
        [Description("CSS-like selector identifying the toggle control")] string selector,
        [Description("Explicit state to set (true/false). Omit to toggle.")] bool? state = null)
    {
        var conn = pool.GetActive();
        var parms = new Dictionary<string, object?> { ["selector"] = selector };
        if (state is not null) parms["state"] = state;
        return await conn.InvokeAsync(ProtocolMethods.Toggle, parms);
    }

    [McpServerTool(Name = "set_value"), Description("""
        Set the numeric Value of a Slider/NumericUpDown/ProgressBar. Auto-clamped to [Minimum, Maximum]. For text values use set_prop instead.
        Returns: {value} actually applied after clamping.
        Example: {"value":75}
        """)]
    public static async Task<string> SetValue(
        ConnectionPool pool,
        [Description("CSS-like selector identifying the range control")] string selector,
        [Description("Value to set (clamped to control's min/max range)")] double value)
    {
        var conn = pool.GetActive();
        return await conn.InvokeAsync(ProtocolMethods.SetValue, new { selector, value });
    }

    [McpServerTool(Name = "wait_for"), Description("""
        Block until a UI condition is met. ELIMINATES screenshot polling loops. Conditions: exists, not_exists, visible, enabled, text_equals, text_contains, count_equals (use 'value' to supply the comparand). Returns immediately if already met.
        Returns: {met:true, elapsedMs} or {met:false, elapsedMs} on timeout.
        Example: {"met":true,"elapsedMs":340}
        """)]
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
        return await conn.InvokeAsync(ProtocolMethods.WaitFor, parms);
    }

    [McpServerTool(Name = "click_and_wait"), Description("""
        Atomic click + wait_for in one call. Use for buttons that open dialogs, navigate, or load content (collapses 3 round-trips into 1). Same conditions as wait_for.
        Returns: {clicked:nodeId, met:bool, elapsedMs}.
        Example: {"clicked":15,"met":true,"elapsedMs":420}
        """)]
    public static async Task<string> ClickAndWait(
        ConnectionPool pool,
        [Description("CSS-like selector identifying the element to click")] string selector,
        [Description("Search query for the wait condition")] string waitQuery,
        [Description("Condition to wait for after clicking")] string waitCondition,
        [Description("Value for the wait condition")] string? waitValue = null,
        [Description("Timeout in milliseconds (default: 5000)")] int timeoutMs = 5000)
    {
        var conn = pool.GetActive();
        var parms = new Dictionary<string, object?>
        {
            ["selector"] = selector, ["waitQuery"] = waitQuery, ["waitCondition"] = waitCondition,
            ["timeoutMs"] = timeoutMs
        };
        if (waitValue is not null) parms["waitValue"] = waitValue;
        return await conn.InvokeAsync(ProtocolMethods.ClickAndWait, parms);
    }

    [McpServerTool(Name = "click_by_query"), Description("""
        Atomic find-visible-and-click. PREFERRED over search+click — eliminates the race where nodeIds become stale. Use 'role' to disambiguate (button/textbox/checkbox/radio/combobox/tab/listitem/menuitem/togglebutton) and 'occurrence' for the Nth match.
        Returns: {nodeId} of the element that was actually clicked.
        Example: {"nodeId":15}
        """)]
    public static async Task<string> ClickByQuery(
        ConnectionPool pool,
        [Description("Search query — matches by type name, x:Name, or visible text content")] string query,
        [Description("Optional role filter: button, textbox, checkbox, radio, combobox, tab, listitem, menuitem, togglebutton")] string? role = null,
        [Description("0-based index when multiple elements match (default: 0 = first match)")] int occurrence = 0)
    {
        var conn = pool.GetActive();
        var parms = new Dictionary<string, object?> { ["query"] = query, ["occurrence"] = occurrence };
        if (role is not null) parms["role"] = role;
        return await conn.InvokeAsync(ProtocolMethods.ClickByQuery, parms);
    }

    [McpServerTool(Name = "scroll"), Description("""
        Scroll a ScrollViewer (or the nearest ScrollViewer ancestor of the selected element) by 'amount' pixels in a direction. For "scroll until X is visible" prefer action='BringIntoView' on the target.
        Returns: {offset:{x,y}} after the scroll.
        Example: {"offset":{"x":0,"y":200}}
        """)]
    public static async Task<string> Scroll(
        ConnectionPool pool,
        [Description("CSS-like selector identifying the element to scroll (or whose ancestor ScrollViewer to scroll)")] string selector,
        [Description("Scroll direction: up, down, left, right")] string direction,
        [Description("Scroll amount in pixels (default: 100)")] double? amount = null)
    {
        var conn = pool.GetActive();
        var parms = new Dictionary<string, object?> { ["selector"] = selector, ["direction"] = direction };
        if (amount is not null) parms["amount"] = amount;
        return await conn.InvokeAsync(ProtocolMethods.Scroll, parms);
    }
}
