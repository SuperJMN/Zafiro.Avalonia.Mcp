using System.ComponentModel;
using System.Text.Json;
using Zafiro.Avalonia.Mcp.Protocol;
using Zafiro.Avalonia.Mcp.Tool.Connection;
using ModelContextProtocol.Server;

namespace Zafiro.Avalonia.Mcp.Tool.Tools;

[McpServerToolType]
public sealed class TreeTools
{
    [McpServerTool(Name = "get_tree"), Description("Get the visual or logical tree of the connected Avalonia app. Returns a hierarchical tree with enriched info per node: type, x:Name, text content, role, isEnabled, bounds, and children. For AI use cases, prefer get_interactables (flat list of actionable controls) or get_screen_text (readable content) instead — they are cheaper and easier to consume.")]
    public static async Task<string> GetTree(
        ConnectionPool pool,
        [Description("Node ID to start from. Omit for roots (windows).")] int? nodeId = null,
        [Description("Tree kind: Visual, Logical, or Merged")] string treeKind = "Visual",
        [Description("Maximum depth to traverse (1=direct children, -1=full)")] int depth = 10)
    {
        var conn = pool.GetActive();
        var parms = new Dictionary<string, object> { ["treeKind"] = treeKind, ["depth"] = depth };
        if (nodeId.HasValue) parms["nodeId"] = nodeId.Value;
        var result = await conn.SendAsync(ProtocolMethods.GetTree, parms);
        return result?.ToString() ?? "No tree data";
    }

    [McpServerTool(Name = "search"), Description("Search for elements by type name, x:Name, or text content (partial match). Returns enriched NodeInfo for each match including nodeId, type, name, text, role, isEnabled, and bounds. Use to find specific controls before interacting with them.")]
    public static async Task<string> Search(
        ConnectionPool pool,
        [Description("Type or x:Name to search for (partial match)")] string query,
        [Description("Maximum results to return")] int limit = 20)
    {
        var conn = pool.GetActive();
        var result = await conn.SendAsync(ProtocolMethods.Search, new { query, limit });
        return result?.ToString() ?? "No results";
    }

    [McpServerTool(Name = "get_ancestors"), Description("Get the ancestor chain from a node up to the root window. Returns an ordered list of parent elements with their type, name, and bounds. Useful for understanding where a control sits in the visual hierarchy.")]
    public static async Task<string> GetAncestors(
        ConnectionPool pool,
        [Description("Node ID to get ancestors for")] int nodeId)
    {
        var conn = pool.GetActive();
        var result = await conn.SendAsync(ProtocolMethods.GetAncestors, new { nodeId });
        return result?.ToString() ?? "No ancestors";
    }

    [McpServerTool(Name = "get_snapshot"), Description("Get a compact spatial snapshot of the UI in a single call. Returns a flat list of all visible text and interactive controls in logical tree order (the order defined in XAML, matching the developer's intended flow). Each entry includes: nodeId (ready to use with click/text_input/etc.), role, text, current value, and absolute position (x, y, w, h) for spatial reasoning. Also reports the focused element and window size. Use this FIRST to understand the UI — it replaces a get_screen_text + get_interactables pair with one cheaper call.")]
    public static async Task<string> GetSnapshot(
        ConnectionPool pool,
        [Description("Node ID to scope. Omit for the first window.")] int? nodeId = null,
        [Description("When true (default), only returns elements within the visible viewport.")] bool visibleOnly = true)
    {
        var conn = pool.GetActive();
        var parms = new Dictionary<string, object> { ["visibleOnly"] = visibleOnly };
        if (nodeId.HasValue) parms["nodeId"] = nodeId.Value;
        var result = await conn.SendAsync(ProtocolMethods.GetSnapshot, parms);
        return result?.ToString() ?? "No snapshot";
    }

    [McpServerTool(Name = "get_screen_text"), Description("Get all visible text on screen in reading order (top-to-bottom, left-to-right). Returns plain text that represents what a user would see. Prefer get_snapshot when you also need nodeIds or positions.")]
    public static async Task<string> GetScreenText(
        ConnectionPool pool,
        [Description("Node ID to scope. Omit for the first window.")] int? nodeId = null,
        [Description("When true, only returns text that is within the visible viewport (not scrolled off-screen). Default: false.")] bool visibleOnly = false)
    {
        var conn = pool.GetActive();
        var parms = new Dictionary<string, object>();
        if (nodeId.HasValue) parms["nodeId"] = nodeId.Value;
        if (visibleOnly) parms["visibleOnly"] = true;
        var result = await conn.SendAsync(ProtocolMethods.GetScreenText, parms);
        return result?.ToString() ?? "No text found";
    }

    [McpServerTool(Name = "get_interactables"), Description("Get all interactive controls visible on screen (buttons, textboxes, checkboxes, sliders, list items, etc.) with their text, role, and current value. Returns a flat JSON array of {nodeId, role, text, value}. The nodeId can be used directly with click, text_input, toggle, select_item, and other interaction tools. Prefer get_snapshot when you also need spatial position context.")]
    public static async Task<string> GetInteractables(
        ConnectionPool pool,
        [Description("Node ID to scope the search. Omit to search all windows.")] int? nodeId = null)
    {
        var conn = pool.GetActive();
        var parms = new Dictionary<string, object>();
        if (nodeId.HasValue) parms["nodeId"] = nodeId.Value;
        var result = await conn.SendAsync(ProtocolMethods.GetInteractables, parms);
        return result?.ToString() ?? "No interactables found";
    }

    [McpServerTool(Name = "list_windows"), Description("List all open windows of the connected Avalonia app. Returns nodeId, title, and bounds for each window.")]
    public static async Task<string> ListWindows(ConnectionPool pool)
    {
        var conn = pool.GetActive();
        var result = await conn.SendAsync(ProtocolMethods.ListWindows);
        return result?.ToString() ?? "No windows";
    }
}
