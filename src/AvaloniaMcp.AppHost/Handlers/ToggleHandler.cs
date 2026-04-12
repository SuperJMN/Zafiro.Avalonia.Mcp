using System.Text.Json;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Threading;
using AvaloniaMcp.Protocol;
using AvaloniaMcp.Protocol.Messages;

namespace AvaloniaMcp.AppHost.Handlers;

public sealed class ToggleHandler : IRequestHandler
{
    public string Method => ProtocolMethods.Toggle;

    public async Task<object> Handle(DiagnosticRequest request)
    {
        int nodeId = 0;
        bool? state = null;

        if (request.Params is JsonElement p)
        {
            if (p.TryGetProperty("nodeId", out var nid)) nodeId = nid.GetInt32();
            if (p.TryGetProperty("state", out var s)) state = s.GetBoolean();
        }

        return await Dispatcher.UIThread.InvokeAsync<object>(() =>
        {
            var visual = NodeRegistry.Resolve(nodeId);
            if (visual is null) return new { error = $"Node {nodeId} not found" };

            if (visual is ToggleSwitch toggleSwitch)
            {
                toggleSwitch.IsChecked = state ?? !toggleSwitch.IsChecked;
                return new { success = true, isChecked = toggleSwitch.IsChecked };
            }

            if (visual is ToggleButton toggle)
            {
                toggle.IsChecked = state ?? !(toggle.IsChecked == true);
                return new { success = true, isChecked = toggle.IsChecked };
            }

            return new { error = "Node is not a ToggleButton, CheckBox, RadioButton, or ToggleSwitch" };
        });
    }
}
