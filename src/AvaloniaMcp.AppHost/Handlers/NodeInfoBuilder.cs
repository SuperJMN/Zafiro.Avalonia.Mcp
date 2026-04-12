using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.VisualTree;
using AvaloniaMcp.Protocol.Models;

namespace AvaloniaMcp.AppHost.Handlers;

public static class NodeInfoBuilder
{
    public static NodeInfo Create(Visual visual, List<NodeInfo>? children = null)
    {
        var id = NodeRegistry.GetOrRegister(visual);
        var parent = visual.GetVisualParent();

        return new NodeInfo
        {
            NodeId = id,
            Type = visual.GetType().Name,
            Name = (visual as Control)?.Name,
            Bounds = new BoundsInfo
            {
                X = visual.Bounds.X,
                Y = visual.Bounds.Y,
                Width = visual.Bounds.Width,
                Height = visual.Bounds.Height
            },
            IsVisible = visual.IsVisible,
            Text = GetText(visual),
            IsEnabled = (visual as InputElement)?.IsEnabled,
            IsFocused = (visual as InputElement)?.IsFocused,
            IsInteractive = GetIsInteractive(visual),
            AutomationId = GetAutomationId(visual),
            Role = GetRole(visual),
            ClassName = GetClassName(visual),
            ParentId = parent is not null ? NodeRegistry.GetOrRegister(parent) : null,
            Children = children
        };
    }

    private static string? GetText(Visual visual)
    {
        if (visual is TextBlock tb)
            return tb.Text;

        if (visual is ContentControl cc && cc.Content is string contentStr)
            return contentStr;

        if (visual is HeaderedContentControl hcc && hcc.Header is string headerStr)
            return headerStr;

        if (visual is TextBox textBox)
            return textBox.Text;

        if (visual is Control control)
        {
            var automationName = AutomationProperties.GetName(control);
            if (!string.IsNullOrEmpty(automationName))
                return automationName;
        }

        return null;
    }

    private static string? GetRole(Visual visual) => visual switch
    {
        CheckBox => "checkbox",
        RadioButton => "radio",
        ToggleButton => "button",
        RepeatButton => "button",
        Button => "button",
        MaskedTextBox => "textbox",
        AutoCompleteBox => "textbox",
        TextBox => "textbox",
        ListBoxItem => "listitem",
        TreeViewItem => "listitem",
        ComboBox => "combobox",
        Slider => "slider",
        TabItem => "tab",
        MenuItem => "menuitem",
        TextBlock => "text",
        _ => null
    };

    private static bool? GetIsInteractive(Visual visual)
    {
        if (visual is not InputElement input)
            return null;

        if (!input.IsEnabled || !input.IsHitTestVisible)
            return false;

        if (input.Focusable)
            return true;

        return visual is Button or RepeatButton or ToggleButton or MenuItem
            or TextBox or MaskedTextBox or AutoCompleteBox
            or CheckBox or RadioButton or ComboBox or Slider;
    }

    private static string? GetAutomationId(Visual visual)
    {
        if (visual is not Control control)
            return null;

        var automationId = AutomationProperties.GetAutomationId(control);
        return string.IsNullOrEmpty(automationId) ? null : automationId;
    }

    private static string? GetClassName(Visual visual)
    {
        if (visual is not StyledElement styled)
            return null;

        var classes = styled.Classes;
        if (classes.Count == 0)
            return null;

        var joined = string.Join(" ", classes);
        return string.IsNullOrEmpty(joined) ? null : joined;
    }
}
