using System.Text.Json;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Zafiro.Avalonia.Mcp.Protocol;
using Zafiro.Avalonia.Mcp.Protocol.Messages;
using Zafiro.Avalonia.Mcp.Protocol.Models;

namespace Zafiro.Avalonia.Mcp.AppHost.Handlers;

public sealed class InteractablesHandler : IRequestHandler
{
    public string Method => ProtocolMethods.GetInteractables;

    public async Task<object> Handle(DiagnosticRequest request)
    {
        int? nodeId = null;

        if (request.Params is JsonElement p)
        {
            if (p.TryGetProperty("nodeId", out var nid)) nodeId = nid.GetInt32();
        }

        return await Dispatcher.UIThread.InvokeAsync<object>(() =>
        {
            IEnumerable<Visual> searchScope;

            if (nodeId.HasValue)
            {
                var scope = NodeRegistry.Resolve(nodeId.Value);
                if (scope is null) return new { error = $"Node {nodeId} not found" };
                searchScope = new[] { scope }.Concat(scope.GetVisualDescendants());
            }
            else
            {
                searchScope = NodeRegistry.GetWindows()
                    .SelectMany(w => new[] { (Visual)w }.Concat(w.GetVisualDescendants()));
            }

            var results = new List<InteractableInfo>();

            foreach (var visual in searchScope)
            {
                if (!IsInteractive(visual)) continue;

                var control = visual as Control;
                var inputElement = visual as InputElement;

                results.Add(new InteractableInfo
                {
                    NodeId = NodeRegistry.GetOrRegister(visual),
                    Type = visual.GetType().Name,
                    Role = GetRole(visual),
                    Text = GetText(visual),
                    Name = control?.Name,
                    AutomationId = control is not null ? AutomationProperties.GetAutomationId(control) : null,
                    IsEnabled = inputElement?.IsEnabled ?? true,
                    IsFocused = inputElement?.IsFocused ?? false,
                    Value = GetValue(visual),
                });
            }

            return results;
        });
    }

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

    private static string GetRole(Visual visual) => visual switch
    {
        TextBox => "textbox",
        CheckBox => "checkbox",
        RadioButton => "radio",
        ToggleSwitch => "switch",
        ToggleButton => "togglebutton",
        Button => "button",
        ComboBox => "combobox",
        Slider => "slider",
        NumericUpDown => "numericupdown",
        ListBoxItem => "listitem",
        TabItem => "tab",
        MenuItem => "menuitem",
        TreeViewItem => "treeitem",
        Expander => "expander",
        DatePicker => "datepicker",
        CalendarDatePicker => "datepicker",
        AutoCompleteBox => "combobox",
        _ => "interactive",
    };

    private static string? GetText(Visual visual) => visual switch
    {
        TextBox tb => tb.Text,
        TextBlock tb => tb.Text,
        HeaderedContentControl hcc => hcc.Header as string ?? GetContentText(hcc),
        ContentControl cc => GetContentText(cc) ?? GetTextFromVisualChildren(cc),
        _ => GetAutomationName(visual) ?? GetTextFromVisualChildren(visual),
    };

    private static string? GetContentText(ContentControl cc)
    {
        return cc.Content as string;
    }

    private static string? GetAutomationName(Visual visual)
    {
        if (visual is Control control)
        {
            var name = AutomationProperties.GetName(control);
            if (!string.IsNullOrEmpty(name)) return name;
        }
        return null;
    }

    /// <summary>
    /// Walk visual children to extract text (for ListBoxItem and similar containers).
    /// Returns the concatenation of visible TextBlock texts.
    /// </summary>
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

    private static string? GetValue(Visual visual)
    {
        var raw = visual switch
        {
            TextBox tb => tb.Text,
            CheckBox cb => cb.IsChecked?.ToString(),
            ToggleSwitch ts => ts.IsChecked?.ToString(),
            ToggleButton tb => tb.IsChecked?.ToString(),
            Slider s => s.Value.ToString(),
            NumericUpDown nud => nud.Value?.ToString(),
            ComboBox cb => cb.SelectedItem?.ToString(),
            _ => null,
        };
        // Truncate verbose values (e.g. full ViewModel ToString)
        return raw is { Length: > 80 } ? raw[..77] + "..." : raw;
    }
}
