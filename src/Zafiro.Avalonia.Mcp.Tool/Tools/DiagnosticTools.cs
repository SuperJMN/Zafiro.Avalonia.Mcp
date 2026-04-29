using System.ComponentModel;
using Zafiro.Avalonia.Mcp.Protocol;
using Zafiro.Avalonia.Mcp.Tool.Connection;
using ModelContextProtocol.Server;

namespace Zafiro.Avalonia.Mcp.Tool.Tools;

[McpServerToolType]
public sealed class DiagnosticTools
{
    [McpServerTool(Name = "get_item"), Description("""
        Realize and return the container for a specific item inside an ItemsControl (ListBox, TreeView, ItemsRepeater, etc.) by index, visible text, or data-context property match. USE THIS for virtualized lists where the container doesn't exist yet — it forces ScrollIntoView + layout to materialize the item. Provide exactly one of: index, text, or dcMatchPath+dcMatchValue.
        Returns: {nodeId, type, index, isRealized, text?} for the realized container, or {error} if not found.
        Example: {"nodeId":77,"type":"ListBoxItem","index":50,"isRealized":true,"text":"Item 50"}
        """)]
    public static async Task<string> GetItem(
        ConnectionPool pool,
        [Description("Selector pointing to the ItemsControl (e.g. 'ListBox', '#myList')")] string selector,
        [Description("Zero-based index of the item to realize")] int? index = null,
        [Description("Realize the first item whose ToString() contains this text (case-insensitive)")] string? text = null,
        [Description("Dot-separated property path on the item's DataContext to match (use with dcMatchValue)")] string? dcMatchPath = null,
        [Description("Value to match against dcMatchPath (string equality, case-insensitive)")] string? dcMatchValue = null)
    {
        var conn = pool.GetActive();
        var parms = new Dictionary<string, object?> { ["selector"] = selector };
        if (index is not null) parms["index"] = index;
        if (text is not null) parms["text"] = text;
        if (dcMatchPath is not null || dcMatchValue is not null)
            parms["dcMatch"] = new { path = dcMatchPath ?? "", value = dcMatchValue ?? "" };
        return await conn.InvokeAsync(ProtocolMethods.GetItem, parms);
    }

    [McpServerTool(Name = "get_layout_info"), Description("""
        Get a comprehensive layout snapshot of a control: bounds, screen position, size constraints, alignment, visibility, and parent. Use to diagnose "why isn't my element showing?" or "why is it sized weirdly?".
        Returns: {bounds, screenBounds, desiredSize, renderSize, margin, padding, horizontalAlignment, verticalAlignment, width, height, minWidth, minHeight, maxWidth, maxHeight, isVisible, isEffectivelyVisible, isEnabled, isHitTestVisible, clipToBounds, isMeasureValid, isArrangeValid, parent}.
        Example: {"bounds":{"x":10,"y":20,"w":100,"h":30},"isVisible":true,"isEffectivelyVisible":false,"margin":{"l":4,"t":0,"r":4,"b":0}}
        """)]
    public static async Task<string> GetLayoutInfo(
        ConnectionPool pool,
        [Description("Selector identifying the target element (e.g. 'Button', '#myBtn')")] string selector)
    {
        var conn = pool.GetActive();
        return await conn.InvokeAsync(ProtocolMethods.GetLayoutInfo, new { selector });
    }

    [McpServerTool(Name = "get_validation_errors"), Description("""
        Get all active data-validation errors in the current UI, optionally scoped to a CSS-like selector. Use when a Save button stays disabled or a form won't submit — shows WHICH controls have errors and WHY.
        Returns: {scope, count, items:[{nodeId, type, name, hasErrors, errors:[{message, source}]}]}.
        Example: {"scope":"app","count":1,"items":[{"nodeId":42,"type":"TextBox","name":"emailBox","hasErrors":true,"errors":[{"message":"required","source":"Exception"}]}]}
        """)]
    public static async Task<string> GetValidationErrors(
        ConnectionPool pool,
        [Description("CSS-like selector to scope the search (e.g. 'TextBox', '#myForm'). Omit for app-wide.")] string? selector = null)
    {
        var conn = pool.GetActive();
        var parms = new Dictionary<string, object?>();
        if (!string.IsNullOrWhiteSpace(selector)) parms["selector"] = selector;
        return await conn.InvokeAsync(ProtocolMethods.GetValidationErrors, parms, "No validation errors");
    }

