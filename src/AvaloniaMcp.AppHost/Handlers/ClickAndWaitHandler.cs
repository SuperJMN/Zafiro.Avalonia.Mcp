using System.Diagnostics;
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

public sealed class ClickAndWaitHandler : IRequestHandler
{
    public string Method => ProtocolMethods.ClickAndWait;

    public async Task<object> Handle(DiagnosticRequest request)
    {
        int nodeId = 0;
        var waitQuery = "";
        var waitCondition = "exists";
        string? waitValue = null;
        var timeoutMs = 5000;

        if (request.Params is JsonElement p)
        {
            if (p.TryGetProperty("nodeId", out var nid)) nodeId = nid.GetInt32();
            if (p.TryGetProperty("waitQuery", out var q)) waitQuery = q.GetString() ?? "";
            if (p.TryGetProperty("waitCondition", out var c)) waitCondition = c.GetString() ?? "exists";
            if (p.TryGetProperty("waitValue", out var v)) waitValue = v.GetString();
            if (p.TryGetProperty("timeoutMs", out var t)) timeoutMs = t.GetInt32();
        }

        timeoutMs = Math.Clamp(timeoutMs, 100, 30000);

        var sw = Stopwatch.StartNew();

        // Perform click on UI thread
        var clickResult = await Dispatcher.UIThread.InvokeAsync<object>(() =>
        {
            var visual = NodeRegistry.Resolve(nodeId);
            if (visual is null) return new { error = $"Node {nodeId} not found" };

            if (visual is ToggleButton toggle)
            {
                toggle.IsChecked = visual is RadioButton ? true : toggle.IsChecked != true;
                return new { success = true, method = "toggle", isChecked = toggle.IsChecked };
            }

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

                if (control.Focusable) control.Focus();
                var center = new Point(control.Bounds.Width / 2, control.Bounds.Height / 2);
                control.RaiseEvent(new RoutedEventArgs(InputElement.PointerPressedEvent));
                control.RaiseEvent(new RoutedEventArgs(InputElement.PointerReleasedEvent));
                return new { success = true, method = "pointer_simulation" };
            }

            return new { error = "Cannot click this element" };
        });

        // If click failed, return immediately
        if (clickResult is { } cr)
        {
            var json = System.Text.Json.JsonSerializer.Serialize(cr);
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.TryGetProperty("error", out _))
                return new { success = false, click_result = clickResult, elapsed_ms = (int)sw.ElapsedMilliseconds };
        }

        // Run the wait polling loop
        var waitResult = await WaitForHandler.PollUntilCondition(waitQuery, waitCondition, waitValue, timeoutMs);

        var totalElapsed = (int)sw.ElapsedMilliseconds;

        if (waitResult is not null)
            return new { success = true, click_result = clickResult, wait_result = waitResult, elapsed_ms = totalElapsed };

        return new
        {
            success = false,
            click_result = clickResult,
            wait_result = new { success = false, error = $"Timeout after {timeoutMs}ms", condition = waitCondition, query = waitQuery },
            elapsed_ms = totalElapsed
        };
    }
}
