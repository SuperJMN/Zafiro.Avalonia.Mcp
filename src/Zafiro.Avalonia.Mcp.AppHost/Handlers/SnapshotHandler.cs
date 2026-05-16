using System.Text.Json;
using System.Text.Json.Serialization;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Presenters;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Zafiro.Avalonia.Mcp.AppHost.Selectors;
using Zafiro.Avalonia.Mcp.Protocol;
using Zafiro.Avalonia.Mcp.Protocol.Messages;

namespace Zafiro.Avalonia.Mcp.AppHost.Handlers;

/// <summary>
/// Returns a compact spatial snapshot of the UI: semantic regions, visible text, and
/// interactive controls in a single flattened hierarchy.
/// Each entry includes nodeId, role, text, current value, nesting, and absolute position/size.
/// This replaces the common pattern of calling get_screen_text + get_interactables separately.
/// </summary>
public sealed class SnapshotHandler : IRequestHandler
{
    public string Method => ProtocolMethods.GetSnapshot;

    public async Task<object> Handle(DiagnosticRequest request)
    {
        string? selector = null;
        var visibleOnly = true;
        var detail = "smart";

        if (request.Params is JsonElement p)
        {
            if (p.TryGetProperty("selector", out var s)) selector = s.GetString();
            if (p.TryGetProperty("visibleOnly", out var vo)) visibleOnly = vo.GetBoolean();
            if (p.TryGetProperty("detail", out var detailElement)) detail = detailElement.GetString() ?? "smart";
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
                var window = NodeRegistry.GetRoots().FirstOrDefault();
                if (window is null) return (object)new { error = "No windows found" };
                root = window;
            }

            return BuildSnapshot(root, visibleOnly, detail);
        });
    }

    internal static object BuildSnapshot(Visual root, bool visibleOnly, string detail = "smart")
    {
        if (!TryParseDetail(detail, out var parsedDetail))
        {
            return HandlerResult.InvalidParam("detail", "detail must be 'smart' or 'verbose'.");
        }

        return parsedDetail == SnapshotDetail.Verbose
            ? BuildVerboseSnapshot(root, visibleOnly)
            : BuildSmartSnapshot(root, visibleOnly);
    }

    private static bool TryParseDetail(string? detail, out SnapshotDetail parsed)
    {
        if (string.IsNullOrWhiteSpace(detail) || detail.Equals("smart", StringComparison.OrdinalIgnoreCase))
        {
            parsed = SnapshotDetail.Smart;
            return true;
        }

        if (detail.Equals("verbose", StringComparison.OrdinalIgnoreCase))
        {
            parsed = SnapshotDetail.Verbose;
            return true;
        }

        parsed = SnapshotDetail.Smart;
        return false;
    }

    private static object BuildSmartSnapshot(Visual root, bool visibleOnly)
    {
        var rootVisual = FindRoot(root);
        var windowBounds = new Rect(0, 0, rootVisual.Bounds.Width, rootVisual.Bounds.Height);

        var entries = new List<SnapshotEntry>();
        CollectSmart(root, rootVisual, entries, visibleOnly, windowBounds, parentEntry: null, level: 0);
        var deduped = Deduplicate(entries);

        return BuildResult(root, rootVisual, deduped, "smart");
    }

    private static object BuildVerboseSnapshot(Visual root, bool visibleOnly)
    {
        var rootVisual = FindRoot(root);
        var windowBounds = new Rect(0, 0, rootVisual.Bounds.Width, rootVisual.Bounds.Height);

        var entries = new List<SnapshotEntry>();
        CollectVerbose(root, rootVisual, entries, visibleOnly, windowBounds, parentEntry: null, level: 0);
        var deduped = DeduplicateVerbose(entries);

        return BuildResult(root, rootVisual, deduped, "verbose");
    }

    private static object BuildResult(Visual root, Visual rootVisual, List<SnapshotEntry> elements, string detail)
    {
        var focused = elements.FirstOrDefault(e => e.IsFocused == true);
        var windowTitle = (root as Window)?.Title ?? (rootVisual as Window)?.Title;
        var windowSize = $"{rootVisual.Bounds.Width}x{rootVisual.Bounds.Height}";

        return new
        {
            window = windowTitle is not null ? $"{windowTitle} ({windowSize})" : windowSize,
            detail,
            focusedId = focused?.NodeId,
            elements
        };
    }

    private static Visual FindRoot(Visual visual)
    {
        var current = visual;
        while (current.GetVisualParent() is Visual parent)
            current = parent;
        return current;
    }

    private static void CollectSmart(Visual visual, Visual rootVisual, List<SnapshotEntry> entries,
        bool visibleOnly, Rect windowBounds, SnapshotEntry? parentEntry, int level)
    {
        if (!visual.IsVisible) return;

        var entry = TryBuild(visual, rootVisual, parentEntry, level);
        var nextParent = parentEntry;
        var nextLevel = level;

        if (entry is not null)
        {
            if (!visibleOnly || IsInViewport(visual, new Rect(entry.X, entry.Y, entry.W, entry.H), rootVisual, windowBounds))
            {
                entries.Add(entry);
                nextParent = entry;
                nextLevel = level + 1;
            }
        }

        foreach (var child in visual.GetVisualChildren())
            CollectSmart(child, rootVisual, entries, visibleOnly, windowBounds, nextParent, nextLevel);
    }

    private static void CollectVerbose(Visual visual, Visual rootVisual, List<SnapshotEntry> entries,
        bool visibleOnly, Rect windowBounds, SnapshotEntry? parentEntry, int level)
    {
        if (!visual.IsVisible) return;

        var entry = TryBuildVerbose(visual, rootVisual, parentEntry, level);
        var nextParent = parentEntry;
        var nextLevel = level;

        if (entry is not null)
        {
            if (!visibleOnly || IsInViewport(visual, new Rect(entry.X, entry.Y, entry.W, entry.H), rootVisual, windowBounds))
            {
                entries.Add(entry);
                nextParent = entry;
                nextLevel = level + 1;
            }
        }

        foreach (var child in visual.GetVisualChildren())
            CollectVerbose(child, rootVisual, entries, visibleOnly, windowBounds, nextParent, nextLevel);
    }

    private static SnapshotEntry? TryBuild(Visual visual, Visual rootVisual, SnapshotEntry? parentEntry, int level)
    {
        var descriptor = Describe(visual, parentEntry);
        if (descriptor is null)
            return null;

        var value = GetValue(visual);

        var abs = GetAbsoluteBounds(visual, rootVisual);
        if (!abs.HasValue) return null;

        var bounds = abs.Value;
        var inputEl = visual as InputElement;

        return new SnapshotEntry
        {
            NodeId = NodeRegistry.GetOrRegister(visual),
            Type = visual.GetType().Name,
            Role = descriptor.Role,
            Text = descriptor.Text,
            Value = value,
            X = Math.Round(bounds.X, 1),
            Y = Math.Round(bounds.Y, 1),
            W = Math.Round(bounds.Width, 1),
            H = Math.Round(bounds.Height, 1),
            Level = level,
            ParentId = parentEntry?.NodeId,
            IsEnabled = inputEl?.IsEnabled,
            IsFocused = inputEl?.IsFocused == true ? true : null,
            Name = (visual as Control)?.Name,
            AutomationId = GetAutomationId(visual),
        };
    }

    private static SnapshotEntry? TryBuildVerbose(Visual visual, Visual rootVisual, SnapshotEntry? parentEntry, int level)
    {
        var role = GetRole(visual);
        var text = GetVerboseText(visual);
        var value = GetValue(visual);
        var isInteractive = IsInteractiveControl(visual);

        if (!isInteractive && string.IsNullOrWhiteSpace(text))
            return null;

        var abs = GetAbsoluteBounds(visual, rootVisual);
        if (!abs.HasValue) return null;

        var bounds = abs.Value;
        var inputEl = visual as InputElement;

        return new SnapshotEntry
        {
            NodeId = NodeRegistry.GetOrRegister(visual),
            Type = visual.GetType().Name,
            Role = role ?? (isInteractive ? "interactive" : "text"),
            Text = text,
            Value = value,
            X = Math.Round(bounds.X, 1),
            Y = Math.Round(bounds.Y, 1),
            W = Math.Round(bounds.Width, 1),
            H = Math.Round(bounds.Height, 1),
            Level = level,
            ParentId = parentEntry?.NodeId,
            IsEnabled = inputEl?.IsEnabled,
            IsFocused = inputEl?.IsFocused == true ? true : null,
            Name = (visual as Control)?.Name,
            AutomationId = GetAutomationId(visual),
        };
    }

    private static SnapshotDescriptor? Describe(Visual visual, SnapshotEntry? parentEntry)
    {
        if (visual is TopLevel)
            return null;

        if (IsInteractiveControl(visual))
        {
            var role = GetRole(visual) ?? "interactive";
            return new SnapshotDescriptor(role, GetMeaningfulText(visual));
        }

        var ownText = GetOwnText(visual);
        if (!string.IsNullOrWhiteSpace(ownText))
        {
            if (IsCoveredByParent(ownText, parentEntry))
                return null;

            return new SnapshotDescriptor(GetRole(visual) ?? "text", ownText);
        }

        var structuralLabel = GetStructuralLabel(visual);
        if (!string.IsNullOrWhiteSpace(structuralLabel))
        {
            if (IsCoveredByParent(structuralLabel, parentEntry))
                return null;

            return new SnapshotDescriptor(GetRole(visual) ?? "group", structuralLabel);
        }

        var automationRole = GetAutomationRole(visual);
        if (automationRole is not null)
        {
            return new SnapshotDescriptor(automationRole, null);
        }

        return null;
    }

    private static bool IsInteractiveControl(Visual visual)
    {
        if (visual is InputElement { Focusable: true } input && input.IsEnabled)
            return true;
        return visual is Button
            or MenuItem
            or ListBoxItem
            or TabItem
            or ComboBoxItem
            or TreeViewItem
            or Expander;
    }

    private static string? GetRole(Visual visual) => visual switch
    {
        _ when GetAutomationRole(visual) is { } automationRole => automationRole,
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
        Label => "label",
        TextBlock => "text",
        _ => null
    };

    private static string? GetAutomationRole(Visual visual)
    {
        if (visual is not Control control)
            return null;

        try
        {
            var peer = ControlAutomationPeer.CreatePeerForElement(control);
            var role = MapAutomationControlType(peer.GetAutomationControlType().ToString());
            return string.IsNullOrWhiteSpace(role) ? null : role;
        }
        catch
        {
            return null;
        }
    }

    private static string? MapAutomationControlType(string controlType)
    {
        return controlType switch
        {
            "None" => null,
            "Custom" => null,
            "Button" => "button",
            "Calendar" => "datepicker",
            "CheckBox" => "checkbox",
            "ComboBox" => "combobox",
            "Edit" => "textbox",
            "ListItem" => "listitem",
            "MenuItem" => "menuitem",
            "ProgressBar" => "progressbar",
            "RadioButton" => "radio",
            "TabItem" => "tab",
            "Text" => "text",
            "TreeItem" => "treeitem",
            _ => controlType.ToLowerInvariant()
        };
    }

    private static string? GetMeaningfulText(Visual visual) => visual switch
    {
        TextBox tb => string.IsNullOrWhiteSpace(tb.Text) ? tb.PlaceholderText : tb.Text,
        TextBlock tb => NormalizeText(tb.Text),
        HeaderedContentControl hcc => GetObjectText(hcc.Header)
                                      ?? GetContentString(hcc)
                                      ?? GetAutomationName(visual)
                                      ?? GetTextFromDescendants(hcc),
        ContentControl cc => GetContentString(cc)
                             ?? GetAutomationName(visual)
                             ?? GetTextFromDescendants(cc),
        _ => GetAutomationName(visual) ?? GetTextFromDescendants(visual)
    };

    private static string? GetVerboseText(Visual visual) => visual switch
    {
        TextBox tb => tb.PlaceholderText ?? tb.Text,
        TextBlock tb => NormalizeText(tb.Text),
        HeaderedContentControl hcc => GetObjectText(hcc.Header) ?? GetContentString(hcc),
        ContentControl cc => GetContentString(cc) ?? GetTextFromDescendants(cc),
        ContentPresenter cp => GetObjectText(cp.Content) ?? GetTextFromDescendants(cp),
        _ => GetAutomationName(visual) ?? GetTextFromDescendants(visual)
    };

    private static string? GetOwnText(Visual visual) => visual switch
    {
        TextBlock tb => NormalizeText(tb.Text),
        TextBox tb => NormalizeText(tb.Text),
        ContentPresenter cp => GetObjectText(cp.Content),
        ContentControl cc when cc is not HeaderedContentControl => GetContentString(cc),
        _ => null
    };

    private static string? GetStructuralLabel(Visual visual)
    {
        var label = visual switch
        {
            HeaderedContentControl hcc => GetObjectText(hcc.Header),
            _ => null
        };

        return label ?? GetAutomationName(visual);
    }

    private static string? GetContentString(ContentControl cc) => GetObjectText(cc.Content);

    private static string? GetObjectText(object? value) => value switch
    {
        string s => NormalizeText(s),
        TextBlock tb => NormalizeText(tb.Text),
        _ => null
    };

    private static string? NormalizeText(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return null;

        return text.Trim();
    }

    private static string? GetAutomationName(Visual visual)
    {
        if (visual is Control ctrl)
        {
            var name = AutomationProperties.GetName(ctrl);
            if (!string.IsNullOrEmpty(name)) return name;
        }
        return null;
    }

    private static string? GetTextFromDescendants(Visual visual)
    {
        var texts = visual.GetVisualDescendants()
            .Where(v => v.IsVisible)
            .Select(GetOwnText)
            .Where(text => !string.IsNullOrWhiteSpace(text))
            .Distinct(StringComparer.Ordinal)
            .Take(3);
        var joined = string.Join(" · ", texts);
        return string.IsNullOrEmpty(joined) ? null : joined;
    }

    private static bool IsCoveredByParent(string text, SnapshotEntry? parentEntry)
    {
        var parentText = parentEntry?.Text;
        if (string.IsNullOrWhiteSpace(parentText))
            return false;

        return string.Equals(parentText, text, StringComparison.Ordinal)
               || parentText.Split(" · ", StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                   .Contains(text, StringComparer.Ordinal);
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

    private static Rect? GetAbsoluteBounds(Visual visual, Visual rootVisual)
    {
        var transform = visual.TransformToVisual(rootVisual);
        if (!transform.HasValue) return null;

        var localBounds = new Rect(0, 0, visual.Bounds.Width, visual.Bounds.Height);
        return localBounds.TransformToAABB(transform.Value);
    }

    private static bool IsInViewport(Visual visual, Rect absoluteBounds, Visual rootVisual, Rect windowBounds)
    {
        if (!windowBounds.Intersects(absoluteBounds)) return false;

        var current = visual.GetVisualParent();
        while (current is not null)
        {
            if (current is ScrollViewer sv)
            {
                var svAbs = GetAbsoluteBounds(sv, rootVisual);
                if (svAbs.HasValue)
                {
                    if (!svAbs.Value.Intersects(absoluteBounds)) return false;
                }
            }
            current = current.GetVisualParent();
        }
        return true;
    }

    private static List<SnapshotEntry> Deduplicate(IEnumerable<SnapshotEntry> entries)
    {
        var deduped = new List<SnapshotEntry>();

        foreach (var entry in entries)
        {
            var duplicateIndex = deduped.FindIndex(existing => IsDuplicate(existing, entry));
            if (duplicateIndex < 0)
            {
                deduped.Add(entry);
                continue;
            }

            if (EntryPriority(entry) > EntryPriority(deduped[duplicateIndex]))
                deduped[duplicateIndex] = entry;
        }

        return deduped;
    }

    private static List<SnapshotEntry> DeduplicateVerbose(IEnumerable<SnapshotEntry> entries)
    {
        var deduped = new List<SnapshotEntry>();

        foreach (var entry in entries)
        {
            var isDuplicate = deduped.Any(existing =>
                existing.Text == entry.Text
                && existing.Role == entry.Role
                && Math.Abs(existing.X - entry.X) < 20
                && Math.Abs(existing.Y - entry.Y) < 20);

            if (!isDuplicate)
                deduped.Add(entry);
        }

        return deduped;
    }

    private static bool IsDuplicate(SnapshotEntry existing, SnapshotEntry entry)
    {
        if (string.IsNullOrWhiteSpace(existing.Text) || existing.Text != entry.Text)
            return false;

        return Math.Abs(existing.X - entry.X) < 20
               && Math.Abs(existing.Y - entry.Y) < 20;
    }

    private static int EntryPriority(SnapshotEntry entry)
    {
        if (entry.Role == "group") return 2;
        if (entry.Role != "text" && entry.Role != "label") return 3;
        return 1;
    }

    private sealed record SnapshotDescriptor(string Role, string? Text);

    private enum SnapshotDetail
    {
        Smart,
        Verbose
    }

    private sealed class SnapshotEntry
    {
        [JsonPropertyName("nodeId")] public int NodeId { get; init; }
        [JsonPropertyName("type")] public required string Type { get; init; }
        [JsonPropertyName("role")] public required string Role { get; init; }
        [JsonPropertyName("text")] public string? Text { get; init; }
        [JsonPropertyName("value")] public string? Value { get; init; }
        [JsonPropertyName("x")] public double X { get; init; }
        [JsonPropertyName("y")] public double Y { get; init; }
        [JsonPropertyName("w")] public double W { get; init; }
        [JsonPropertyName("h")] public double H { get; init; }
        [JsonPropertyName("level")] public int Level { get; init; }
        [JsonPropertyName("parentId")] public int? ParentId { get; init; }
        [JsonPropertyName("isEnabled")] public bool? IsEnabled { get; init; }
        [JsonPropertyName("isFocused")] public bool? IsFocused { get; init; }
        [JsonPropertyName("name")] public string? Name { get; init; }
        [JsonPropertyName("automationId")] public string? AutomationId { get; init; }
    }
}
