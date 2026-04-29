using System.Text.Json;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Zafiro.Avalonia.Mcp.AppHost.Selectors;
using Zafiro.Avalonia.Mcp.Protocol;
using Zafiro.Avalonia.Mcp.Protocol.Messages;

namespace Zafiro.Avalonia.Mcp.AppHost.Handlers;

public sealed class InputHandler : IRequestHandler
{
    public string Method => ProtocolMethods.Click;

    public async Task<object> Handle(DiagnosticRequest request)
    {
        string? selector = null;
        if (request.Params is JsonElement p && p.TryGetProperty("selector", out var s))
            selector = s.GetString();

        return await Dispatcher.UIThread.InvokeAsync<object>(() =>
        {
            var (visual, error) = SelectorRequestHelper.ResolveSingle(selector);
            if (visual is null) return error!;
            return Click(visual);
        });
    }

    /// <summary>
    /// Performs the appropriate "click" semantic on the given visual.
    /// Exposed for tests so they don't need to spin a Dispatcher.
    /// </summary>
    internal static object Click(Visual visual)
    {
        var nodeId = NodeRegistry.GetOrRegister(visual);

        if (visual is ToggleButton toggle)
        {
            toggle.IsChecked = visual is RadioButton ? true : toggle.IsChecked != true;
            return new { success = true, nodeId, method = "toggle", isChecked = toggle.IsChecked };
        }

        if (visual is Button button)
        {
            if (button.Command is { } cmd && cmd.CanExecute(button.CommandParameter))
            {
                cmd.Execute(button.CommandParameter);
                return new { success = true, nodeId, method = "command" };
            }

            button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            return new { success = true, nodeId, method = "click_event" };
        }

        if (visual is MenuItem menuItem)
        {
            if (menuItem.Command is { } miCmd && miCmd.CanExecute(menuItem.CommandParameter))
            {
                miCmd.Execute(menuItem.CommandParameter);
                return new { success = true, nodeId, method = "menu_command" };
            }

            menuItem.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
            return new { success = true, nodeId, method = "menu_click" };
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
                    return new { success = true, nodeId, method = "listbox_select", selectedIndex = lb.SelectedIndex };
                }
            }

            if (control is TabItem ti)
            {
                var tc = ti.GetVisualAncestors().OfType<TabControl>().FirstOrDefault();
                if (tc is not null)
                {
                    var idx = tc.IndexFromContainer(ti);
                    if (idx >= 0) tc.SelectedIndex = idx;
                    return new { success = true, nodeId, method = "tab_select", selectedIndex = tc.SelectedIndex };
                }
            }

            var itemsHost = control.GetVisualAncestors().OfType<SelectingItemsControl>().FirstOrDefault();
            if (itemsHost is not null)
            {
                var idx = itemsHost.IndexFromContainer(control);
                if (idx >= 0)
                {
                    itemsHost.SelectedIndex = idx;
                    return new { success = true, nodeId, method = "item_select", selectedIndex = idx };
                }
            }

            if (control.Focusable) control.Focus();
            var center = new Point(control.Bounds.Width / 2, control.Bounds.Height / 2);
            RaisePointerEvent(control, InputElement.PointerPressedEvent, center);
            RaisePointerEvent(control, InputElement.PointerReleasedEvent, center);
            return new { success = true, nodeId, method = "pointer_simulation" };
        }

        return new { error = "Cannot click this element", nodeId };
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
        string? selector = null;
        string? key = null;
        string? text = null;
        string? modifiers = null;

        if (request.Params is JsonElement p)
        {
            if (p.TryGetProperty("selector", out var s)) selector = s.GetString();
            if (p.TryGetProperty("key", out var k)) key = k.GetString();
            if (p.TryGetProperty("text", out var t)) text = t.GetString();
            if (p.TryGetProperty("modifiers", out var m)) modifiers = m.GetString();
        }

        return await Dispatcher.UIThread.InvokeAsync<object>(() =>
        {
            var (visual, error) = SelectorRequestHelper.ResolveSingle(selector);
            if (visual is null) return error!;
            return KeyDown(visual, key, text, modifiers);
        });
    }

    internal static object KeyDown(Visual visual, string? key, string? text, string? modifiers)
    {
        var nodeId = NodeRegistry.GetOrRegister(visual);
        if (visual is not InputElement element)
            return new { error = "selector did not resolve to an InputElement", nodeId };

        if (text is not null)
        {
            if (element is TextBox textBox)
            {
                textBox.Text = text;
                return new { success = true, nodeId, action = "text_set", value = text };
            }
            return new { error = "Text input only supported on TextBox", nodeId };
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
            return new { success = true, nodeId, action = "key_down", key, modifiers };
        }

        return new { error = "No key or text provided", nodeId };
    }
}

public sealed class KeyUpHandler : IRequestHandler
{
    public string Method => ProtocolMethods.KeyUp;

    public async Task<object> Handle(DiagnosticRequest request)
    {
        string? selector = null;
        string? key = null;
        string? modifiers = null;

        if (request.Params is JsonElement p)
        {
            if (p.TryGetProperty("selector", out var s)) selector = s.GetString();
            if (p.TryGetProperty("key", out var k)) key = k.GetString();
            if (p.TryGetProperty("modifiers", out var m)) modifiers = m.GetString();
        }

        return await Dispatcher.UIThread.InvokeAsync<object>(() =>
        {
            var (visual, error) = SelectorRequestHelper.ResolveSingle(selector);
            if (visual is null) return error!;
            return KeyUp(visual, key, modifiers);
        });
    }

    internal static object KeyUp(Visual visual, string? key, string? modifiers)
    {
        var nodeId = NodeRegistry.GetOrRegister(visual);
        if (visual is not InputElement element)
            return new { error = "selector did not resolve to an InputElement", nodeId };

        if (key is not null && Enum.TryParse<Key>(key, true, out var keyEnum))
        {
            var keyArgs = new KeyEventArgs
            {
                RoutedEvent = InputElement.KeyUpEvent,
                Key = keyEnum,
                KeyModifiers = KeyModifierParser.Parse(modifiers)
            };
            element.RaiseEvent(keyArgs);
            return new { success = true, nodeId, action = "key_up", key, modifiers };
        }

        return new { error = "No key provided", nodeId };
    }
}

public sealed class TextInputHandler : IRequestHandler
{
    public string Method => ProtocolMethods.TextInput;

    public async Task<object> Handle(DiagnosticRequest request)
    {
        string? selector = null;
        string? text = null;
        var pressEnter = false;

        if (request.Params is JsonElement p)
        {
            if (p.TryGetProperty("selector", out var s)) selector = s.GetString();
            if (p.TryGetProperty("text", out var t)) text = t.GetString();
            if (p.TryGetProperty("pressEnter", out var pe)) pressEnter = pe.GetBoolean();
        }

        return await Dispatcher.UIThread.InvokeAsync<object>(() =>
        {
            var (visual, error) = SelectorRequestHelper.ResolveSingle(selector);
            if (visual is null) return error!;
            return TextInput(visual, text, pressEnter);
        });
    }

    internal static object TextInput(Visual visual, string? text, bool pressEnter)
    {
        var nodeId = NodeRegistry.GetOrRegister(visual);

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
            return new { success = true, nodeId, text = textBox.Text };
        }

        if (visual is Control control)
        {
            var tb = control.GetVisualDescendants().OfType<TextBox>().FirstOrDefault();
            if (tb is not null)
            {
                if (text is not null) tb.Text = text;
                return new { success = true, nodeId, text = tb.Text };
            }
        }

        return new { error = "No TextBox found", nodeId };
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
