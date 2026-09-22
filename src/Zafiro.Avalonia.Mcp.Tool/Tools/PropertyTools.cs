using System.ComponentModel;
using Zafiro.Avalonia.Mcp.Protocol;
using Zafiro.Avalonia.Mcp.Tool.Connection;
using ModelContextProtocol.Server;

namespace Zafiro.Avalonia.Mcp.Tool.Tools;

[McpServerToolType]
public sealed class PropertyTools
{
    [McpServerTool(Name = "get_props"), Description("""
        Get Avalonia effective property values for an element. Pass propertyNames="Width,Height,Background" to limit output. Owner-qualified names, including attached properties such as Grid.Row, are supported. This is the inexpensive value query; use explain_property for provenance and get_styles for actual attached styles.
        Returns: array of {name, owner, value, type, priority}.
        Example: [{"name":"Width","owner":"Layoutable","value":"200","type":"Double","priority":"LocalValue"},{"name":"Grid.Row","owner":"Grid","value":"1","type":"Int32","priority":"LocalValue"}]
        """)]
    public static async Task<string> GetProps(
        ConnectionPool pool,
        [Description("CSS-like selector identifying the element")] string selector,
        [Description("Optional property names to filter (comma-separated); qualify attached properties as Owner.Name")] string? propertyNames = null)
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
        [Description("CSS-like selector identifying the element")] string selector,
        [Description("Property name to query")] string propertyName)
    {
        var conn = pool.GetActive();
        return await conn.InvokeAsync(ProtocolMethods.GetPropertyValues, new { selector, propertyName });
    }

    [McpServerTool(Name = "get_styles"), Description("""
        Inspect actual attached style and control-theme sources in one UI-thread snapshot, with normal classes separated from pseudoclasses. Set includeInactive=true to include inactive attached triggers; unknown activation is always reported. propertyNames filters setters by unambiguous owner-qualified property registration. Does not simulate selector matching or activate bindings.
        Returns: {target:{sourceId,type,nodeId},timestamp,classState:{classes,pseudoClasses},styles:[{sourceId,kind,selector,scope,active,applicationState,priority,framePriority,frameOrder,setters:[{property,provider,wins,declarationOrder}]}],resolution:{status,unavailable,truncated}}. Providers describe declared/cached entries, not the target's overriding effective value. Results are bounded; unavailable source evidence is explicit.
        Example: get_styles(selector="#Save",includeInactive=true,propertyNames="TemplatedControl.Background")
        """)]
    public static async Task<string> GetStyles(
        ConnectionPool pool,
        [Description("CSS-like selector identifying the element")] string selector,
        [Description("Include attached inactive styles and themes")] bool includeInactive = false,
        [Description("Optional unambiguous property names to filter (comma-separated, at most 64)")] string? propertyNames = null)
    {
        var conn = pool.GetActive();
        var parms = new Dictionary<string, object> { ["selector"] = selector, ["includeInactive"] = includeInactive };
        if (propertyNames is not null)
            parms["propertyNames"] = propertyNames.Split(',', StringSplitOptions.TrimEntries);

        return await conn.InvokeAsync(ProtocolMethods.GetStyles, parms, "No styles");
    }

    [McpServerTool(Name = "explain_property"), Description("""
        Explain why one Avalonia property has its effective value, using retained runtime entry identity rather than comparing values. Owner-qualified and attached property names are supported; ambiguous properties or elements are errors. Read-only, bounded, and captured with class/pseudoclass state in one timestamped UI-thread snapshot.
        Returns: {target, timestamp, property:{id,name,owner,type,registrationKind,inherits},effectiveValue,priority,classState,origin,baseValue,isCurrentValueOverride,hasCoercion,isCoercedDefault,defaultValue,candidates?,resolution:{status,unavailable,truncated}}. origin/provider distinguish styles, themes, local bindings, template bindings, dynamic resources, inheritance, defaults and animations when retained. Direct properties have no styled priority stack. Lost AXAML locations, static-resource origins, assignment history and unsupported evidence are explicitly partial. Source IDs are shared with get_styles.
        Example: explain_property(selector="#Save",propertyName="TemplatedControl.Background",includeCandidates=true,maxDepth=8)
        """)]
    public static async Task<string> ExplainProperty(
        ConnectionPool pool,
        [Description("Selector identifying exactly one element")] string selector,
        [Description("Property name, qualified by its owner when ambiguous (for example Grid.Row)")] string propertyName,
        [Description("Include competing retained runtime entries and why they did not win")] bool includeCandidates = false,
        [Description("Maximum inheritance/resource trace depth, between 1 and 32")] int maxDepth = 8)
    {
        var connection = pool.GetActive();
        return await connection.InvokeAsync(ProtocolMethods.ExplainProperty,
            new { selector, propertyName, includeCandidates, maxDepth });
    }
}
