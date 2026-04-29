using System.ComponentModel;
using Zafiro.Avalonia.Mcp.Protocol;
using Zafiro.Avalonia.Mcp.Tool.Connection;
using ModelContextProtocol.Server;

namespace Zafiro.Avalonia.Mcp.Tool.Tools;

[McpServerToolType]
public sealed class ResourceTools
{
    [McpServerTool(Name = "get_resources"), Description("""
        Get resource dictionary entries (brushes, styles, templates, theme keys) from Application or a specific element. Omit selector for app-level. Set onlySelf=true to skip inherited resources.
        Returns: array of {key, type, value}.
        Example: [{"key":"PrimaryBrush","type":"SolidColorBrush","value":"#0066CC"}]
        """)]
    public static async Task<string> GetResources(
        ConnectionPool pool,
        [Description("CSS-like selector to scope resources to. Omit for Application resources.")] string? selector = null,
        [Description("Only resources defined on the element itself")] bool onlySelf = false)
    {
        var conn = pool.GetActive();
        var parms = new Dictionary<string, object?> { ["onlySelf"] = onlySelf };
        if (selector is not null) parms["selector"] = selector;

        return await conn.InvokeAsync(ProtocolMethods.GetResources, parms, "No resources");
    }

    [McpServerTool(Name = "list_assets"), Description("""
        List all embedded assets (images, fonts, XAML, etc.) in the running app. Use to discover avares:// URLs to feed into open_asset or to verify an asset is shipped.
        Returns: array of avares:// URLs.
        Example: ["avares://MyApp/Assets/logo.png","avares://MyApp/Views/MainView.axaml"]
        """)]
    public static async Task<string> ListAssets(ConnectionPool pool)
    {
        var conn = pool.GetActive();
        return await conn.InvokeAsync(ProtocolMethods.ListAssets, empty: "No assets");
    }

    [McpServerTool(Name = "open_asset"), Description("""
        Download an embedded asset by its avares:// URL. Run list_assets first if you don't know the URL. For XAML in particular, get_xaml is a faster shortcut.
        Returns: {url, mimeType, content} (base64 for binary).
        Example: {"url":"avares://MyApp/Views/MainView.axaml","mimeType":"text/xml","content":"<UserControl ...>...</UserControl>"}
        """)]
    public static async Task<string> OpenAsset(
        ConnectionPool pool,
        [Description("Asset URL (avares://...)")] string assetUrl)
    {
        var conn = pool.GetActive();
        return await conn.InvokeAsync(ProtocolMethods.OpenAsset, new { assetUrl });
    }
}
