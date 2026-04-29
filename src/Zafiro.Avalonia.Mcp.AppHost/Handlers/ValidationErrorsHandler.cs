using System.Text.Json;
using Avalonia.Controls;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Zafiro.Avalonia.Mcp.AppHost.Selectors;
using Zafiro.Avalonia.Mcp.Protocol;
using Zafiro.Avalonia.Mcp.Protocol.Messages;

namespace Zafiro.Avalonia.Mcp.AppHost.Handlers;

public sealed class ValidationErrorsHandler : IRequestHandler
{
    public string Method => ProtocolMethods.GetValidationErrors;

    public async Task<object> Handle(DiagnosticRequest request)
    {
        string? selector = null;
        if (request.Params is JsonElement p && p.TryGetProperty("selector", out var sel))
            selector = sel.GetString();

        return await Dispatcher.UIThread.InvokeAsync<object>(() =>
            ScanForErrors(selector));
    }

    public static object ScanForErrors(string? selector, IEnumerable<Control>? overrideCandidates = null)
    {
        IEnumerable<Control> candidates;
        string scope;

        if (overrideCandidates is not null)
        {
            candidates = overrideCandidates;
            scope = selector ?? "test";
        }
        else if (!string.IsNullOrWhiteSpace(selector))
        {
            var engine = new SelectorEngine();
            var matches = engine.Resolve(selector!);
            candidates = matches
                .SelectMany(v => new[] { v }.Concat(v.GetVisualDescendants()))
                .OfType<Control>()
                .Distinct();
            scope = selector!;
        }
        else
        {
            candidates = NodeRegistry.GetRoots()
                .SelectMany(w => new[] { (Control)w }.Concat(w.GetVisualDescendants().OfType<Control>()))
                .Distinct();
            scope = "app";
        }

        var items = new List<object>();
        foreach (var control in candidates)
        {
            if (!DataValidationErrors.GetHasErrors(control))
                continue;

            var errors = (DataValidationErrors.GetErrors(control) ?? Enumerable.Empty<object>())
                .Select(e => new
                {
                    message = e is Exception ex ? ex.Message : e?.ToString() ?? "",
                    source = e?.GetType().Name ?? "unknown"
                })
                .ToList();

            items.Add(new
            {
                nodeId = NodeRegistry.GetOrRegister(control),
                type = control.GetType().Name,
                name = control.Name ?? "",
                hasErrors = true,
                errors
            });
        }

        return new
        {
            scope,
            count = items.Count,
            items
        };
    }
}
