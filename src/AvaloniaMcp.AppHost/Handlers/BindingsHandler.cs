using System.Text.Json;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Diagnostics;
using Avalonia.Threading;
using AvaloniaMcp.Protocol;
using AvaloniaMcp.Protocol.Messages;

namespace AvaloniaMcp.AppHost.Handlers;

/// <summary>
/// Inspects active bindings on a control's AvaloniaProperties.
/// Returns binding paths, modes, sources, and current values — essential for MVVM debugging.
/// </summary>
public sealed class BindingsHandler : IRequestHandler
{
    public string Method => ProtocolMethods.GetBindings;

    public async Task<object> Handle(DiagnosticRequest request)
    {
        int nodeId = 0;
        if (request.Params is JsonElement p && p.TryGetProperty("nodeId", out var nid))
            nodeId = nid.GetInt32();

        return await Dispatcher.UIThread.InvokeAsync<object>(() =>
        {
            var visual = NodeRegistry.Resolve(nodeId);
            if (visual is not AvaloniaObject ao)
                return new { error = $"Node {nodeId} not found" };

            var bindings = new List<object>();

            foreach (var prop in AvaloniaPropertyRegistry.Instance.GetRegistered(ao))
            {
                var diag = ao.GetDiagnostic(prop);
                if (diag is null) continue;

                // Only include properties that have non-default values or bindings
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
        });
    }
}
