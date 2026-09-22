using System.Text.Json;
using Avalonia;
using Avalonia.Threading;
using Zafiro.Avalonia.Mcp.AppHost.Provenance;
using Zafiro.Avalonia.Mcp.AppHost.Selectors;
using Zafiro.Avalonia.Mcp.Protocol;
using Zafiro.Avalonia.Mcp.Protocol.Messages;

namespace Zafiro.Avalonia.Mcp.AppHost.Handlers;

public sealed class ExplainPropertyHandler : IRequestHandler
{
    public string Method => ProtocolMethods.ExplainProperty;

    public async Task<object> Handle(DiagnosticRequest request)
    {
        string? selector = null;
        string? propertyName = null;
        var includeCandidates = false;
        var maxDepth = 8;
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
            if (parameters.TryGetProperty("propertyName", out var name))
            {
                if (name.ValueKind != JsonValueKind.String)
                    return HandlerResult.InvalidParam("propertyName", "The property name must be a string.");
                propertyName = name.GetString();
            }
            if (parameters.TryGetProperty("includeCandidates", out var candidates))
            {
                if (candidates.ValueKind is not (JsonValueKind.True or JsonValueKind.False))
                    return HandlerResult.InvalidParam("includeCandidates", "includeCandidates must be a boolean.");
                includeCandidates = candidates.GetBoolean();
            }
            if (parameters.TryGetProperty("maxDepth", out var depth) &&
                (depth.ValueKind != JsonValueKind.Number || !depth.TryGetInt32(out maxDepth)))
                return HandlerResult.InvalidParam("maxDepth", "maxDepth must be an integer between 1 and 32.");
        }
        if (maxDepth is < 1 or > InspectionContext.MaximumDepth)
            return HandlerResult.InvalidParam("maxDepth", "maxDepth must be between 1 and 32.");

        return await Dispatcher.UIThread.InvokeAsync<object>(() =>
        {
            var (visual, error) = SelectorRequestHelper.ResolveSingle(selector);
            return visual is null ? error! : Explain(visual, propertyName ?? "", includeCandidates, maxDepth);
        });
    }

    internal static object Explain(AvaloniaObject target, string propertyName, bool includeCandidates = false, int maxDepth = 8)
    {
        var (property, error) = PropertyLookup.Resolve(target, propertyName);
        return error is not null ? error : PropertyProvenanceInspector.Explain(target, property!, includeCandidates, maxDepth);
    }
}
