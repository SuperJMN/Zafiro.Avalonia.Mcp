using Xunit;
using Zafiro.Avalonia.Mcp.Protocol.Selectors;

namespace Zafiro.Avalonia.Mcp.Tests.Selectors;

public class SelectorParserTests
{
    [Fact]
    public void Parses_TypeOnly()
    {
        var p = SelectorParser.Parse("Button");
        var c = Single(p).Steps.Single().Compound;
        Assert.Equal("Button", c.TypeName);
        Assert.Null(c.NodeId);
        Assert.Empty(c.Filters);
    }

    [Fact]
    public void Parses_NodeIdOnly()
    {
        var p = SelectorParser.Parse("#42");
        var c = Single(p).Steps.Single().Compound;
        Assert.Null(c.TypeName);
        Assert.Equal(42, c.NodeId);
    }

    [Fact]
    public void Parses_TypeWithNodeId()
    {
        var p = SelectorParser.Parse("Button#42");
        var c = Single(p).Steps.Single().Compound;
        Assert.Equal("Button", c.TypeName);
        Assert.Equal(42, c.NodeId);
    }

    [Fact]
    public void Parses_AttributeFilter_Equal()
    {
        var p = SelectorParser.Parse("Button[Name=Save]");
        var f = (AttributeFilter)Single(p).Steps.Single().Compound.Filters.Single();
        Assert.Equal("Name", f.Path);
        Assert.Equal(AttrOp.Equal, f.Op);
        Assert.Equal("Save", f.Value);
        Assert.False(f.IsDataContext);
    }

    [Fact]
    public void Parses_AttributeFilter_Quoted()
    {
        var p = SelectorParser.Parse("Button[Name=\"Save Changes\"]");
        var f = (AttributeFilter)Single(p).Steps.Single().Compound.Filters.Single();
        Assert.Equal("Save Changes", f.Value);
    }

    [Theory]
    [InlineData("Button[Name*=ave]", AttrOp.Contains)]
    [InlineData("Button[Name^=Sa]", AttrOp.StartsWith)]
    [InlineData("Button[Name$=ve]", AttrOp.EndsWith)]
    public void Parses_AttributeFilter_Operators(string sel, AttrOp expected)
    {
        var p = SelectorParser.Parse(sel);
        var f = (AttributeFilter)Single(p).Steps.Single().Compound.Filters.Single();
        Assert.Equal(expected, f.Op);
    }

    [Fact]
    public void Parses_DataContextAttribute()
    {
        var p = SelectorParser.Parse("ListBoxItem[dc.Id=42]");
        var f = (AttributeFilter)Single(p).Steps.Single().Compound.Filters.Single();
        Assert.True(f.IsDataContext);
        Assert.Equal("Id", f.Path);
        Assert.Equal("42", f.Value);
    }

    [Fact]
    public void Parses_DataContextNestedPath()
    {
        var p = SelectorParser.Parse("ListBoxItem[dc.User.Name=Alice]");
        var f = (AttributeFilter)Single(p).Steps.Single().Compound.Filters.Single();
        Assert.True(f.IsDataContext);
        Assert.Equal("User.Name", f.Path);
    }

    [Fact]
    public void Parses_DataContextPredicate()
    {
        var p = SelectorParser.Parse("ListBoxItem[dc:'Id == 42 && IsActive']");
        var f = (DataContextPredicateFilter)Single(p).Steps.Single().Compound.Filters.Single();
        Assert.Equal("Id == 42 && IsActive", f.Expression);
    }

    [Fact]
    public void Parses_PseudoClasses()
    {
        var p = SelectorParser.Parse("Button:enabled:visible");
        var filters = Single(p).Steps.Single().Compound.Filters.OfType<PseudoFilter>().ToList();
        Assert.Equal(2, filters.Count);
        Assert.Equal("enabled", filters[0].Name);
        Assert.Equal("visible", filters[1].Name);
    }

