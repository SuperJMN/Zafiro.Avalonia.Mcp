using System.Text.Json;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Threading;
using Zafiro.Avalonia.Mcp.AppHost.Selectors;
using Zafiro.Avalonia.Mcp.Protocol;
using Zafiro.Avalonia.Mcp.Protocol.Messages;

namespace Zafiro.Avalonia.Mcp.AppHost.Handlers;

public sealed class ToggleHandler : IRequestHandler
{
    public string Method => ProtocolMethods.Toggle;

    public async Task<object> Handle(DiagnosticRequest request)
    {
        string? selector = null;
        bool? state = null;

        if (request.Params is JsonElement p)
        {
            if (p.TryGetProperty("selector", out var s)) selector = s.GetString();
            if (p.TryGetProperty("state", out var st)) state = st.GetBoolean();
        }

        return await Dispatcher.UIThread.InvokeAsync<object>(() =>
        {
            var (visual, error) = SelectorRequestHelper.ResolveSingle(selector);
            if (visual is null) return error!;
            return Toggle(visual, state);
        });
    }

    internal static object Toggle(Visual visual, bool? state)
    {
        var nodeId = NodeRegistry.GetOrRegister(visual);

        if (visual is ToggleSwitch toggleSwitch)
        {
            toggleSwitch.IsChecked = state ?? !toggleSwitch.IsChecked;
            return new { success = true, nodeId, isChecked = toggleSwitch.IsChecked };
        }

        if (visual is ToggleButton toggle)
        {
            toggle.IsChecked = state ?? !(toggle.IsChecked == true);
            return new { success = true, nodeId, isChecked = toggle.IsChecked };
        }

        return new { error = "selector did not resolve to a ToggleButton, CheckBox, RadioButton, or ToggleSwitch", nodeId };
    }
}
