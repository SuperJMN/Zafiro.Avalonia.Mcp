using System.Text.Json;
using System.Text.Json.Serialization;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Zafiro.Avalonia.Mcp.AppHost.Selectors;
using Zafiro.Avalonia.Mcp.Protocol;
using Zafiro.Avalonia.Mcp.Protocol.Messages;

namespace Zafiro.Avalonia.Mcp.AppHost.Handlers;

/// <summary>
/// Returns a compact spatial snapshot of the UI: all visible text and interactive
/// controls in a single flat list sorted by reading order (top-to-bottom, left-to-right).
/// Each entry includes nodeId, role, text, current value, and absolute position/size.
/// This replaces the common pattern of calling get_screen_text + get_interactables separately.
/// </summary>
public sealed class SnapshotHandler : IRequestHandler
{
    public string Method => ProtocolMethods.GetSnapshot;

    public async Task<object> Handle(DiagnosticRequest request)
    {
        string? selector = null;
        var visibleOnly = true;

        if (request.Params is JsonElement p)
        {
            if (p.TryGetProperty("selector", out var s)) selector = s.GetString();
            if (p.TryGetProperty("visibleOnly", out var vo)) visibleOnly = vo.GetBoolean();
        }

        return await Dispatcher.UIThread.InvokeAsync<object>(() =>
        {
            Visual root;
            if (!string.IsNullOrWhiteSpace(selector))
            {
                var (visual, error) = SelectorRequestHelper.ResolveSingle(selector);
                if (visual is null) return error!;
                root = visual;
            }
            else
            {
                var window = NodeRegistry.GetWindows().FirstOrDefault();
                if (window is null) return (object)new { error = "No windows found" };
                root = window;
            }

            var rootVisual = FindRoot(root);
            var windowBounds = new Rect(0, 0, rootVisual.Bounds.Width, rootVisual.Bounds.Height);

            var entries = new List<SnapshotEntry>();
            Collect(root, rootVisual, entries, visibleOnly, windowBounds);

            // Deduplicate entries with identical text at nearly the same position
            // (e.g. Button → AccessText → TextBlock all emit the same label)
            var deduped = new List<SnapshotEntry>();
            foreach (var entry in entries)
            {
                var isDuplicate = deduped.Any(e =>
                    e.Text == entry.Text &&
                    e.Role == entry.Role &&
                    Math.Abs(e.X - entry.X) < 20 &&
                    Math.Abs(e.Y - entry.Y) < 20);
                if (!isDuplicate)
                    deduped.Add(entry);
            }

            // Find the focused element
            var focused = deduped.FirstOrDefault(e => e.IsFocused == true);

            var windowTitle = (root as Window)?.Title
                           ?? (rootVisual as Window)?.Title;
            var windowSize = $"{rootVisual.Bounds.Width}x{rootVisual.Bounds.Height}";

            return (object)new
            {
                window = windowTitle is not null ? $"{windowTitle} ({windowSize})" : windowSize,
                focusedId = focused?.NodeId,
                elements = deduped
            };
        });
    }

    private static Visual FindRoot(Visual visual)
    {
        var current = visual;
        while (current.GetVisualParent() is Visual parent)
            current = parent;
        return current;
    }

    private static void Collect(Visual visual, Visual rootVisual, List<SnapshotEntry> entries,
        bool visibleOnly, Rect windowBounds)
    {
        if (!visual.IsVisible) return;

        var entry = TryBuild(visual, rootVisual);
        if (entry is not null)
        {
            if (!visibleOnly || IsInViewport(visual, new Rect(entry.X, entry.Y, entry.W, entry.H), rootVisual, windowBounds))
                entries.Add(entry);
        }

        foreach (var child in visual.GetVisualChildren())
            Collect(child, rootVisual, entries, visibleOnly, windowBounds);
    }

    private static SnapshotEntry? TryBuild(Visual visual, Visual rootVisual)
    {
        var role = GetRole(visual);
        var text = GetText(visual);
        var value = GetValue(visual);

        // Include: interactive controls (always) or text elements (only if they have text)
        var isInteractive = IsInteractiveControl(visual);
        if (!isInteractive && string.IsNullOrWhiteSpace(text))
            return null;

        var transform = visual.TransformToVisual(rootVisual);
        if (!transform.HasValue) return null;

        var abs = visual.Bounds.TransformToAABB(transform.Value);
        var inputEl = visual as InputElement;

        return new SnapshotEntry
        {
            NodeId = NodeRegistry.GetOrRegister(visual),
            Role = role ?? (isInteractive ? "interactive" : "text"),
            Text = text,
            Value = value,
            X = Math.Round(abs.X, 1),
            Y = Math.Round(abs.Y, 1),
            W = Math.Round(abs.Width, 1),
            H = Math.Round(abs.Height, 1),
            IsEnabled = inputEl?.IsEnabled,
            IsFocused = inputEl?.IsFocused == true ? true : null,
            Name = (visual as Control)?.Name,
            AutomationId = GetAutomationId(visual),
        };
    }

