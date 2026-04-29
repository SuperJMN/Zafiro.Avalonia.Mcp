using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.VisualTree;
using Zafiro.Avalonia.Mcp.AppHost.Handlers;
using Zafiro.Avalonia.Mcp.Protocol.Selectors;

namespace Zafiro.Avalonia.Mcp.AppHost.Selectors;

/// <summary>
/// Resolves a parsed selector against the live visual tree.
/// </summary>
/// <remarks>
/// Must be invoked on the UI thread by the caller.
/// </remarks>
public sealed class SelectorEngine
{
    private readonly IDataContextPredicateEvaluator? _predicateEvaluator;

    /// <summary>
    /// Shared default instance backed by the Roslyn evaluator. Can be replaced in tests.
    /// </summary>
    public static SelectorEngine Default { get; set; } = new SelectorEngine(new RoslynDataContextPredicateEvaluator());

    public SelectorEngine(IDataContextPredicateEvaluator? predicateEvaluator = null)
    {
        _predicateEvaluator = predicateEvaluator;
    }

    public IReadOnlyList<Visual> Resolve(string selector, Visual? scope = null)
    {
        var parsed = SelectorParser.Parse(selector);
        return Resolve(parsed, scope);
    }

    public IReadOnlyList<Visual> Resolve(ParsedSelector parsed, Visual? scope = null)
    {
        var seen = new HashSet<Visual>(ReferenceEqualityComparer.Instance);
        var results = new List<Visual>();

        foreach (var path in parsed.Alternatives)
        {
            var matches = ResolvePath(path, scope);
            foreach (var v in matches)
            {
                if (seen.Add(v)) results.Add(v);
            }
        }

        return results;
    }

    public Visual? ResolveSingle(string selector, Visual? scope = null)
        => Resolve(selector, scope).FirstOrDefault();

    private IReadOnlyList<Visual> ResolvePath(SelectorPath path, Visual? scope)
    {
        if (path.Steps.Count == 0) return Array.Empty<Visual>();

        // Initial seed: descendants of scope OR descendants of all windows (and the windows themselves).
        IEnumerable<Visual> initial = scope is not null
            ? new[] { scope }.Concat(scope.GetVisualDescendants())
            : NodeRegistry.GetRoots().SelectMany(w => new[] { (Visual)w }.Concat(w.GetVisualDescendants()));

        IEnumerable<Visual> current = initial.Where(v => MatchesCompound(v, path.Steps[0].Compound));

        for (int i = 1; i < path.Steps.Count; i++)
        {
            var step = path.Steps[i];
            current = step.Combinator switch
            {
                Combinator.Descendant => current.SelectMany(v => v.GetVisualDescendants()
                    .Where(d => MatchesCompound(d, step.Compound))),
                Combinator.Child => current.SelectMany(v => v.GetVisualChildren().OfType<Visual>()
                    .Where(d => MatchesCompound(d, step.Compound))),
                _ => current.Where(v => MatchesCompound(v, step.Compound)),
            };
        }

        // Apply :nth across the final compound's pseudo filters at path level
        var nth = path.Steps[^1].Compound.Filters.OfType<PseudoFilter>()
            .FirstOrDefault(f => f.Name == "nth");
        var list = current.ToList();
        if (nth is not null && int.TryParse(nth.Argument, out var idx))
        {
            return idx >= 0 && idx < list.Count ? new[] { list[idx] } : Array.Empty<Visual>();
        }
        return list;
    }

    private bool MatchesCompound(Visual visual, CompoundSelector compound)
    {
        if (compound.NodeId is int id && NodeRegistry.GetOrRegister(visual) != id)
            return false;

        if (compound.TypeName is { } typeName && !TypeMatches(visual, typeName))
            return false;

        foreach (var filter in compound.Filters)
        {
            if (filter is PseudoFilter pf && pf.Name == "nth")
                continue; // applied at path level
            if (!MatchesFilter(visual, filter))
                return false;
        }
        return true;
    }

    private bool MatchesFilter(Visual visual, SelectorFilter filter)
    {
        switch (filter)
        {
            case AttributeFilter af:
                return MatchesAttribute(visual, af);
            case DataContextPredicateFilter dpf:
                return MatchesPredicate(visual, dpf);
            case PseudoFilter pf:
                return MatchesPseudo(visual, pf);
            default:
                return false;
        }
    }

    private bool MatchesAttribute(Visual visual, AttributeFilter af)
    {
        object? value;
        if (af.IsDataContext)
        {
            if (visual is not StyledElement se) return false;
            value = ResolvePath(se.DataContext, af.Path);
        }
        else
        {
            value = ResolveAttributePath(visual, af.Path);
        }

        if (value is null) return false;
        var actual = Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture) ?? "";

