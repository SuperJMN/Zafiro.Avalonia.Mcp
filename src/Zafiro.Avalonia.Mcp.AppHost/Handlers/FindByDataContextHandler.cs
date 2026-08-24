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
    private readonly SelectorEngine _selectorEngine;

    public FindByDataContextHandler() : this(SelectorEngine.Default)
    {
    }

    internal FindByDataContextHandler(SelectorEngine selectorEngine)
    {
        _selectorEngine = selectorEngine;
    }

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

        var visuals = await _selectorEngine.ResolveDataContextAsync(selector, predicate);

        return await Dispatcher.UIThread.InvokeAsync<object>(() =>
        {
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
