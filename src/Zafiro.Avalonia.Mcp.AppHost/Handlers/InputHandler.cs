using System.Text.Json;
using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Input.Raw;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
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
        if (visual is TextBlock)
        {
            var interactiveAncestor = visual.GetVisualAncestors().FirstOrDefault(IsSemanticClickTarget);
            if (interactiveAncestor is null)
            {
                var textNodeId = NodeRegistry.GetOrRegister(visual);
                return HandlerResult.Error(
                    DiagnosticErrorCodes.UnsupportedOperation,
                    "TextBlock has no interactive ancestor that can be clicked.",
                    "Target the owning Button, MenuItem, or selectable item.",
                    new { nodeId = textNodeId, elementType = visual.GetType().Name });
            }

            visual = interactiveAncestor;
        }

        var nodeId = NodeRegistry.GetOrRegister(visual);
        ICommand? executableCommand = null;
        object? commandParameter = null;

        if (visual is InputElement { IsEffectivelyEnabled: false })
        {
            return DisabledClickError(visual, nodeId);
        }

        if (visual is Button { Command: { } buttonCommand } commandButton)
        {
            commandParameter = commandButton.CommandParameter;
            if (!buttonCommand.CanExecute(commandParameter))
                return DisabledClickError(visual, nodeId, " Its command cannot execute.");
            executableCommand = buttonCommand;
        }
        if (visual is ToggleButton toggle)
        {
            toggle.IsChecked = visual is RadioButton ? true : toggle.IsChecked != true;
            return new { success = true, nodeId, method = "toggle", isChecked = toggle.IsChecked };
        }

        if (visual is Button button)
        {
            if (executableCommand is not null)
            {
                executableCommand.Execute(commandParameter);
                return new { success = true, nodeId, method = "command" };
            }

            if (button.Flyout is PopupFlyoutBase flyout)
            {
                flyout.ShowAt(button);
                return new { success = true, nodeId, method = "flyout" };
            }

            button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            return new { success = true, nodeId, method = "click_event" };
        }

        if (visual is MenuItem menuItem)
        {
            var hasCommand = menuItem.Command is not null;
            var commandExecuted = ActivateMenuItem(menuItem);
            if (hasCommand && !commandExecuted)
                return DisabledClickError(visual, nodeId, " Its command did not execute.");
            return new { success = true, nodeId, method = commandExecuted ? "menu_command" : "menu_click" };
        }

        if (visual is Control control)
        {
            if (control is ListBoxItem lbi)
            {
                var lb = lbi.GetVisualAncestors().OfType<ListBox>().FirstOrDefault()
                    ?? lbi.GetLogicalAncestors().OfType<ListBox>().FirstOrDefault();
                if (lb is not null)
                {
                    var idx = lb.IndexFromContainer(lbi);
                    if (idx < 0) return InvalidSelectionContainerError(lbi, lb, nodeId);
                    lb.SelectedIndex = idx;
                    return new { success = true, nodeId, method = "listbox_select", selectedIndex = lb.SelectedIndex };
                }
            }

            if (control is TabItem ti)
            {
                var tc = ti.GetVisualAncestors().OfType<TabControl>().FirstOrDefault()
                    ?? ti.GetLogicalAncestors().OfType<TabControl>().FirstOrDefault();
                if (tc is not null)
                {
                    var idx = tc.IndexFromContainer(ti);
                    if (idx < 0) return InvalidSelectionContainerError(ti, tc, nodeId);
                    tc.SelectedIndex = idx;
                    return new { success = true, nodeId, method = "tab_select", selectedIndex = tc.SelectedIndex };
                }
            }

            if (control is TreeViewItem treeViewItem)
            {
                var treeView = treeViewItem.GetVisualAncestors().OfType<TreeView>().FirstOrDefault()
                    ?? treeViewItem.GetLogicalAncestors().OfType<TreeView>().FirstOrDefault();

                if (treeView is not null)
                {
                    var selectedItem = treeViewItem.DataContext ?? treeViewItem.Header ?? treeViewItem;
                    treeView.SelectedItem = selectedItem;
                    treeViewItem.IsSelected = true;

                    return new
                    {
                        success = true,
                        nodeId,
                        method = "treeview_select",
                        isSelected = treeViewItem.IsSelected,
                        selectedItem = treeView.SelectedItem?.ToString()
                    };
                }

                treeViewItem.IsSelected = true;
                return new { success = true, nodeId, method = "treeview_select", isSelected = treeViewItem.IsSelected };
            }

            var itemsHost = control.GetVisualAncestors().OfType<SelectingItemsControl>().FirstOrDefault()
                ?? control.GetLogicalAncestors().OfType<SelectingItemsControl>().FirstOrDefault();
            if (itemsHost is not null)
            {
                var idx = itemsHost.IndexFromContainer(control);
                if (idx < 0) return InvalidSelectionContainerError(control, itemsHost, nodeId);
                itemsHost.SelectedIndex = idx;
                return new { success = true, nodeId, method = "item_select", selectedIndex = itemsHost.SelectedIndex };
            }

            if (control.Focusable) control.Focus();
            var center = new Point(control.Bounds.Width / 2, control.Bounds.Height / 2);
            SimulatePointerClick(control, center);
            return HandlerResult.Error(
                DiagnosticErrorCodes.UnsupportedOperation,
                $"Pointer events were emitted for '{control.GetType().Name}', but the click result could not be verified.",
                "Target a semantic interactive control or verify the resulting UI state explicitly.",
                new { nodeId, elementType = control.GetType().Name, method = "pointer_simulation" });
        }

        return HandlerResult.Error(
            DiagnosticErrorCodes.UnsupportedOperation,
            $"Cannot click element type '{visual.GetType().Name}'.",
            null,
            new { nodeId, elementType = visual.GetType().Name });
    }

    private static bool IsSemanticClickTarget(Visual visual) => visual is Button
        or MenuItem
        or ListBoxItem
        or TabItem
        or ComboBoxItem
        or TreeViewItem;

    private static HandlerErrorResult DisabledClickError(Visual visual, int nodeId, string extra = "") =>
        HandlerResult.Error(
            DiagnosticErrorCodes.UnsupportedOperation,
            $"Cannot click disabled element '{visual.GetType().Name}'.{extra}",
            "Enable the element (including its command and ancestors) before clicking it.",
            new { nodeId, elementType = visual.GetType().Name });

    private static HandlerErrorResult InvalidSelectionContainerError(Control control, SelectingItemsControl owner, int nodeId) =>
        HandlerResult.Error(
            DiagnosticErrorCodes.UnsupportedOperation,
            $"Element '{control.GetType().Name}' is not a generated container of '{owner.GetType().Name}'.",
            "Target an item container returned by get_snapshot or get_tree.",
            new { nodeId, elementType = control.GetType().Name, ownerType = owner.GetType().Name });

    private static void SimulatePointerClick(Control control, Point position)
    {
        var pointer = new Pointer(Pointer.GetNextFreeId(), PointerType.Mouse, isPrimary: true);
        var rootVisual = FindRootVisual(control);
        var rootPosition = control.TransformToVisual(rootVisual)?.Transform(position) ?? position;
        var timestamp = unchecked((ulong)Environment.TickCount64);

        control.RaiseEvent(new PointerPressedEventArgs(
            control,
            pointer,
            rootVisual,
            rootPosition,
            timestamp,
            new PointerPointProperties(RawInputModifiers.LeftMouseButton, PointerUpdateKind.LeftButtonPressed),
            KeyModifiers.None));

        control.RaiseEvent(new PointerReleasedEventArgs(
            control,
            pointer,
            rootVisual,
            rootPosition,
            timestamp + 1,
            new PointerPointProperties(RawInputModifiers.None, PointerUpdateKind.LeftButtonReleased),
            KeyModifiers.None,
            MouseButton.Left));
    }

    private static bool ActivateMenuItem(MenuItem menuItem)
    {
        if (!menuItem.HasSubMenu)
        {
            if (menuItem.ToggleType == MenuItemToggleType.CheckBox)
            {
                menuItem.IsChecked = !menuItem.IsChecked;
            }
            else if (menuItem.ToggleType == MenuItemToggleType.Radio && !menuItem.IsChecked)
            {
                menuItem.IsChecked = true;
            }
        }

        var clickArgs = new RoutedEventArgs(MenuItem.ClickEvent);
        menuItem.RaiseEvent(clickArgs);

        if (!menuItem.StaysOpenOnClick)
        {
            CloseMenu(menuItem);
        }

        return clickArgs.Handled;
    }

    private static void CloseMenu(MenuItem menuItem)
    {
        var menu = menuItem.GetLogicalAncestors().OfType<MenuBase>().FirstOrDefault()
            ?? menuItem.GetVisualAncestors().OfType<MenuBase>().FirstOrDefault();
        menu?.Close();
    }

    private static Visual FindRootVisual(Visual visual)
    {
        var current = visual;
        while (current.GetVisualParent() is Visual parent)
            current = parent;
        return current;
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
