using System.Text.Json;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Diagnostics;
using Avalonia.Threading;
using Zafiro.Avalonia.Mcp.Protocol;
using Zafiro.Avalonia.Mcp.Protocol.Messages;

namespace Zafiro.Avalonia.Mcp.AppHost.Handlers;

public sealed class PropertyHandler : IRequestHandler
{
    public string Method => ProtocolMethods.GetProperties;

    public async Task<object> Handle(DiagnosticRequest request)
    {
        int nodeId = 0;
        List<string>? filterNames = null;

        if (request.Params is JsonElement p)
        {
            if (p.TryGetProperty("nodeId", out var nid)) nodeId = nid.GetInt32();
            if (p.TryGetProperty("propertyNames", out var names) && names.ValueKind == JsonValueKind.Array)
                filterNames = names.EnumerateArray().Select(e => e.GetString()!).ToList();
        }

        return await Dispatcher.UIThread.InvokeAsync<object>(() =>
        {
            var visual = NodeRegistry.Resolve(nodeId);
            if (visual is null) return new { error = $"Node {nodeId} not found" };

            if (visual is not AvaloniaObject ao) return new { error = "Not an AvaloniaObject" };

            var registeredProps = AvaloniaPropertyRegistry.Instance.GetRegistered(ao)
                .Concat(AvaloniaPropertyRegistry.Instance.GetRegisteredAttached(ao.GetType()));

            var result = new List<Protocol.Models.PropertyInfo>();
            foreach (var prop in registeredProps)
            {
                if (filterNames is not null && !filterNames.Contains(prop.Name, StringComparer.OrdinalIgnoreCase))
                    continue;

                try
                {
                    var diag = ao.GetDiagnostic(prop);
                    var value = ao.GetValue(prop);

                    result.Add(new Protocol.Models.PropertyInfo
                    {
                        Name = prop.Name,
                        Type = prop.PropertyType.Name,
                        Value = value?.ToString(),
                        Priority = diag?.Priority.ToString()
                    });
                }
                catch
                {
                    // Skip properties that throw
                }
            }

            return result;
        });
    }
}
