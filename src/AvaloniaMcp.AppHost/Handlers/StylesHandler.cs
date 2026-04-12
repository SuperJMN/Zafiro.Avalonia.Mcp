using System.Text.Json;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Diagnostics;
using Avalonia.Styling;
using Avalonia.Threading;
using AvaloniaMcp.Protocol;
using AvaloniaMcp.Protocol.Messages;

namespace AvaloniaMcp.AppHost.Handlers;

public sealed class StylesHandler : IRequestHandler
{
    public string Method => ProtocolMethods.GetStyles;

    public async Task<object> Handle(DiagnosticRequest request)
    {
        int nodeId = 0;
        var includeDefaults = false;
        List<string>? filterNames = null;

        if (request.Params is JsonElement p)
        {
            if (p.TryGetProperty("nodeId", out var nid)) nodeId = nid.GetInt32();
            if (p.TryGetProperty("includeDefaults", out var id)) includeDefaults = id.GetBoolean();
            if (p.TryGetProperty("propertyNames", out var names) && names.ValueKind == JsonValueKind.Array)
                filterNames = names.EnumerateArray().Select(e => e.GetString()!).ToList();
        }

        return await Dispatcher.UIThread.InvokeAsync<object>(() =>
        {
            var visual = NodeRegistry.Resolve(nodeId);
            if (visual is not StyledElement styled) return new { error = $"Node {nodeId} not found or not a StyledElement" };

            var styles = new List<object>();

            if (styled is AvaloniaObject ao)
            {
                var props = AvaloniaPropertyRegistry.Instance.GetRegistered(ao);
                foreach (var prop in props)
                {
                    if (filterNames is not null && !filterNames.Contains(prop.Name, StringComparer.OrdinalIgnoreCase))
                        continue;

                    try
                    {
                        var diag = ao.GetDiagnostic(prop);
                        if (!includeDefaults && !ao.IsSet(prop))
                            continue;

                        var value = ao.GetValue(prop);
                        styles.Add(new
                        {
                            property = prop.Name,
                            value = value?.ToString(),
                            type = prop.PropertyType.Name,
                            source = diag?.Priority.ToString() ?? "unknown"
                        });
                    }
                    catch { }
                }
            }

            var appliedClasses = styled.Classes.ToList();

            return new
            {
                classes = appliedClasses,
                setters = styles
            };
        });
    }
}