    [Fact]
    public void Parses_HyphenatedPseudo()
    {
        var p = SelectorParser.Parse("Button:has-text(\"Sign in\")");
        var f = (PseudoFilter)Single(p).Steps.Single().Compound.Filters.Single();
        Assert.Equal("has-text", f.Name);
        Assert.Equal("Sign in", f.Argument);
    }

    [Fact]
    public void Parses_Nth()
    {
        var p = SelectorParser.Parse("ListBoxItem:nth(2)");
        var f = (PseudoFilter)Single(p).Steps.Single().Compound.Filters.Single();
        Assert.Equal("nth", f.Name);
        Assert.Equal("2", f.Argument);
    }

    [Fact]
    public void Parses_DescendantCombinatorImplicit()
    {
        var p = SelectorParser.Parse("ListBox ListBoxItem");
        var path = Single(p);
        Assert.Equal(2, path.Steps.Count);
        Assert.Equal(Combinator.Self, path.Steps[0].Combinator);
        Assert.Equal(Combinator.Descendant, path.Steps[1].Combinator);
        Assert.Equal("ListBox", path.Steps[0].Compound.TypeName);
        Assert.Equal("ListBoxItem", path.Steps[1].Compound.TypeName);
    }

    [Fact]
    public void Parses_DescendantCombinatorExplicit()
    {
        var p = SelectorParser.Parse("ListBox >> ListBoxItem");
        var path = Single(p);
        Assert.Equal(Combinator.Descendant, path.Steps[1].Combinator);
    }

    [Fact]
    public void Parses_ChildCombinator()
    {
        var p = SelectorParser.Parse("ListBox > ListBoxItem");
        var path = Single(p);
        Assert.Equal(Combinator.Child, path.Steps[1].Combinator);
    }

    [Fact]
    public void Parses_StarTypeAny()
    {
        var p = SelectorParser.Parse("*[Name=foo]");
        var c = Single(p).Steps.Single().Compound;
        Assert.Null(c.TypeName);
        Assert.Single(c.Filters);
    }

    [Fact]
    public void Parses_NameShorthand_HashIdent()
    {
        var p = SelectorParser.Parse("#Save");
        var f = (AttributeFilter)Single(p).Steps.Single().Compound.Filters.Single();
        Assert.Equal("Name", f.Path);
        Assert.Equal("Save", f.Value);
    }

    [Fact]
    public void Parses_Alternatives()
    {
        var p = SelectorParser.Parse("Button, MenuItem");
        Assert.Equal(2, p.Alternatives.Count);
        Assert.Equal("Button", p.Alternatives[0].Steps[0].Compound.TypeName);
        Assert.Equal("MenuItem", p.Alternatives[1].Steps[0].Compound.TypeName);
    }

    [Fact]
    public void Parses_Complex()
    {
        var p = SelectorParser.Parse("ListBox#10 >> ListBoxItem[dc.Id=42]:nth(0)");
        var path = Single(p);
        Assert.Equal(2, path.Steps.Count);
        Assert.Equal(10, path.Steps[0].Compound.NodeId);
        Assert.Equal(Combinator.Descendant, path.Steps[1].Combinator);
        var item = path.Steps[1].Compound;
        Assert.Equal("ListBoxItem", item.TypeName);
        Assert.Equal(2, item.Filters.Count);
    }

    [Fact]
    public void Throws_Empty()
    {
        Assert.Throws<SelectorParseException>(() => SelectorParser.Parse(""));
    }

    [Fact]
    public void Throws_UnterminatedString()
    {
        Assert.Throws<SelectorParseException>(() => SelectorParser.Parse("Button[Name='unterminated"));
    }

    [Fact]
    public void Throws_TrailingGarbage()
    {
        Assert.Throws<SelectorParseException>(() => SelectorParser.Parse("Button @"));
    }

    [Fact]
    public void Throws_EmptyCompoundAfterCombinator()
    {
        Assert.Throws<SelectorParseException>(() => SelectorParser.Parse("Button >>"));
    }

    private static SelectorPath Single(ParsedSelector p) => Assert.Single(p.Alternatives);
}
