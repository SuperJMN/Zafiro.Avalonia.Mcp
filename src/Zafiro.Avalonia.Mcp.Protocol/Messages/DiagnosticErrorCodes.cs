namespace Zafiro.Avalonia.Mcp.Protocol.Messages;

/// <summary>
/// Stable machine-readable codes for <see cref="DiagnosticError.Code"/>.
/// New codes may be added; existing codes must not change spelling.
/// </summary>
public static class DiagnosticErrorCodes
{
    public const string NoMatch = "NO_MATCH";
    public const string AmbiguousSelector = "AMBIGUOUS_SELECTOR";
    public const string StaleNode = "STALE_NODE";
    public const string InvalidParam = "INVALID_PARAM";
    public const string InvalidSelector = "INVALID_SELECTOR";
    public const string UnsupportedOperation = "UNSUPPORTED_OPERATION";
    public const string Timeout = "TIMEOUT";
    public const string Internal = "INTERNAL";
}
