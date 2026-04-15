using System.ComponentModel;
using Zafiro.Avalonia.Mcp.Protocol;
using Zafiro.Avalonia.Mcp.Server.Connection;
using ModelContextProtocol.Server;

namespace Zafiro.Avalonia.Mcp.Server.Tools;

[McpServerToolType]
public sealed class DataTools
{
    [McpServerTool(Name = "get_datacontext"), Description("Get the DataContext (ViewModel) of a UI element. Returns the type name and all public properties with their current values. Essential for MVVM debugging — see what data backs a view.")]
    public static async Task<string> GetDataContext(
        ConnectionPool pool,
        [Description("Node ID of the element")] int nodeId)
    {
        var conn = pool.GetActive();
        var result = await conn.SendAsync(ProtocolMethods.GetDataContext, new { nodeId });
        return result?.ToString() ?? "No data context";
    }

    [McpServerTool(Name = "get_bindings"), Description("Get active data bindings on a UI element. Shows which properties have bindings, their paths, priorities, and current values. Use this to debug binding issues or understand how a view connects to its ViewModel.")]
    public static async Task<string> GetBindings(
        ConnectionPool pool,
        [Description("Node ID of the element")] int nodeId)
    {
        var conn = pool.GetActive();
        var result = await conn.SendAsync(ProtocolMethods.GetBindings, new { nodeId });
        return result?.ToString() ?? "No bindings";
    }

    [McpServerTool(Name = "find_view_source"), Description("Find the AXAML source file (avares:// URL) for a runtime UI control. Maps from the live control back to its XAML definition. Returns null if no embedded AXAML is found.")]
    public static async Task<string> FindViewSource(
        ConnectionPool pool,
        [Description("Node ID of the element")] int nodeId)
    {
        var conn = pool.GetActive();
        var result = await conn.SendAsync(ProtocolMethods.FindViewSource, new { nodeId });
        return result?.ToString() ?? "No source found";
    }

    [McpServerTool(Name = "get_xaml"), Description("Get the AXAML source code for a runtime UI control. Combines find_view_source + open_asset in one call — finds the XAML file and returns its content. Use this to see how a view is defined in code.")]
    public static async Task<string> GetXaml(
        ConnectionPool pool,
        [Description("Node ID of the element")] int nodeId)
    {
        var conn = pool.GetActive();
        var result = await conn.SendAsync(ProtocolMethods.GetXaml, new { nodeId });
        return result?.ToString() ?? "No XAML found";
    }

    [McpServerTool(Name = "diff_tree"), Description("Efficient screen change detection. First call captures a snapshot of all visible text. Subsequent calls return only what changed (added/removed lines). Use after performing an action to see its effect without re-reading the full screen. Set action='snapshot' to explicitly capture, or 'diff' (default) to auto-snapshot and compare.")]
    public static async Task<string> DiffTree(
        ConnectionPool pool,
        [Description("Node ID to scope. Omit for the first window.")] int? nodeId = null,
        [Description("'snapshot' to capture, 'diff' to compare with last snapshot (default)")] string action = "diff")
    {
        var conn = pool.GetActive();
        var parms = new Dictionary<string, object> { ["action"] = action };
        if (nodeId.HasValue) parms["nodeId"] = nodeId.Value;
        var result = await conn.SendAsync(ProtocolMethods.DiffTree, parms);
        return result?.ToString() ?? "No diff data";
    }
}
