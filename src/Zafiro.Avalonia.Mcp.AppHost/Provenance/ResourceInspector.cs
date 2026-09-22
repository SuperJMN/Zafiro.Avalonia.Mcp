using Avalonia;
using Avalonia.Controls;
using Avalonia.Styling;
using Zafiro.Avalonia.Mcp.Protocol.Models;

namespace Zafiro.Avalonia.Mcp.AppHost.Provenance;

internal sealed class ResourceInspector
{
    private readonly InspectionContext _context;
    private readonly object _key;
    private readonly ThemeVariant? _theme;
    private readonly HashSet<object> _path = new(ReferenceEqualityComparer.Instance);
    private int _remaining = InspectionContext.MaxItems;
    private bool _unknown;

    private ResourceInspector(InspectionContext context, object key, ThemeVariant? theme)
    {
        _context = context;
        _key = key;
        _theme = theme;
    }

    public static ResourceOrigin Find(object? host, object? key, object? theme, InspectionContext parent, int depth)
    {
        var context = new InspectionContext(parent.MaxDepth);
        RuntimeSource? dictionary = null;
        RuntimeSource? scope = null;
        if (key is not (string or Type))
            context.Unavailable("resourceDictionary", "Only retained string and type resource keys can be traced safely.");
        else if (host is not IResourceHost resourceHost)
            context.Unavailable("resourceDictionary", "The binding has not retained an active resource lookup host.");
        else
        {
            var inspector = new ResourceInspector(context, key, theme as ThemeVariant);
            var visited = new HashSet<object>(ReferenceEqualityComparer.Instance);
            for (var current = resourceHost; current is not null;)
            {
                if (depth++ >= context.MaxDepth)
                {
                    context.Truncate("resourceDepth");
                    break;
                }
                if (!visited.Add(current))
                {
                    context.Unavailable("resourceDictionary", "A resource lookup cycle was detected.");
                    break;
                }

                if (inspector.Search(current, depth) is { } found)
                {
                    dictionary = ProvenanceIdentity.Source(found);
                    if (found is IResourceProvider { Owner: { } owner })
                        scope = ProvenanceIdentity.Source(owner);
                    break;
                }
                if (inspector._unknown)
                    break;
                current = (current as IStyleHost)?.StylingParent as IResourceHost;
            }
            if (dictionary is null)
                context.Unavailable("resourceDictionary", "No supplying dictionary could be established from retained, loaded resources.");
        }

        context.Unavailable("resourceAliases", "Resolved static-resource aliases and their original keys are not retained.");
        foreach (var unavailable in context.Resolution().Unavailable)
            parent.Unavailable(unavailable.Evidence, unavailable.Reason);
        if (context.Truncated)
            parent.Truncate("resourceLookup");
        return new ResourceOrigin(dictionary, host is null ? null : ProvenanceIdentity.Source(host), scope,
            ProvenanceIdentity.Value((theme as ThemeVariant)?.Key), context.Resolution());
    }

    private object? Search(object node, int depth)
    {
        if (depth >= _context.MaxDepth || _remaining-- <= 0)
        {
            _context.Truncate("resourceLookup");
            _unknown = true;
            return null;
        }
        if (!_path.Add(node))
        {
            _context.Unavailable("resourceDictionary", "A resource dictionary cycle was detected.");
            _unknown = true;
            return null;
        }

        try
        {
            if (node is ResourceDictionary dictionary)
                return SearchDictionary(dictionary, depth);

            if (node is StyledElement or Application or Styles or StyleBase)
            {
                if (AvaloniaDiagnosticsAdapter.OwnResources(node) is { } resources)
                {
                    var found = Search(resources, depth + 1);
                    if (found is not null || _unknown)
                        return found;
                }

                if (node is Styles styles)
                {
                    for (var i = styles.Count - 1; i >= 0; i--)
                    {
                        var found = Search(styles[i], depth + 1);
                        if (found is not null || _unknown)
                            return found;
                    }
                }
                else if (node is StyleBase style)
                {
                    foreach (var child in ((IStyle)style).Children)
                    {
                        var found = Search(child, depth + 1);
                        if (found is not null || _unknown)
                            return found;
                    }
                }
                else if (AvaloniaDiagnosticsAdapter.OwnStyles(node) is { } localStyles)
                    return Search(localStyles, depth + 1);
                return null;
            }

            _unknown = true;
            _context.Unavailable("resourceDictionary",
                $"The resource provider {node.GetType().FullName} cannot be inspected without invoking application code or loading resources.");
            return null;
        }
        finally
        {
            _path.Remove(node);
        }
    }

    private object? SearchDictionary(ResourceDictionary dictionary, int depth)
    {
        if (dictionary.ContainsKey(_key))
            return dictionary;

        if (AvaloniaDiagnosticsAdapter.ThemeDictionaries(dictionary) is { } variants)
        {
            var seen = new HashSet<ThemeVariant>();
            for (var variant = _theme; variant is not null && variant != ThemeVariant.Default; variant = variant.InheritVariant)
            {
                if (!seen.Add(variant) || seen.Count > _context.MaxDepth)
                {
                    _context.Truncate("themeVariantChain");
                    _unknown = true;
                    return null;
                }
                if (variants.Contains(variant))
                {
                    var found = Search(variants[variant]!, depth + 1);
                    if (found is not null || _unknown)
                        return found;
                }
            }
            if (variants.Contains(ThemeVariant.Default))
            {
                var found = Search(variants[ThemeVariant.Default]!, depth + 1);
                if (found is not null || _unknown)
                    return found;
            }
        }

        if (AvaloniaDiagnosticsAdapter.MergedDictionaries(dictionary) is { } merged)
        {
            for (var i = merged.Count - 1; i >= 0; i--)
            {
                var found = Search(merged[i]!, depth + 1);
                if (found is not null || _unknown)
                    return found;
            }
        }
        return null;
    }
}
