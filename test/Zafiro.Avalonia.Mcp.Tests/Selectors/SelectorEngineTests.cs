using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Xunit;
using Zafiro.Avalonia.Mcp.AppHost.Handlers;
using Zafiro.Avalonia.Mcp.AppHost.Selectors;

namespace Zafiro.Avalonia.Mcp.Tests.Selectors;

[Collection("Avalonia")]
public class SelectorEngineTests
{
    private readonly SelectorEngine _engine = new();

    private static T Run<T>(Func<T> f) => Dispatcher.UIThread.Invoke(f);

    [Fact]
    public void Resolves_ByType_FromScope()
    {
        var (root, save) = BuildTree();
        var matches = Run(() => _engine.Resolve("Button", root));
        Assert.Equal(2, matches.Count); // save + disabled
        Assert.Contains(save, matches);
    }

    [Fact]
    public void Resolves_ByName()
    {
        var (root, _) = BuildTree();
        var matches = Run(() => _engine.Resolve("[Name=SaveBtn]", root));
        Assert.Single(matches);
        Assert.Equal("SaveBtn", ((Control)matches[0]).Name);
    }

    [Fact]
    public void Resolves_HashName_Shorthand()
    {
        var (root, _) = BuildTree();
        var matches = Run(() => _engine.Resolve("#SaveBtn", root));
        Assert.Single(matches);
    }

    [Fact]
    public void Resolves_HasText()
    {
        var (root, _) = BuildTree();
        var matches = Run(() => _engine.Resolve("Button:has-text(\"Save\")", root));
        Assert.Single(matches);
    }

    [Fact]
    public void Resolves_Disabled()
    {
        var (root, _) = BuildTree();
        var matches = Run(() => _engine.Resolve("Button:disabled", root));
        Assert.Single(matches);
        Assert.Equal("DisabledBtn", ((Control)matches[0]).Name);
    }

    [Fact]
    public void Resolves_Enabled()
    {
        var (root, _) = BuildTree();
        var matches = Run(() => _engine.Resolve("Button:enabled", root));
        // SaveBtn is enabled, DisabledBtn is not
        Assert.Single(matches);
        Assert.Equal("SaveBtn", ((Control)matches[0]).Name);
    }

    [Fact]
    public void Resolves_Descendant_Implicit()
    {
        var (root, _) = BuildTree();
        var matches = Run(() => _engine.Resolve("StackPanel TextBox", root));
        Assert.Single(matches);
        Assert.IsType<TextBox>(matches[0]);
    }

    [Fact]
    public void Resolves_Descendant_Explicit()
    {
        var (root, _) = BuildTree();
        var matches = Run(() => _engine.Resolve("StackPanel >> Button", root));
        Assert.Equal(2, matches.Count); // Both buttons
    }

    [Fact]
    public void Resolves_Nth()
    {
        var (root, _) = BuildTree();
        var matches = Run(() => _engine.Resolve("Button:nth(1)", root));
        Assert.Single(matches);
        Assert.Equal("DisabledBtn", ((Control)matches[0]).Name);
    }

    [Fact]
    public void Resolves_NodeId()
    {
        var (root, _) = BuildTree();
        var btn = Run(() => root.GetVisualDescendants().OfType<Button>().First());
        var nodeId = Run(() => NodeRegistry.GetOrRegister(btn));
        var matches = Run(() => _engine.Resolve($"#{nodeId}", root));
        Assert.Single(matches);
        Assert.Same(btn, matches[0]);
    }

    [Fact]
    public void Resolves_Alternatives_DedupesByReference()
    {
        var (root, _) = BuildTree();
        // Both alternatives match SaveBtn; should appear once.
        var matches = Run(() => _engine.Resolve("Button[Name=SaveBtn], #SaveBtn", root));
        Assert.Single(matches);
    }

    [Fact]
    public void Resolves_DataContextEquality()
    {
        var (root, _) = Run(() =>
        {
            var item1 = new Button { Name = "B1", DataContext = new VmRow(1, "Alice") };
            var item2 = new Button { Name = "B2", DataContext = new VmRow(2, "Bob") };
            var sp = new StackPanel { Children = { item1, item2 } };
            return ((Visual)sp, item1);
        });

        var matches = Run(() => _engine.Resolve("Button[dc.Id=2]", root));
        Assert.Single(matches);
        Assert.Equal("B2", ((Control)matches[0]).Name);
    }

    [Fact]
    public void Resolves_Predicate_WithEvaluator()
    {
        var engine = new SelectorEngine(new StubPredicateEvaluator());
        var (root, _) = Run(() =>
        {
            var item1 = new Button { Name = "B1", DataContext = new VmRow(1, "Alice") };
            var item2 = new Button { Name = "B2", DataContext = new VmRow(42, "Bob") };
            var sp = new StackPanel { Children = { item1, item2 } };
            return ((Visual)sp, item1);
        });

        var matches = Run(() => engine.Resolve("Button[dc:'Id == 42']", root));
        Assert.Single(matches);
        Assert.Equal("B2", ((Control)matches[0]).Name);
    }

    [Fact]
    public void Predicate_WithoutEvaluator_NoMatch()
    {
        var (root, _) = Run(() =>
        {
            var item = new Button { DataContext = new VmRow(1, "Alice") };
            var sp = new StackPanel { Children = { item } };
            return ((Visual)sp, item);
        });

        var matches = Run(() => _engine.Resolve("Button[dc:'anything']", root));
        Assert.Empty(matches);
    }

    [Fact]
    public void Resolves_Visible_FiltersOutHidden()
    {
        var (root, _) = Run(() =>
        {
            var hidden = new Button { Name = "Hidden", IsVisible = false };
            var shown = new Button { Name = "Shown" };
            var sp = new StackPanel { Children = { hidden, shown } };
            return ((Visual)sp, shown);
        });

        var matches = Run(() => _engine.Resolve("Button:visible", root));
        Assert.Single(matches);
        Assert.Equal("Shown", ((Control)matches[0]).Name);
    }

    [Fact]
    public void NoMatch_Empty()
    {
        var (root, _) = BuildTree();
        var matches = Run(() => _engine.Resolve("ListBoxItem", root));
        Assert.Empty(matches);
    }

    private static (Visual root, Button save) BuildTree() => Run(() =>
    {
        var save = new Button { Name = "SaveBtn", Content = "Save" };
        var disabled = new Button { Name = "DisabledBtn", Content = "Cancel", IsEnabled = false };
        var input = new TextBox { Name = "Input", Text = "" };
        var sp = new StackPanel { Children = { save, disabled, input } };
        return ((Visual)sp, save);
    });

    private sealed record VmRow(int Id, string Name);

    private sealed class StubPredicateEvaluator : IDataContextPredicateEvaluator
    {
        public bool Evaluate(string expression, object dataContext)
        {
            // Minimal: parses "Id == N"
            var trimmed = expression.Replace(" ", "");
            if (trimmed.StartsWith("Id==") && int.TryParse(trimmed[4..], out var n))
            {
                var prop = dataContext.GetType().GetProperty("Id");
                if (prop is null) return false;
                var value = prop.GetValue(dataContext);
                return value is int i && i == n;
            }
            return false;
        }
    }
}
