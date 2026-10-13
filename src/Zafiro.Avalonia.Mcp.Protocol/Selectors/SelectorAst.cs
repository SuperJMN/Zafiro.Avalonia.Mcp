namespace Zafiro.Avalonia.Mcp.Protocol.Selectors;

public enum Combinator
{
    Self,
    Descendant,
    Child,
}

public enum AttrOp
{
    Equal,
    Contains,
    StartsWith,
    EndsWith,
}

public abstract record SelectorFilter;

public sealed record AttributeFilter(
    string Path,
    AttrOp Op,
    string Value,
    bool IsDataContext) : SelectorFilter;

public sealed record DataContextPredicateFilter(string Expression) : SelectorFilter;

public sealed record PseudoFilter(string Name, string? Argument) : SelectorFilter;

/// <summary>Matches a style class in Avalonia's <c>Classes</c> collection (case-sensitive, like Avalonia selectors).</summary>
public sealed record ClassFilter(string Name) : SelectorFilter;

public sealed record CompoundSelector(
    string? TypeName,
    int? NodeId,
    IReadOnlyList<SelectorFilter> Filters);

public sealed record SelectorStep(Combinator Combinator, CompoundSelector Compound);

public sealed record SelectorPath(IReadOnlyList<SelectorStep> Steps);

public sealed record ParsedSelector(IReadOnlyList<SelectorPath> Alternatives);
