using System.Text.Json;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;
using Avalonia.VisualTree;
using AvaloniaMcp.Protocol;
using AvaloniaMcp.Protocol.Messages;
using AvaloniaMcp.Protocol.Models;

namespace AvaloniaMcp.AppHost.Handlers;

public sealed class SearchHandler : IRequestHandler
{
    public string Method => ProtocolMethods.Search;

    public async Task<object> Handle(DiagnosticRequest request)
    {
        var query = "";
        var limit = 20;
        int? scopeNodeId = null;

        if (request.Params is JsonElement p)
        {
            if (p.TryGetProperty("query", out var q)) query = q.GetString() ?? "";
            if (p.TryGetProperty("limit", out var l)) limit = l.GetInt32();
            if (p.TryGetProperty("scopeNodeId", out var s)) scopeNodeId = s.GetInt32();
        }

        return await Dispatcher.UIThread.InvokeAsync<object>(() =>
        {
            var results = new List<NodeInfo>();
            IEnumerable<Visual> searchScope;

            if (scopeNodeId.HasValue)
            {
                var scope = NodeRegistry.Resolve(scopeNodeId.Value);
                if (scope is null) return new { error = $"Scope node {scopeNodeId} not found" };
                searchScope = scope.GetVisualDescendants();
            }
            else
            {
                searchScope = NodeRegistry.GetWindows()
                    .SelectMany(w => new[] { (Visual)w }.Concat(w.GetVisualDescendants()));
            }

            foreach (var visual in searchScope)
            {
                if (results.Count >= limit) break;
                if (Matches(visual, query))
                {
                    results.Add(NodeInfoBuilder.Create(visual));
                }
            }

            return results;
        });
    }

    private static bool Matches(Visual visual, string query)
    {
        if (string.IsNullOrEmpty(query)) return true;

        var typeName = visual.GetType().Name;
        if (typeName.Contains(query, StringComparison.OrdinalIgnoreCase))
            return true;

        if (visual is Control control && control.Name is not null
            && control.Name.Contains(query, StringComparison.OrdinalIgnoreCase))
            return true;

        if (visual is TextBlock tb && tb.Text?.Contains(query, StringComparison.OrdinalIgnoreCase) == true)
            return true;

        if (visual is ContentControl cc && cc.Content is string s
            && s.Contains(query, StringComparison.OrdinalIgnoreCase))
            return true;

        return false;
    }
}
