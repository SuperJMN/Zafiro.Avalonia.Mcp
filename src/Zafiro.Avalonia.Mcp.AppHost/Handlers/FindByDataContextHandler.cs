using System.Text.Json;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;
using Zafiro.Avalonia.Mcp.AppHost.Selectors;
using Zafiro.Avalonia.Mcp.Protocol;
using Zafiro.Avalonia.Mcp.Protocol.Messages;

namespace Zafiro.Avalonia.Mcp.AppHost.Handlers;

/// <summary>
/// Resolves UI elements whose DataContext satisfies a C# predicate expression.
/// </summary>
public sealed class FindByDataContextHandler : IRequestHandler
{
    public string Method => ProtocolMethods.FindByDataContext;

    public async Task<object> Handle(DiagnosticRequest request)
    {
        string selector = "*";
        string? predicate = null;

        if (request.Params is JsonElement p)
        {
            if (p.TryGetProperty("selector", out var s)) selector = s.GetString() ?? "*";
            if (p.TryGetProperty("predicate", out var pred)) predicate = pred.GetString();
        }

        if (string.IsNullOrEmpty(predicate))
            return new { error = "predicate is required" };

        var escapedPredicate = predicate.Replace("'", "\\'");
        var fullSelector = $"{selector}[dc:'{escapedPredicate}']";

        return await Dispatcher.UIThread.InvokeAsync<object>(() =>
        {
            var visuals = SelectorEngine.Default.Resolve(fullSelector);
            var items = visuals.Select(v => new
            {
                nodeId = NodeRegistry.GetOrRegister(v),
                type = v.GetType().Name,
                name = v is Control c ? c.Name : null,
                dataContextType = v is StyledElement se ? se.DataContext?.GetType().FullName : null
            }).ToList();

            return new { count = items.Count, items };
        });
    }
}
