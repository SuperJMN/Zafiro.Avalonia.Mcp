using System.Text.Json;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
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

        if (ContainsExplicitMutation(predicate))
        {
            return HandlerResult.Error(
                DiagnosticErrorCodes.InvalidParam,
                "predicate must be a read-only boolean expression; assignments and increment/decrement operators are not allowed.",
                "Use '==' to compare values (for example, ScriptInProgress == true).",
                new { param = "predicate" });
        }

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

    private static bool ContainsExplicitMutation(string predicate)
    {
        var expression = SyntaxFactory.ParseExpression(predicate);

        return expression.DescendantNodesAndSelf().Any(node =>
            node is AssignmentExpressionSyntax ||
            node.IsKind(SyntaxKind.PreIncrementExpression) ||
            node.IsKind(SyntaxKind.PreDecrementExpression) ||
            node.IsKind(SyntaxKind.PostIncrementExpression) ||
            node.IsKind(SyntaxKind.PostDecrementExpression));
    }
}