    private static bool IsInteractiveControl(Visual visual)
    {
        if (visual is InputElement { Focusable: true } input && input.IsEnabled)
            return true;
        return visual is Button or MenuItem or ListBoxItem or TabItem or ComboBoxItem;
    }

    private static string? GetRole(Visual visual) => visual switch
    {
        TextBox => "textbox",
        CheckBox => "checkbox",
        RadioButton => "radio",
        ToggleSwitch => "switch",
        ToggleButton tb when tb is not CheckBox => "togglebutton",
        Button => "button",
        ComboBox => "combobox",
        Slider => "slider",
        NumericUpDown => "numericupdown",
        ListBoxItem => "listitem",
        TabItem => "tab",
        MenuItem => "menuitem",
        TreeViewItem => "treeitem",
        Expander => "expander",
        DatePicker or CalendarDatePicker => "datepicker",
        AutoCompleteBox => "combobox",
        TextBlock => "text",
        _ => null
    };

    private static string? GetText(Visual visual) => visual switch
    {
        TextBox tb => tb.PlaceholderText ?? tb.Text,
        TextBlock tb => tb.Text,
        HeaderedContentControl hcc => hcc.Header as string ?? GetContentString(hcc),
        ContentControl cc => GetContentString(cc) ?? GetTextFromChildren(cc),
        _ => GetAutomationName(visual) ?? GetTextFromChildren(visual)
    };

    private static string? GetContentString(ContentControl cc) => cc.Content as string;

    private static string? GetAutomationName(Visual visual)
    {
        if (visual is Control ctrl)
        {
            var name = AutomationProperties.GetName(ctrl);
            if (!string.IsNullOrEmpty(name)) return name;
        }
        return null;
    }

    private static string? GetTextFromChildren(Visual visual)
    {
        var texts = visual.GetVisualDescendants()
            .OfType<TextBlock>()
            .Where(tb => tb.IsVisible && !string.IsNullOrWhiteSpace(tb.Text))
            .Select(tb => tb.Text!)
            .Take(3);
        var joined = string.Join(" · ", texts);
        return string.IsNullOrEmpty(joined) ? null : joined;
    }

    private static string? GetValue(Visual visual)
    {
        var raw = visual switch
        {
            TextBox tb => tb.Text,
            CheckBox cb => cb.IsChecked?.ToString(),
            ToggleSwitch ts => ts.IsChecked?.ToString(),
            ToggleButton tb => tb.IsChecked?.ToString(),
            Slider s => s.Value.ToString("G4"),
            NumericUpDown nud => nud.Value?.ToString(),
            ComboBox cb => cb.SelectedItem?.ToString(),
            _ => null
        };
        return raw is { Length: > 80 } ? raw[..77] + "..." : raw;
    }

    private static string? GetAutomationId(Visual visual)
    {
        if (visual is not Control ctrl) return null;
        var id = AutomationProperties.GetAutomationId(ctrl);
        return string.IsNullOrEmpty(id) ? null : id;
    }

    private static bool IsInViewport(Visual visual, Rect absoluteBounds, Visual rootVisual, Rect windowBounds)
    {
        if (!windowBounds.Intersects(absoluteBounds)) return false;

        var current = visual.GetVisualParent();
        while (current is not null)
        {
            if (current is ScrollViewer sv)
            {
                var svTransform = sv.TransformToVisual(rootVisual);
                if (svTransform.HasValue)
                {
                    var svAbs = sv.Bounds.TransformToAABB(svTransform.Value);
                    if (!svAbs.Intersects(absoluteBounds)) return false;
                }
            }
            current = current.GetVisualParent();
        }
        return true;
    }

    private sealed class SnapshotEntry
    {
        [JsonPropertyName("nodeId")] public int NodeId { get; init; }
        [JsonPropertyName("role")] public required string Role { get; init; }
        [JsonPropertyName("text")] public string? Text { get; init; }
        [JsonPropertyName("value")] public string? Value { get; init; }
        [JsonPropertyName("x")] public double X { get; init; }
        [JsonPropertyName("y")] public double Y { get; init; }
        [JsonPropertyName("w")] public double W { get; init; }
        [JsonPropertyName("h")] public double H { get; init; }
        [JsonPropertyName("isEnabled")] public bool? IsEnabled { get; init; }
        [JsonPropertyName("isFocused")] public bool? IsFocused { get; init; }
        [JsonPropertyName("name")] public string? Name { get; init; }
        [JsonPropertyName("automationId")] public string? AutomationId { get; init; }
    }
}
