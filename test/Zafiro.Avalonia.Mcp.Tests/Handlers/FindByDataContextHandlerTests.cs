using System.Text.Json;
using Avalonia.Controls;
using Xunit;
using Zafiro.Avalonia.Mcp.AppHost.Handlers;
using Zafiro.Avalonia.Mcp.AppHost.Selectors;
using Zafiro.Avalonia.Mcp.Protocol.Messages;

namespace Zafiro.Avalonia.Mcp.Tests.Handlers;

[Collection("Avalonia")]
public class FindByDataContextHandlerTests
{
    public FindByDataContextHandlerTests(AvaloniaTestFixture _) { }

    private sealed record VmItem(int Id, string Label);

    [Fact]
    public async Task FindByDataContext_MissingPredicate_ReturnsError()
    {
        var handler = new FindByDataContextHandler();
        var request = new DiagnosticRequest
        {
            Id = "t1",
            Method = "find_by_datacontext",
            Params = JsonSerializer.SerializeToElement(new { selector = "*" })
        };

        var result = await handler.Handle(request);
        var json = JsonSerializer.Serialize(result);

        Assert.Contains("error", json, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void FindByDataContext_StubEvaluator_FiltersCorrectly()
    {
        // Build a scoped visual tree directly on the test thread (no window or dispatcher needed)
        var b1 = new Button { Name = "B1", DataContext = new VmItem(1, "First") };
        var b2 = new Button { Name = "B2", DataContext = new VmItem(2, "Second") };
        var sp = new StackPanel();
        sp.Children.Add(b1);
        sp.Children.Add(b2);

        var engine = new SelectorEngine(new StubEvaluator());

        // Resolve with explicit scope so no window traversal is needed
        var matches = engine.Resolve("Button[dc:'Id == 2']", sp);

        Assert.Single(matches);
        Assert.Same(b2, matches[0]);
    }

    [Fact]
    public void FindByDataContext_StubEvaluator_ReturnsBothWhenNoFilter()
    {
        var b1 = new Button { Name = "B1", DataContext = new VmItem(1, "First") };
        var b2 = new Button { Name = "B2", DataContext = new VmItem(2, "Second") };
        var sp = new StackPanel();
        sp.Children.Add(b1);
        sp.Children.Add(b2);

        var engine = new SelectorEngine(new StubEvaluator());

        // A predicate that always matches
        var matches = engine.Resolve("Button[dc:'Id > 0']", sp);

        Assert.Equal(2, matches.Count);
    }

    private sealed class StubEvaluator : IDataContextPredicateEvaluator
    {
        public bool Evaluate(string expression, object dataContext)
        {
            var trimmed = expression.Replace(" ", "");
            if (trimmed.StartsWith("Id==") && int.TryParse(trimmed[4..], out var n))
            {
                var prop = dataContext.GetType().GetProperty("Id");
                return prop?.GetValue(dataContext) is int i && i == n;
            }
            if (trimmed.StartsWith("Id>") && int.TryParse(trimmed[3..], out var gt))
            {
                var prop = dataContext.GetType().GetProperty("Id");
                return prop?.GetValue(dataContext) is int i && i > gt;
            }
            return false;
        }
    }
}
