using System.Text.Json;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using AvaloniaMcp.Protocol;
using AvaloniaMcp.Protocol.Messages;

namespace AvaloniaMcp.AppHost.Handlers;

public sealed class InputHandler : IRequestHandler
{
    public string Method => ProtocolMethods.Click;

    public async Task<object> Handle(DiagnosticRequest request)
    {
        int nodeId = 0;
        if (request.Params is JsonElement p && p.TryGetProperty("nodeId", out var nid))
            nodeId = nid.GetInt32();

        return await Dispatcher.UIThread.InvokeAsync<object>(() =>
        {
            var (visual, resolveError) = NodeRegistry.ResolveChecked(nodeId);
            if (visual is null) return new { error = resolveError };

            // ToggleButton, CheckBox, RadioButton — toggle IsChecked directly
            if (visual is ToggleButton toggle)
            {
                toggle.IsChecked = visual is RadioButton ? true : toggle.IsChecked != true;
                return new { success = true, method = "toggle", isChecked = toggle.IsChecked };
            }

            // Button — invoke command or raise click event
            if (visual is Button button)
            {
                if (button.Command is { } cmd && cmd.CanExecute(button.CommandParameter))
                {
                    cmd.Execute(button.CommandParameter);
                    return new { success = true, method = "command" };
                }

                button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                return new { success = true, method = "click_event" };
            }

            // MenuItem — invoke command or raise click event
            if (visual is MenuItem menuItem)
            {
                if (menuItem.Command is { } miCmd && miCmd.CanExecute(menuItem.CommandParameter))
                {
                    miCmd.Execute(menuItem.CommandParameter);
                    return new { success = true, method = "menu_command" };
                }

                menuItem.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
                return new { success = true, method = "menu_click" };
            }

            if (visual is Control control)
            {
                // ListBoxItem — select in parent ListBox
                if (control is ListBoxItem lbi)
                {
                    var lb = lbi.GetVisualAncestors().OfType<ListBox>().FirstOrDefault();
                    if (lb is not null)
                    {
                        var idx = lb.IndexFromContainer(lbi);
                        if (idx >= 0) lb.SelectedIndex = idx;
                        return new { success = true, method = "listbox_select", selectedIndex = lb.SelectedIndex };
                    }
                }

                // TabItem — select in parent TabControl
                if (control is TabItem ti)
                {
                    var tc = ti.GetVisualAncestors().OfType<TabControl>().FirstOrDefault();
                    if (tc is not null)
                    {
                        var idx = tc.IndexFromContainer(ti);
                        if (idx >= 0) tc.SelectedIndex = idx;
                        return new { success = true, method = "tab_select", selectedIndex = tc.SelectedIndex };
                    }
                }

                // Generic item in a SelectingItemsControl (handles ComboBox items etc.)
                var selector = control.GetVisualAncestors().OfType<SelectingItemsControl>().FirstOrDefault();
                if (selector is not null)
                {
                    var idx = selector.IndexFromContainer(control);
                    if (idx >= 0)
                    {
                        selector.SelectedIndex = idx;
                        return new { success = true, method = "item_select", selectedIndex = idx };
                    }
                }

                // Fallback — focus and simulate pointer press/release
                if (control.Focusable) control.Focus();
                var center = new Point(control.Bounds.Width / 2, control.Bounds.Height / 2);
                RaisePointerEvent(control, InputElement.PointerPressedEvent, center);
                RaisePointerEvent(control, InputElement.PointerReleasedEvent, center);
                return new { success = true, method = "pointer_simulation" };
            }

            return new { error = "Cannot click this element" };
        });
    }

    private static void RaisePointerEvent(Control control, RoutedEvent routedEvent, Point position)
    {
        control.RaiseEvent(new RoutedEventArgs(routedEvent));
    }
}

public sealed class KeyboardHandler : IRequestHandler
{
    public string Method => ProtocolMethods.KeyDown;