    [McpServerTool(Name = "get_focus"), Description("""
        Get the currently keyboard-focused UI element. Use to answer "where am I?" without screenshotting.
        Returns: {nodeId, type, name, windowNodeId} or {focused: null} when nothing is focused.
        Example: {"nodeId":42,"type":"TextBox","name":"SearchBox","windowNodeId":1}
        """)]
    public static async Task<string> GetFocus(ConnectionPool pool)
    {
        var conn = pool.GetActive();
        return await conn.InvokeAsync(ProtocolMethods.GetFocus, new { }, "No focused element");
    }

    [McpServerTool(Name = "get_active_window"), Description("""
        Get the currently active (topmost/focused) window and a list of all open windows. Use to orient navigation.
        Returns: {activeWindow:{nodeId,title,isActive}, openWindows:[{nodeId,title,isActive}]}.
        Example: {"activeWindow":{"nodeId":1,"title":"Main","isActive":true},"openWindows":[{"nodeId":1,"title":"Main","isActive":true}]}
        """)]
    public static async Task<string> GetActiveWindow(ConnectionPool pool)
    {
        var conn = pool.GetActive();
        return await conn.InvokeAsync(ProtocolMethods.GetActiveWindow, new { }, "No active window");
    }

    [McpServerTool(Name = "get_open_dialogs"), Description("""
        Get all currently open modal/dialog windows (windows with an Owner set via ShowDialog). Use to detect blocking dialogs.
        Returns: [{nodeId, title, isModal, owner:{nodeId,title}}]. Empty array when no dialogs are open.
        Example: [{"nodeId":5,"title":"Confirm","isModal":true,"owner":{"nodeId":1,"title":"Main"}}]
        """)]
    public static async Task<string> GetOpenDialogs(ConnectionPool pool)
    {
        var conn = pool.GetActive();
        return await conn.InvokeAsync(ProtocolMethods.GetOpenDialogs, new { }, "[]");
    }

    [McpServerTool(Name = "get_command_info"), Description("""
        Inspect the ICommand attached to a control (Button, MenuItem, etc.) and explain WHY it is enabled or disabled. Distinguishes between command CanExecute=false, the control's own IsEnabled=false, and a parent container being disabled. Use when an agent needs to understand why a button is greyed out.
        Returns: {nodeId, commandType, parameter, canExecute, isEnabled, enableReason, matchedCount?}. enableReason is one of: enabled, command_cannot_execute, is_enabled_false, parent_disabled.
        Example: {"nodeId":42,"commandType":"ReactiveUI.ReactiveCommand`2","parameter":null,"canExecute":false,"isEnabled":false,"enableReason":"command_cannot_execute"}
        """)]
    public static async Task<string> GetCommandInfo(
        ConnectionPool pool,
        [Description("Selector identifying the target control (e.g. 'Button', '#saveBtn', 'Button:has-text(\"Save\")')")] string selector)
    {
        var conn = pool.GetActive();
        return await conn.InvokeAsync(ProtocolMethods.GetCommandInfo, new { selector }, "No command info");
    }

    [McpServerTool(Name = "find_by_datacontext"), Description("""
        Find UI elements by evaluating a live C# predicate against their DataContext (ViewModel).
        The predicate is a boolean C# expression with direct access to DataContext properties
        (e.g. predicate="Id == 42 && IsActive"). Scope the search with selector (default "*" = all elements).
        Returns: {count, items:[{nodeId, type, name, dataContextType}]}.
        Example: {"count":1,"items":[{"nodeId":7,"type":"ListBoxItem","name":null,"dataContextType":"MyApp.VmRow"}]}
        """)]
    public static async Task<string> FindByDataContext(
        ConnectionPool pool,
        [Description("C# boolean expression evaluated against the DataContext, e.g. \"Id == 42 && IsActive\"")] string predicate,
        [Description("Selector to scope the search (default \"*\" = all elements)")] string selector = "*")
    {
        var conn = pool.GetActive();
        return await conn.InvokeAsync(ProtocolMethods.FindByDataContext, new { selector, predicate }, "No matches");
    }
}
