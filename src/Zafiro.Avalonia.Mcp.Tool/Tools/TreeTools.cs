using System.ComponentModel;
using System.Text.Json;
using Zafiro.Avalonia.Mcp.Protocol;
using Zafiro.Avalonia.Mcp.Tool.Connection;
using ModelContextProtocol.Server;

namespace Zafiro.Avalonia.Mcp.Tool.Tools;

[McpServerToolType]
public sealed class TreeTools
{
    [McpServerTool(Name = "get_tree"), Description("""
        Hierarchical visual/logical tree of the app. EXPENSIVE — prefer get_snapshot, get_interactables, or get_screen_text for most AI tasks; only use get_tree when you specifically need parent/child structure.
        Returns: nested NodeInfo with {nodeId, type, name, text, role, isEnabled, bounds, children}.
        Example: {"nodeId":1,"type":"Window","children":[{"nodeId":2,"type":"StackPanel","children":[{"nodeId":3,"type":"Button","text":"OK","role":"button"}]}]}
        """)]
    public static async Task<string> GetTree(
        ConnectionPool pool,
        [Description("CSS-like selector to scope the tree to a single element. Omit for roots (windows).")] string? selector = null,
        [Description("Tree kind: Visual, Logical, or Merged")] string treeKind = "Visual",
        [Description("Maximum depth to traverse (1=direct children, -1=full)")] int depth = 10)
    {
        var conn = pool.GetActive();
        var parms = new Dictionary<string, object> { ["treeKind"] = treeKind, ["depth"] = depth };
        if (selector is not null) parms["selector"] = selector;
        return await conn.InvokeAsync(ProtocolMethods.GetTree, parms, "No tree data");
    }

    [McpServerTool(Name = "search"), Description("""
        Find elements by type name, x:Name, or text content (partial, case-insensitive). Use to locate a specific control before interacting; for "click on the X button" prefer click_by_query (one call instead of search+click).
        Returns: array of NodeInfo {nodeId, type, name, text, role, isEnabled, bounds}.
        Example: [{"nodeId":42,"type":"Button","name":"SaveBtn","text":"Save","role":"button","isEnabled":true,"bounds":{"x":10,"y":20,"w":80,"h":30}}]
        """)]
    public static async Task<string> Search(
        ConnectionPool pool,
        [Description("Type or x:Name to search for (partial match)")] string query,
        [Description("Maximum results to return")] int limit = 20)
    {
        var conn = pool.GetActive();
        return await conn.InvokeAsync(ProtocolMethods.Search, new { query, limit }, "No results");
    }

    [McpServerTool(Name = "get_ancestors"), Description("""
        Walk up the visual tree from a node to the root window. Useful when search returned a TextBlock but you actually need the enclosing ListBoxItem/Button to interact with.
        Returns: ordered array (closest parent first) of {nodeId, type, name, bounds}.
        Example: [{"nodeId":40,"type":"ListBoxItem"},{"nodeId":35,"type":"ListBox"},{"nodeId":1,"type":"Window"}]
        """)]
    public static async Task<string> GetAncestors(
        ConnectionPool pool,
        [Description("CSS-like selector identifying the element to get ancestors for")] string selector)
    {
        var conn = pool.GetActive();
        return await conn.InvokeAsync(ProtocolMethods.GetAncestors, new { selector }, "No ancestors");
    }

    [McpServerTool(Name = "get_snapshot"), Description("""
        BEST FIRST CALL to understand the UI. Compact spatial flat list of all visible text + interactive controls in logical (XAML) order. Replaces get_screen_text + get_interactables with one cheaper call.
        Returns: {window:{w,h}, focused:nodeId, items:[{nodeId, role, text, value, x, y, w, h}, ...]}.
        Example: {"window":{"w":800,"h":600},"focused":12,"items":[{"nodeId":5,"role":"text","text":"Login"},{"nodeId":12,"role":"textbox","value":"user@x","x":50,"y":80,"w":200,"h":24},{"nodeId":15,"role":"button","text":"Sign in"}]}
        """)]
    public static async Task<string> GetSnapshot(
        ConnectionPool pool,
        [Description("CSS-like selector to scope the snapshot to a single element. Omit for the first window.")] string? selector = null,
        [Description("When true (default), only returns elements within the visible viewport.")] bool visibleOnly = true)
    {
        var conn = pool.GetActive();
        var parms = new Dictionary<string, object> { ["visibleOnly"] = visibleOnly };
        if (selector is not null) parms["selector"] = selector;
        return await conn.InvokeAsync(ProtocolMethods.GetSnapshot, parms, "No snapshot");
    }

    [McpServerTool(Name = "get_screen_text"), Description("""
        All visible text on screen in reading order (top-to-bottom, left-to-right). Cheapest way to "read" the UI when you don't need nodeIds; otherwise prefer get_snapshot.
        Returns: plain text string, one logical line per text element.
        Example: "Welcome\nUsername\nPassword\nSign in\nForgot password?"
        """)]
    public static async Task<string> GetScreenText(
        ConnectionPool pool,
        [Description("CSS-like selector to scope. Omit for the first window.")] string? selector = null,
        [Description("When true, only returns text that is within the visible viewport (not scrolled off-screen). Default: false.")] bool visibleOnly = false)
    {
        var conn = pool.GetActive();
        var parms = new Dictionary<string, object>();
        if (selector is not null) parms["selector"] = selector;
        if (visibleOnly) parms["visibleOnly"] = true;
        return await conn.InvokeAsync(ProtocolMethods.GetScreenText, parms, "No text found");
    }

    [McpServerTool(Name = "get_interactables"), Description("""
        Flat list of every actionable control visible on screen (buttons, textboxes, checkboxes, sliders, list items, etc.). Use to answer "what can I do here?". Prefer get_snapshot when you also need spatial coordinates.
        Returns: array of {nodeId, role, text, value}; nodeId is ready for click/text_input/toggle/select_item.
        Example: [{"nodeId":12,"role":"textbox","value":""},{"nodeId":13,"role":"checkbox","text":"Remember me","value":false},{"nodeId":15,"role":"button","text":"Sign in"}]
        """)]
    public static async Task<string> GetInteractables(
        ConnectionPool pool,
        [Description("CSS-like selector to scope the search. Omit to search all windows.")] string? selector = null)
    {
        var conn = pool.GetActive();
        var parms = new Dictionary<string, object>();
        if (selector is not null) parms["selector"] = selector;
        return await conn.InvokeAsync(ProtocolMethods.GetInteractables, parms, "No interactables found");
    }

    [McpServerTool(Name = "list_windows"), Description("""
        List all open top-level windows. Useful when the app has dialogs or multiple windows and you need to scope subsequent calls.
        Returns: array of {nodeId, title, bounds}.
        Example: [{"nodeId":1,"title":"MainWindow","bounds":{"x":0,"y":0,"w":1280,"h":720}},{"nodeId":50,"title":"Settings","bounds":{"x":300,"y":200,"w":400,"h":300}}]
        """)]
    public static async Task<string> ListWindows(ConnectionPool pool)
    {
        var conn = pool.GetActive();
        return await conn.InvokeAsync(ProtocolMethods.ListWindows, empty: "No windows");
    }
}
