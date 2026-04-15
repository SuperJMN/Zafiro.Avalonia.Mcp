using System.ComponentModel;
using Zafiro.Avalonia.Mcp.Protocol;
using Zafiro.Avalonia.Mcp.Tool.Connection;
using ModelContextProtocol.Server;

namespace Zafiro.Avalonia.Mcp.Tool.Tools;

[McpServerToolType]
public sealed class PropertyTools
{
    [McpServerTool(Name = "get_props"), Description("Get property values for a UI element. Returns all Avalonia property names, values, and types. Use the optional propertyNames filter to retrieve specific properties and reduce output size.")]
    public static async Task<string> GetProps(
        ConnectionPool pool,
        [Description("Node ID of the element")] int nodeId,
        [Description("Optional property names to filter (comma-separated)")] string? propertyNames = null)
    {
        var conn = pool.GetActive();
        var parms = new Dictionary<string, object> { ["nodeId"] = nodeId };
        if (propertyNames is not null)
            parms["propertyNames"] = propertyNames.Split(',', StringSplitOptions.TrimEntries);

        var result = await conn.SendAsync(ProtocolMethods.GetProperties, parms);
        return result?.ToString() ?? "No properties";
    }

    [McpServerTool(Name = "set_prop"), Description("Set a property value on a UI element. Accepts string values that are parsed to the property's type. Use 'unset' as the value to clear a property back to its default. Set isXamlValue=true to parse complex XAML markup (e.g., brushes, transforms).")]
    public static async Task<string> SetProp(
        ConnectionPool pool,
        [Description("Node ID of the element")] int nodeId,
        [Description("Property name to set")] string propertyName,
        [Description("Value to set (use 'unset' to clear)")] string value,
        [Description("Whether the value is XAML markup")] bool isXamlValue = false)
    {
        var conn = pool.GetActive();
        var result = await conn.SendAsync(ProtocolMethods.SetProperty,
            new { nodeId, propertyName, value, isXamlValue });
        return result?.ToString() ?? "No result";
    }

    [McpServerTool(Name = "get_styles"), Description("Get the applied styles and setters for a UI element. Shows which styles are active, their selectors, and the property values they set. Useful for debugging styling issues. Set includeDefaults=true to also see properties with default/unset values.")]
    public static async Task<string> GetStyles(
        ConnectionPool pool,
        [Description("Node ID of the element")] int nodeId,
        [Description("Include default/unset values")] bool includeDefaults = false,
        [Description("Optional property names to filter (comma-separated)")] string? propertyNames = null)
    {
        var conn = pool.GetActive();
        var parms = new Dictionary<string, object> { ["nodeId"] = nodeId, ["includeDefaults"] = includeDefaults };
        if (propertyNames is not null)
            parms["propertyNames"] = propertyNames.Split(',', StringSplitOptions.TrimEntries);

        var result = await conn.SendAsync(ProtocolMethods.GetStyles, parms);
        return result?.ToString() ?? "No styles";
    }
}
