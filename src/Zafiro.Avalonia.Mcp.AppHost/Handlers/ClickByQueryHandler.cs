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
            var clickResult = PerformClick(visual);

            return new { success = true, nodeId, type = visual.GetType().Name, text = GetText(visual), clickResult };
        });
    }

    private static List<Visual> FindMatchingVisuals(string query, string? role)
    {
        var results = new List<Visual>();

        foreach (var window in NodeRegistry.GetWindows())
        {
            foreach (var visual in window.GetVisualDescendants())
            {
                // Same interactivity filter as InteractablesHandler
                if (!IsInteractive(visual)) continue;
                if (!MatchesQuery(visual, query)) continue;
                if (role != null && !MatchesRole(visual, role)) continue;

                results.Add(visual);
            }
        }

        return results;
    }

    /// <summary>
    /// Mirrors InteractablesHandler.IsInteractive — only returns controls the user can act on.
    /// </summary>
    private static bool IsInteractive(Visual visual)
    {
        if (!visual.IsVisible) return false;

        if (visual is InputElement input)
        {
            if (!input.IsEnabled) return false;
            if (input.Focusable) return true;
        }

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
        "listitem" => visual is ListBoxItem,
        "menuitem" => visual is MenuItem,
        "togglebutton" => visual is ToggleButton,
        _ => true,
    };

    private static string PerformClick(Visual visual)
    {
        if (visual is ToggleButton toggle)
        {
            toggle.IsChecked = visual is RadioButton ? true : toggle.IsChecked != true;
            return "toggle";
        }

        if (visual is Button button)
        {
            if (button.Command is { } cmd && cmd.CanExecute(button.CommandParameter))
            {
                cmd.Execute(button.CommandParameter);
                return "command";
            }
            button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            return "click_event";
        }

        if (visual is MenuItem menuItem)
        {
            if (menuItem.Command is { } miCmd && miCmd.CanExecute(menuItem.CommandParameter))
            {
                miCmd.Execute(menuItem.CommandParameter);
                return "menu_command";
            }
            menuItem.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
            return "menu_click";
        }

        if (visual is Control control)
        {
            if (control is ListBoxItem lbi)
            {
                var lb = lbi.GetVisualAncestors().OfType<ListBox>().FirstOrDefault();
                if (lb is not null)
                {
                    var idx = lb.IndexFromContainer(lbi);
                    if (idx >= 0) lb.SelectedIndex = idx;
                    return "listbox_select";
                }
            }

            if (control is TabItem ti)
            {
                var tc = ti.GetVisualAncestors().OfType<TabControl>().FirstOrDefault();
                if (tc is not null)
                {
                    var idx = tc.IndexFromContainer(ti);
                    if (idx >= 0) tc.SelectedIndex = idx;
                    return "tab_select";
                }
            }

            if (control.Focusable) control.Focus();
            control.RaiseEvent(new RoutedEventArgs(InputElement.PointerPressedEvent));
            control.RaiseEvent(new RoutedEventArgs(InputElement.PointerReleasedEvent));
            return "pointer_simulation";
        }

        return "no_action";
    }

    /// <summary>
    /// Extracts text from a visual, walking visual children for containers (same as InteractablesHandler).
    /// </summary>
    private static string? GetText(Visual visual) => visual switch
    {
        TextBox tb => tb.Text,
        TextBlock tb => tb.Text,
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
