using System.ComponentModel;
using Zafiro.Avalonia.Mcp.Protocol;
using Zafiro.Avalonia.Mcp.Tool.Connection;
using ModelContextProtocol.Server;

namespace Zafiro.Avalonia.Mcp.Tool.Tools;

[McpServerToolType]
public sealed class ResourceTools
{
    [McpServerTool(Name = "get_resources"), Description("Get resource dictionary entries (brushes, styles, templates, etc.) from the application or a specific element. Omit nodeId to get Application-level resources. Set onlySelf=true to exclude inherited resources. Returns resource keys, types, and values.")]
    public static async Task<string> GetResources(
        ConnectionPool pool,
        [Description("Node ID to scope resources to. Omit for Application resources.")] int? nodeId = null,
        [Description("Only resources defined on the node itself")] bool onlySelf = false)
    {
        var conn = pool.GetActive();
        var parms = new Dictionary<string, object?> { ["onlySelf"] = onlySelf };
        if (nodeId.HasValue) parms["nodeId"] = nodeId.Value;

        var result = await conn.SendAsync(ProtocolMethods.GetResources, parms);
        return result?.ToString() ?? "No resources";
    }

    [McpServerTool(Name = "list_assets"), Description("List all embedded assets (images, fonts, XAML files, etc.) in the application. Returns avares:// URLs that can be opened with open_asset.")]
    public static async Task<string> ListAssets(ConnectionPool pool)
    {
        var conn = pool.GetActive();
        var result = await conn.SendAsync(ProtocolMethods.ListAssets);
        return result?.ToString() ?? "No assets";
    }

    [McpServerTool(Name = "open_asset"), Description("Download an embedded asset by its avares:// URL. Use list_assets first to discover available asset URLs. Returns the asset content.")]
    public static async Task<string> OpenAsset(
        ConnectionPool pool,
        [Description("Asset URL (avares://...)")] string assetUrl)
    {
        var conn = pool.GetActive();
        var result = await conn.SendAsync(ProtocolMethods.OpenAsset, new { assetUrl });
        return result?.ToString() ?? "No result";
    }
}
