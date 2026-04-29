using System.Text.Json;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Diagnostics;
using Avalonia.Styling;
using Avalonia.Threading;
using Zafiro.Avalonia.Mcp.AppHost.Selectors;
using Zafiro.Avalonia.Mcp.Protocol;
using Zafiro.Avalonia.Mcp.Protocol.Messages;

namespace Zafiro.Avalonia.Mcp.AppHost.Handlers;

public sealed class StylesHandler : IRequestHandler
{
    public string Method => ProtocolMethods.GetStyles;

    public async Task<object> Handle(DiagnosticRequest request)
    {
        string? selector = null;
        var includeDefaults = false;
        List<string>? filterNames = null;

        if (request.Params is JsonElement p)
        {
            if (p.TryGetProperty("selector", out var s)) selector = s.GetString();
            if (p.TryGetProperty("includeDefaults", out var id)) includeDefaults = id.GetBoolean();
            if (p.TryGetProperty("propertyNames", out var names) && names.ValueKind == JsonValueKind.Array)
                filterNames = names.EnumerateArray().Select(e => e.GetString()!).ToList();
        }

        return await Dispatcher.UIThread.InvokeAsync<object>(() =>
        {
            var (visual, error) = SelectorRequestHelper.ResolveSingle(selector, requireSingle: false);
            if (visual is null) return error!;
            return GetStyles(visual, includeDefaults, filterNames);
        });
    }

    internal static object GetStyles(Visual visual, bool includeDefaults, List<string>? filterNames)
    {
        var nodeId = NodeRegistry.GetOrRegister(visual);
        if (visual is not StyledElement styled)
            return new { error = "selector did not resolve to a StyledElement", nodeId };

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
            nodeId,
            classes = appliedClasses,
            setters = styles
        };
    }
}