    public async Task<object> Handle(DiagnosticRequest request)
    {
        int nodeId = 0;
        string? key = null;
        string? text = null;
        string? modifiers = null;

        if (request.Params is JsonElement p)
        {
            if (p.TryGetProperty("nodeId", out var nid)) nodeId = nid.GetInt32();
            if (p.TryGetProperty("key", out var k)) key = k.GetString();
            if (p.TryGetProperty("text", out var t)) text = t.GetString();
            if (p.TryGetProperty("modifiers", out var m)) modifiers = m.GetString();
        }

        return await Dispatcher.UIThread.InvokeAsync<object>(() =>
        {
            var (visual, resolveError) = NodeRegistry.ResolveChecked(nodeId);
            if (visual is not InputElement element) return new { error = resolveError ?? $"Node {nodeId} is not an InputElement" };

            if (text is not null)
            {
                if (element is TextBox textBox)
                {
                    textBox.Text = text;
                    return new { success = true, action = "text_set", value = text };
                }
                return new { error = "Text input only supported on TextBox" };
            }

            if (key is not null && Enum.TryParse<Key>(key, true, out var keyEnum))
            {
                var keyArgs = new KeyEventArgs
                {
                    RoutedEvent = InputElement.KeyDownEvent,
                    Key = keyEnum,
                    KeyModifiers = KeyModifierParser.Parse(modifiers)
                };
                element.RaiseEvent(keyArgs);
                return new { success = true, action = "key_down", key, modifiers };
            }

            return new { error = "No key or text provided" };
        });
    }
}

public sealed class KeyUpHandler : IRequestHandler
{
    public string Method => ProtocolMethods.KeyUp;

    public async Task<object> Handle(DiagnosticRequest request)
    {
        int nodeId = 0;
        string? key = null;
        string? modifiers = null;

        if (request.Params is JsonElement p)
        {
            if (p.TryGetProperty("nodeId", out var nid)) nodeId = nid.GetInt32();
            if (p.TryGetProperty("key", out var k)) key = k.GetString();
            if (p.TryGetProperty("modifiers", out var m)) modifiers = m.GetString();
        }

        return await Dispatcher.UIThread.InvokeAsync<object>(() =>
        {
            var (visual, resolveError) = NodeRegistry.ResolveChecked(nodeId);
            if (visual is not InputElement element) return new { error = resolveError ?? $"Node {nodeId} is not an InputElement" };

            if (key is not null && Enum.TryParse<Key>(key, true, out var keyEnum))
            {
                var keyArgs = new KeyEventArgs
                {
                    RoutedEvent = InputElement.KeyUpEvent,
                    Key = keyEnum,
                    KeyModifiers = KeyModifierParser.Parse(modifiers)
                };
                element.RaiseEvent(keyArgs);
                return new { success = true, action = "key_up", key, modifiers };
            }

            return new { error = "No key provided" };
        });
    }
}

public sealed class TextInputHandler : IRequestHandler
{
    public string Method => ProtocolMethods.TextInput;

    public async Task<object> Handle(DiagnosticRequest request)
    {
        int nodeId = 0;
        string? text = null;
        var pressEnter = false;

        if (request.Params is JsonElement p)
        {
            if (p.TryGetProperty("nodeId", out var nid)) nodeId = nid.GetInt32();
            if (p.TryGetProperty("text", out var t)) text = t.GetString();
            if (p.TryGetProperty("pressEnter", out var pe)) pressEnter = pe.GetBoolean();
        }

        return await Dispatcher.UIThread.InvokeAsync<object>(() =>
        {
            var (visual, resolveError) = NodeRegistry.ResolveChecked(nodeId);
            if (visual is null) return new { error = resolveError };

            if (visual is TextBox textBox)
            {
                if (text is not null) textBox.Text = text;
                if (pressEnter)
                {
                    textBox.RaiseEvent(new KeyEventArgs
                    {
                        RoutedEvent = InputElement.KeyDownEvent,
                        Key = Key.Enter
                    });
                }
                return new { success = true, text = textBox.Text };
            }

            // Try finding a child TextBox
            if (visual is Control control)
            {
                var tb = control.GetVisualDescendants().OfType<TextBox>().FirstOrDefault();
                if (tb is not null)
                {
                    if (text is not null) tb.Text = text;
                    return new { success = true, text = tb.Text };
                }
            }

            return new { error = "No TextBox found" };
        });
    }
}

internal static class KeyModifierParser
{
    public static KeyModifiers Parse(string? modifiers)
    {
        if (string.IsNullOrWhiteSpace(modifiers)) return KeyModifiers.None;

        var result = KeyModifiers.None;
        foreach (var part in modifiers.Split('+', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
        {
            result |= part.ToLowerInvariant() switch
            {
                "ctrl" or "control" => KeyModifiers.Control,
                "shift" => KeyModifiers.Shift,
                "alt" => KeyModifiers.Alt,
                "meta" or "win" or "cmd" => KeyModifiers.Meta,
                _ => KeyModifiers.None
            };
        }

        return result;
    }
}
