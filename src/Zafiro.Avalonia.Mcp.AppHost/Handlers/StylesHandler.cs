using System.Text.Json;
using Avalonia;
using Avalonia.Threading;
using Zafiro.Avalonia.Mcp.AppHost.Provenance;
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
        var includeInactive = false;
        List<string>? filterNames = null;
        if (request.Params is JsonElement parameters)
        {
            if (parameters.ValueKind != JsonValueKind.Object)
                return HandlerResult.InvalidParam("params", "Parameters must be an object.");
            if (parameters.TryGetProperty("selector", out var selected))
            {
                if (selected.ValueKind != JsonValueKind.String)
                    return HandlerResult.InvalidParam("selector", "The selector must be a string.");
                selector = selected.GetString();
            }
            if (parameters.TryGetProperty("includeInactive", out var inactive))
            {
                if (inactive.ValueKind is not (JsonValueKind.True or JsonValueKind.False))
                    return HandlerResult.InvalidParam("includeInactive", "includeInactive must be a boolean.");
                includeInactive = inactive.GetBoolean();
            }
            if (parameters.TryGetProperty("propertyNames", out var names))
            {
                if (names.ValueKind != JsonValueKind.Array || names.GetArrayLength() > 64 ||
                    names.EnumerateArray().Any(name => name.ValueKind != JsonValueKind.String))
                    return HandlerResult.InvalidParam("propertyNames", "Supply an array of at most 64 property names.");
                filterNames = names.EnumerateArray().Select(name => name.GetString()!).ToList();
            }
        }

        return await Dispatcher.UIThread.InvokeAsync<object>(() =>
        {
            var (visual, error) = SelectorRequestHelper.ResolveSingle(selector);
            return visual is null ? error! : GetStyles(visual, includeInactive, filterNames);
        });
    }

    internal static object GetStyles(Visual visual, bool includeInactive, List<string>? filterNames)
    {
        HashSet<AvaloniaProperty>? filter = null;
        if (filterNames is not null)
        {
            filter = [];
            foreach (var name in filterNames)
            {
                var (property, error) = PropertyLookup.Resolve(visual, name);
                if (error is not null)
                    return error;
                filter.Add(property!);
            }
        }
        return PropertyProvenanceInspector.Styles(visual, includeInactive, filter);
    }
}
