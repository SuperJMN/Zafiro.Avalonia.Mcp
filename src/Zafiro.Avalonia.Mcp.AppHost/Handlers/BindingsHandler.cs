using System.Text.Json;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Diagnostics;
using Avalonia.Threading;
using Zafiro.Avalonia.Mcp.AppHost.Selectors;
using Zafiro.Avalonia.Mcp.Protocol;
using Zafiro.Avalonia.Mcp.Protocol.Messages;

namespace Zafiro.Avalonia.Mcp.AppHost.Handlers;

/// <summary>
/// Inspects active bindings on a control's AvaloniaProperties.
/// </summary>
public sealed class BindingsHandler : IRequestHandler
{
    public string Method => ProtocolMethods.GetBindings;

    public async Task<object> Handle(DiagnosticRequest request)
    {
        string? selector = null;
        if (request.Params is JsonElement p && p.TryGetProperty("selector", out var s))
            selector = s.GetString();

        return await Dispatcher.UIThread.InvokeAsync<object>(() =>
        {
            var (visual, error) = SelectorRequestHelper.ResolveSingle(selector, requireSingle: false);
            if (visual is null) return error!;
            return GetBindings(visual);
        });
    }

    internal static object GetBindings(Visual visual)
    {
        var nodeId = NodeRegistry.GetOrRegister(visual);
        if (visual is not AvaloniaObject ao)
            return new { error = "selector did not resolve to AvaloniaObject", nodeId };

        var bindings = new List<object>();

        foreach (var prop in AvaloniaPropertyRegistry.Instance.GetRegistered(ao))
        {
            var diag = ao.GetDiagnostic(prop);
            if (diag is null) continue;

            if (diag.Priority == BindingPriority.Unset) continue;

            string? diagnostic = null;
            try
            {
                diagnostic = diag.Diagnostic;
            }
            catch { }

            var currentValue = diag.Value;
            var valueStr = currentValue switch
            {
                null => "null",
                string s when s.Length > 120 => s[..117] + "...",
                _ => currentValue.ToString() is { Length: > 120 } ts ? ts[..117] + "..." : currentValue.ToString()
            };

            bindings.Add(new
            {
                property = prop.Name,
                propertyType = prop.PropertyType.Name,
                priority = diag.Priority.ToString(),
                value = valueStr,
                diagnostic,
                isLocalValue = diag.Priority == BindingPriority.LocalValue,
            });
        }

        return new { nodeId, type = ao.GetType().Name, bindings };
    }
}
