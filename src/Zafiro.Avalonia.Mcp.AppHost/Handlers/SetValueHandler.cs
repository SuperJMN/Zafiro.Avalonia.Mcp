using System.Text.Json;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Threading;
using Zafiro.Avalonia.Mcp.AppHost.Selectors;
using Zafiro.Avalonia.Mcp.Protocol;
using Zafiro.Avalonia.Mcp.Protocol.Messages;

namespace Zafiro.Avalonia.Mcp.AppHost.Handlers;

public sealed class SetValueHandler : IRequestHandler
{
    public string Method => ProtocolMethods.SetValue;

    public async Task<object> Handle(DiagnosticRequest request)
    {
        string? selector = null;
        double value = 0;

        if (request.Params is JsonElement p)
        {
            if (p.TryGetProperty("selector", out var s)) selector = s.GetString();
            if (p.TryGetProperty("value", out var v)) value = v.GetDouble();
        }

        return await Dispatcher.UIThread.InvokeAsync<object>(() =>
        {
            var (visual, error) = SelectorRequestHelper.ResolveSingle(selector);
            if (visual is null) return error!;
            return SetValue(visual, value);
        });
    }

    internal static object SetValue(Visual visual, double value)
    {
        var nodeId = NodeRegistry.GetOrRegister(visual);

        if (visual is Slider slider)
        {
            slider.Value = Math.Clamp(value, slider.Minimum, slider.Maximum);
            return new { success = true, nodeId, value = slider.Value };
        }

        if (visual is ProgressBar progressBar)
        {
            progressBar.Value = Math.Clamp(value, progressBar.Minimum, progressBar.Maximum);
            return new { success = true, nodeId, value = progressBar.Value };
        }

        if (visual is NumericUpDown numericUpDown)
        {
            var decimalValue = (decimal)value;
            decimalValue = Math.Clamp(decimalValue, numericUpDown.Minimum, numericUpDown.Maximum);
            numericUpDown.Value = decimalValue;
            return new { success = true, nodeId, value = (double)(numericUpDown.Value ?? decimalValue) };
        }

        return new { error = "selector did not resolve to a Slider, ProgressBar, or NumericUpDown", nodeId };
    }
}
