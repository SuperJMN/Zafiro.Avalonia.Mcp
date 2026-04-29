using System.ComponentModel;
using Zafiro.Avalonia.Mcp.Protocol;
using Zafiro.Avalonia.Mcp.Tool.Connection;
using ModelContextProtocol.Server;

namespace Zafiro.Avalonia.Mcp.Tool.Tools;

[McpServerToolType]
public sealed class DataTools
{
    [McpServerTool(Name = "get_datacontext"), Description("""
        Get the DataContext (ViewModel) of a UI element with type name and current values of public properties. ESSENTIAL for MVVM debugging — use to see what data backs a view. For property bindings (not values) use get_bindings.
        Returns: {type, properties:[{name, type, value}]}.
        Example: {"type":"MainViewModel","properties":[{"name":"Title","type":"String","value":"Home"},{"name":"Count","type":"Int32","value":3}]}
        """)]
    public static async Task<string> GetDataContext(
        ConnectionPool pool,
        [Description("CSS-like selector identifying the element")] string selector)
    {
        var conn = pool.GetActive();
        return await conn.InvokeAsync(ProtocolMethods.GetDataContext, new { selector }, "No data context");
    }

    [McpServerTool(Name = "get_bindings"), Description("""
        Get active data bindings on a UI element: properties bound, paths, priorities, current resolved values. Use to debug binding errors or trace where a UI value comes from. For ViewModel state use get_datacontext.
        Returns: array of {property, path, priority, value}.
        Example: [{"property":"Text","path":"Title","priority":"LocalValue","value":"Home"}]
        """)]
    public static async Task<string> GetBindings(
        ConnectionPool pool,
        [Description("CSS-like selector identifying the element")] string selector)
    {
        var conn = pool.GetActive();
        return await conn.InvokeAsync(ProtocolMethods.GetBindings, new { selector }, "No bindings");
    }

    [McpServerTool(Name = "find_view_source"), Description("""
        Find the AXAML source URL (avares://...) for a runtime control. Maps live tree → XAML definition. Use before get_xaml or open_asset. Returns null if no embedded AXAML found (e.g. code-built control).
        Returns: {assetUrl} or null.
        Example: {"assetUrl":"avares://MyApp/Views/MainView.axaml"}
        """)]
    public static async Task<string> FindViewSource(
        ConnectionPool pool,
        [Description("CSS-like selector identifying the element")] string selector)
    {
        var conn = pool.GetActive();
        return await conn.InvokeAsync(ProtocolMethods.FindViewSource, new { selector }, "No source found");
    }

    [McpServerTool(Name = "get_xaml"), Description("""
        Get the AXAML source code for a runtime control (combines find_view_source + open_asset). Use to inspect how a view is declared. Returns null when no XAML can be located.
        Returns: {assetUrl, content} or null.
        Example: {"assetUrl":"avares://MyApp/Views/MainView.axaml","content":"<UserControl ...>...</UserControl>"}
        """)]
    public static async Task<string> GetXaml(
        ConnectionPool pool,
        [Description("CSS-like selector identifying the element")] string selector)
    {
        var conn = pool.GetActive();
        return await conn.InvokeAsync(ProtocolMethods.GetXaml, new { selector }, "No XAML found");
    }

    [McpServerTool(Name = "diff_tree"), Description("""
        Efficient screen change detection. action='snapshot' captures baseline; action='diff' (default) auto-snapshots and reports added/removed lines vs the previous snapshot. Use after an action to see ONLY what changed instead of re-reading the whole screen.
        Returns: {added:[...], removed:[...]} for diff, {captured:n} for snapshot.
        Example: {"added":["Saved successfully"],"removed":["Unsaved changes"]}
        """)]
    public static async Task<string> DiffTree(
        ConnectionPool pool,
        [Description("Node ID to scope. Omit for the first window.")] int? nodeId = null,
        [Description("'snapshot' to capture, 'diff' to compare with last snapshot (default)")] string action = "diff")
    {
        var conn = pool.GetActive();
        var parms = new Dictionary<string, object> { ["action"] = action };
        if (nodeId.HasValue) parms["nodeId"] = nodeId.Value;
        return await conn.InvokeAsync(ProtocolMethods.DiffTree, parms, "No diff data");
    }
}
