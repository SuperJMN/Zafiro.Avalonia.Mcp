using Avalonia;
using Zafiro.Avalonia.Mcp.Protocol.Messages;

namespace Zafiro.Avalonia.Mcp.AppHost.Selectors;

/// <summary>
/// Shared helpers for selector-based MCP request handlers that operate on a single resolved element.
/// Returns standardised error payloads with stable <c>code</c> values so tooling can react programmatically.
/// </summary>
public static class SelectorRequestHelper
{
    /// <summary>
    /// Resolves a selector to a single visual on the calling thread (must be the UI thread).
    /// </summary>
    /// <param name="selector">The CSS-like selector string.</param>
    /// <param name="requireSingle">When <c>true</c> (default), more than one match yields an AMBIGUOUS_SELECTOR error.</param>
    /// <returns>Tuple with the resolved visual (or <c>null</c>) and an error payload (or <c>null</c>).</returns>
    public static (Visual? visual, object? error) ResolveSingle(string? selector, bool requireSingle = true)
    {
        if (string.IsNullOrWhiteSpace(selector))
            return (null, new { error = "selector is required", code = DiagnosticErrorCodes.MissingSelector });

        var matches = SelectorEngine.Default.Resolve(selector);
        if (matches.Count == 0)
            return (null, new { error = "no element matched selector", code = DiagnosticErrorCodes.NoMatch, selector });

        if (requireSingle && matches.Count > 1)
            return (null, new
            {
                error = $"Selector '{selector}' matched {matches.Count} elements.",
                code = DiagnosticErrorCodes.AmbiguousSelector,
                matchCount = matches.Count,
                selector
            });

        return (matches[0], null);
    }
}
