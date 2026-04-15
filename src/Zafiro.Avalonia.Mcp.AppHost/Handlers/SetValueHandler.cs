using System.Text.Json;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Threading;
using Zafiro.Avalonia.Mcp.Protocol;
using Zafiro.Avalonia.Mcp.Protocol.Messages;

namespace Zafiro.Avalonia.Mcp.AppHost.Handlers;

public sealed class SetValueHandler : IRequestHandler
{
    public string Method => ProtocolMethods.SetValue;

    public async Task<object> Handle(DiagnosticRequest request)
    {
        int nodeId = 0;
        double value = 0;

        if (request.Params is JsonElement p)
        {
            if (p.TryGetProperty("nodeId", out var nid)) nodeId = nid.GetInt32();
            if (p.TryGetProperty("value", out var v)) value = v.GetDouble();
        }

        return await Dispatcher.UIThread.InvokeAsync<object>(() =>
        {
            var visual = NodeRegistry.Resolve(nodeId);
            if (visual is null) return new { error = $"Node {nodeId} not found" };

            if (visual is Slider slider)
            {
                slider.Value = Math.Clamp(value, slider.Minimum, slider.Maximum);
                return new { success = true, value = slider.Value };
            }

            if (visual is ProgressBar progressBar)
            {
                progressBar.Value = Math.Clamp(value, progressBar.Minimum, progressBar.Maximum);
                return new { success = true, value = progressBar.Value };
            }

            if (visual is NumericUpDown numericUpDown)
            {
                var decimalValue = (decimal)value;
                decimalValue = Math.Clamp(decimalValue, numericUpDown.Minimum, numericUpDown.Maximum);
                numericUpDown.Value = decimalValue;
                return new { success = true, value = (double)(numericUpDown.Value ?? decimalValue) };
            }

            return new { error = "Node is not a Slider, ProgressBar, or NumericUpDown" };
        });
    }
}
