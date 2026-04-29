using System.ComponentModel;
using Zafiro.Avalonia.Mcp.Protocol;
using Zafiro.Avalonia.Mcp.Tool.Connection;
using ModelContextProtocol.Server;

namespace Zafiro.Avalonia.Mcp.Tool.Tools;

[McpServerToolType]
public sealed class PropertyTools
{
    [McpServerTool(Name = "get_props"), Description("""
        Get Avalonia property values for an element. Pass propertyNames="Width,Height,Background" to limit output. For applied styles use get_styles, for bindings use get_bindings.
        Returns: array of {name, value, type}.
        Example: [{"name":"Width","value":"200","type":"Double"},{"name":"IsEnabled","value":"True","type":"Boolean"}]
        """)]
    public static async Task<string> GetProps(
        ConnectionPool pool,
        [Description("CSS-like selector identifying the element")] string selector,
        [Description("Optional property names to filter (comma-separated)")] string? propertyNames = null)
    {
        var conn = pool.GetActive();
        var parms = new Dictionary<string, object> { ["selector"] = selector };
        if (propertyNames is not null)
            parms["propertyNames"] = propertyNames.Split(',', StringSplitOptions.TrimEntries);

        return await conn.InvokeAsync(ProtocolMethods.GetProperties, parms, "No properties");
    }

    [McpServerTool(Name = "set_prop"), Description("""
        Set a property value. Strings are parsed to the property's type; pass value="unset" to clear. Set isXamlValue=true for XAML markup like brushes ("#FF0000") or transforms. For numeric Slider/NumericUpDown values prefer set_value. Use get_prop_values first to know valid options.
        Returns: confirmation with the new value.
        Example: "Set Background = #FF0000 on Border#7"
        """)]
    public static async Task<string> SetProp(
        ConnectionPool pool,
        [Description("CSS-like selector identifying the element")] string selector,
        [Description("Property name to set")] string propertyName,
        [Description("Value to set (use 'unset' to clear)")] string value,
        [Description("Whether the value is XAML markup")] bool isXamlValue = false)
    {
        var conn = pool.GetActive();
        return await conn.InvokeAsync(ProtocolMethods.SetProperty,
            new { selector, propertyName, value, isXamlValue });
    }

    [McpServerTool(Name = "get_prop_values"), Description("""
        Get the allowed values for a property: enum members, true/false for booleans, or TypeConverter standard values. Call BEFORE set_prop to discover valid input.
        Returns: array of allowed string values, or null if unconstrained.
        Example: ["Left","Center","Right","Stretch"]
        """)]
    public static async Task<string> GetPropertyValues(
        ConnectionPool pool,
        [Description("Node ID of the element")] int nodeId,
        [Description("Property name to query")] string propertyName)
    {
        var conn = pool.GetActive();
        return await conn.InvokeAsync(ProtocolMethods.GetPropertyValues, new { nodeId, propertyName });
    }

    [McpServerTool(Name = "get_styles"), Description("""
        Get the styles currently applied to an element with their selectors and setters. Use to debug "why does this control look like that?". Set includeDefaults=true to also see unset/default values; filter via propertyNames.
        Returns: array of {selector, setters:[{property, value}]}.
        Example: [{"selector":"Button.primary","setters":[{"property":"Background","value":"#0066CC"}]}]
        """)]
    public static async Task<string> GetStyles(
        ConnectionPool pool,
        [Description("CSS-like selector identifying the element")] string selector,
        [Description("Include default/unset values")] bool includeDefaults = false,
        [Description("Optional property names to filter (comma-separated)")] string? propertyNames = null)
    {
        var conn = pool.GetActive();
        var parms = new Dictionary<string, object> { ["selector"] = selector, ["includeDefaults"] = includeDefaults };
        if (propertyNames is not null)
            parms["propertyNames"] = propertyNames.Split(',', StringSplitOptions.TrimEntries);

        return await conn.InvokeAsync(ProtocolMethods.GetStyles, parms, "No styles");
    }
}