        return af.Op switch
        {
            AttrOp.Equal => string.Equals(actual, af.Value, StringComparison.OrdinalIgnoreCase),
            AttrOp.Contains => actual.Contains(af.Value, StringComparison.OrdinalIgnoreCase),
            AttrOp.StartsWith => actual.StartsWith(af.Value, StringComparison.OrdinalIgnoreCase),
            AttrOp.EndsWith => actual.EndsWith(af.Value, StringComparison.OrdinalIgnoreCase),
            _ => false,
        };
    }

    private static object? ResolveAttributePath(Visual visual, string path)
    {
        // Special shorthand handled by short-circuits below
        if (path.Equals("Name", StringComparison.OrdinalIgnoreCase) && visual is Control ctrl)
            return ctrl.Name;
        if (path.Equals("AutomationId", StringComparison.OrdinalIgnoreCase) && visual is Control ctrl2)
            return AutomationProperties.GetAutomationId(ctrl2);
        if (path.Equals("Text", StringComparison.OrdinalIgnoreCase))
            return visual switch
            {
                TextBox tb => tb.Text,
                TextBlock tbl => tbl.Text,
                ContentControl cc => cc.Content as string,
                _ => null,
            };
        if (path.Equals("Role", StringComparison.OrdinalIgnoreCase))
            return GetRole(visual);

        return ResolvePath(visual, path);
    }

    private static object? ResolvePath(object? root, string path)
    {
        if (root is null) return null;
        if (string.IsNullOrEmpty(path)) return root;
        var current = root;
        foreach (var segment in path.Split('.'))
        {
            if (current is null) return null;
            var prop = current.GetType().GetProperty(segment,
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public
                | System.Reflection.BindingFlags.IgnoreCase);
            if (prop is null) return null;
            try { current = prop.GetValue(current); }
            catch { return null; }
        }
        return current;
    }

    private bool MatchesPredicate(Visual visual, DataContextPredicateFilter filter)
    {
        if (visual is not StyledElement se) return false;
        var dc = se.DataContext;
        if (dc is null) return false;
        if (_predicateEvaluator is null) return false;
        return _predicateEvaluator.Evaluate(filter.Expression, dc);
    }

    private static bool MatchesPseudo(Visual visual, PseudoFilter filter)
    {
        return filter.Name switch
        {
            "visible" => visual.IsVisible && IsEffectivelyVisible(visual),
            "hidden" => !visual.IsVisible || !IsEffectivelyVisible(visual),
            "enabled" => visual is not InputElement ie || ie.IsEnabled,
            "disabled" => visual is InputElement ie && !ie.IsEnabled,
            "focused" => visual is InputElement ie2 && ie2.IsKeyboardFocusWithin,
            "checked" => visual is ToggleButton tb && tb.IsChecked == true,
            "has-text" => HasText(visual, filter.Argument ?? ""),
            "role" => string.Equals(GetRole(visual), filter.Argument, StringComparison.OrdinalIgnoreCase),
            _ => false,
        };
    }

    private static bool IsEffectivelyVisible(Visual visual)
    {
        var cur = visual;
        while (cur is not null)
        {
            if (!cur.IsVisible) return false;
            cur = cur.GetVisualParent();
        }
        return true;
    }

    private static bool HasText(Visual visual, string needle)
    {
        var t = ExtractText(visual);
        return t is not null && t.Contains(needle, StringComparison.OrdinalIgnoreCase);
    }

    private static string? ExtractText(Visual visual) => visual switch
    {
        TextBox tb => tb.Text,
        TextBlock tbl => tbl.Text,
        HeaderedContentControl hcc => hcc.Header as string ?? hcc.Content as string,
        ContentControl cc => cc.Content as string ?? GetTextFromChildren(cc),
        _ => GetTextFromChildren(visual),
    };

    private static string? GetTextFromChildren(Visual visual)
    {
        var blocks = visual.GetVisualDescendants().OfType<TextBlock>()
            .Where(t => t.IsVisible && !string.IsNullOrWhiteSpace(t.Text))
            .Select(t => t.Text!)
            .Take(5)
            .ToList();
        return blocks.Count == 0 ? null : string.Join(" · ", blocks);
    }

    /// <summary>
    /// Type matching: exact (case-insensitive) on type name first; falls back to substring contains
    /// to allow shorthand like "Button" matching "ButtonSpinner". Also walks base types.
    /// </summary>
    private static bool TypeMatches(Visual visual, string typeName)
    {
        for (var t = visual.GetType(); t is not null && t != typeof(object); t = t.BaseType)
        {
            if (string.Equals(t.Name, typeName, StringComparison.OrdinalIgnoreCase))
                return true;
        }
        // shorthand contains
        return visual.GetType().Name.Contains(typeName, StringComparison.OrdinalIgnoreCase);
    }

    private static string GetRole(Visual visual) => visual switch
    {
        // Order matters: more specific types first (CheckBox/RadioButton derive from ToggleButton; ToggleButton from Button).
        CheckBox => "checkbox",
        RadioButton => "radio",
        ToggleButton => "togglebutton",
        Button => "button",
        TextBox => "textbox",
        ComboBox => "combobox",
        TabItem => "tab",
        ListBoxItem => "listitem",
        MenuItem => "menuitem",
        Slider => "slider",
        _ => visual.GetType().Name.ToLowerInvariant(),
    };
}

/// <summary>
/// Pluggable evaluator for <c>[dc:'expr']</c> predicates. Implementations evaluate a C# expression
/// against a DataContext object and return true/false. Returning false (instead of throwing)
/// is the contract for "no match".
/// </summary>
public interface IDataContextPredicateEvaluator
{
    bool Evaluate(string expression, object dataContext);
}
