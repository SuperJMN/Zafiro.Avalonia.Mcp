using System.Text.Json;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Zafiro.Avalonia.Mcp.Protocol;
using Zafiro.Avalonia.Mcp.Protocol.Messages;

namespace Zafiro.Avalonia.Mcp.AppHost.Handlers;

/// <summary>
/// Atomic search-and-click: finds an interactive control by query and clicks it in a single UI thread dispatch.
/// Uses the same interactivity filter as <see cref="InteractablesHandler"/> so results are consistent.
/// </summary>
public sealed class ClickByQueryHandler : IRequestHandler
{
    public string Method => ProtocolMethods.ClickByQuery;

    public async Task<object> Handle(DiagnosticRequest request)
    {
        string? query = null;
        string? role = null;
        int occurrence = 0;

        if (request.Params is JsonElement p)
        {
            if (p.TryGetProperty("query", out var q)) query = q.GetString();
            if (p.TryGetProperty("role", out var r)) role = r.GetString();
            if (p.TryGetProperty("occurrence", out var o)) occurrence = o.GetInt32();
        }

        if (string.IsNullOrWhiteSpace(query))
            return new { error = "query is required" };

        return await Dispatcher.UIThread.InvokeAsync<object>(() =>
        {
            var candidates = FindMatchingVisuals(query, role);

            if (candidates.Count == 0)
                return new { error = $"No interactive element found matching query '{query}'" + (role != null ? $" with role '{role}'" : "") };

            if (occurrence >= candidates.Count)
                return new { error = $"Occurrence {occurrence} out of range. Found {candidates.Count} match(es).", matchCount = candidates.Count };

            var visual = candidates[occurrence];
            var nodeId = NodeRegistry.GetOrRegister(visual);
            var clickResult = InputHandler.Click(visual);

            if (clickResult is HandlerErrorResult)
                return clickResult;

            return new
            {
                success = true,
                nodeId,
                type = visual.GetType().Name,
                text = GetText(visual),
                clickResult = TryGetResultMethod(clickResult)
            };
        });
    }

    private static List<Visual> FindMatchingVisuals(string query, string? role)
    {
        var results = new List<Visual>();

        foreach (var window in NodeRegistry.GetInspectableRoots())
        {
            foreach (var visual in window.GetVisualDescendants())
            {
                // Same interactivity filter as InteractablesHandler
                if (!IsClickable(visual)) continue;
                if (!MatchesQuery(visual, query)) continue;
                if (role != null && !MatchesRole(visual, role)) continue;

                results.Add(visual);
            }
        }

        return results;
    }

    /// <summary>
    /// Returns controls with click semantics. Enabled-state validation belongs to
    /// <see cref="InputHandler.Click(Visual)"/> so failures remain structured and truthful.
    /// </summary>
    private static bool IsClickable(Visual visual)
    {
        if (!visual.IsVisible) return false;

        if (visual is InputElement { Focusable: true }) return true;

        return visual is Button
            or MenuItem
            or ListBoxItem
            or TabItem
            or ComboBoxItem;
    }

    private static bool MatchesQuery(Visual visual, string query)
    {
        if (visual.GetType().Name.Contains(query, StringComparison.OrdinalIgnoreCase))
            return true;

        if (visual is Control c && c.Name?.Contains(query, StringComparison.OrdinalIgnoreCase) == true)
            return true;

        // Check AutomationId (commonly used to identify controls)
        if (visual is Control ctrl)
        {
            var automationId = AutomationProperties.GetAutomationId(ctrl);
            if (automationId?.Contains(query, StringComparison.OrdinalIgnoreCase) == true)
                return true;
        }

        var text = GetText(visual);
        if (text?.Contains(query, StringComparison.OrdinalIgnoreCase) == true)
            return true;

        return false;
    }

    private static bool MatchesRole(Visual visual, string role) => role.ToLowerInvariant() switch
    {
        "button" => visual is Button,
        "textbox" => visual is TextBox,
        "checkbox" => visual is CheckBox,
        "radio" => visual is RadioButton,
        "combobox" => visual is ComboBox,
        "tab" => visual is TabItem,
        "listitem" or "treeitem" => visual is ListBoxItem or TreeViewItem,
        "menuitem" => visual is MenuItem,
        "togglebutton" => visual is ToggleButton,
        _ => true,
    };

    private static string? TryGetResultMethod(object result)
    {
        var property = result.GetType().GetProperty("method");
        return property?.GetValue(result) as string;
    }

    /// <summary>
    /// Extracts text from a visual, walking visual children for containers (same as InteractablesHandler).
    /// </summary>
    private static string? GetText(Visual visual) => visual switch
    {
        TextBox tb => tb.Text,
        TextBlock tb => tb.Text,
        HeaderedSelectingItemsControl hsic => hsic.Header as string ?? GetTextFromVisualChildren(hsic),
        HeaderedItemsControl hic => hic.Header as string ?? GetTextFromVisualChildren(hic),
        HeaderedContentControl hcc => hcc.Header as string ?? hcc.Content as string,
        ContentControl cc => cc.Content as string ?? GetAutomationName(cc) ?? GetTextFromVisualChildren(cc),
        _ => GetAutomationName(visual) ?? GetTextFromVisualChildren(visual),
    };

    private static string? GetAutomationName(Visual visual)
    {
        if (visual is Control control)
        {
            var name = AutomationProperties.GetName(control);
            if (!string.IsNullOrEmpty(name)) return name;
        }
        return null;
    }

    private static string? GetTextFromVisualChildren(Visual visual)
    {
        var texts = visual.GetVisualDescendants()
            .OfType<TextBlock>()
            .Where(tb => tb.IsVisible && !string.IsNullOrWhiteSpace(tb.Text))
            .Select(tb => tb.Text!)
            .Take(5)
            .ToList();
        return texts.Count > 0 ? string.Join(" · ", texts) : null;
    }
}
