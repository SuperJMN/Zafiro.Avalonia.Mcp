using Zafiro.Avalonia.Mcp.Protocol.Messages;

namespace Zafiro.Avalonia.Mcp.AppHost.Handlers;

/// <summary>
/// Sentinel returned by handlers to signal a structured error to <see cref="RequestDispatcher"/>.
/// The dispatcher unwraps it into a <see cref="DiagnosticResponse.ErrorInfo"/> payload instead of
/// serialising the object as a successful result.
/// </summary>
public sealed class HandlerErrorResult
{
    public DiagnosticError Error { get; }

    public HandlerErrorResult(DiagnosticError error)
    {
        Error = error;
    }
}

/// <summary>
/// Helpers for building handler error results with the standard <see cref="DiagnosticErrorCodes"/>.
/// </summary>
public static class HandlerResult
{
    public static HandlerErrorResult Error(string code, string message, string? suggested = null, object? details = null)
        => new(new DiagnosticError(message, code, suggested, details));

    public static HandlerErrorResult StaleNode(int nodeId, string? extra = null)
    {
        var msg = extra is null
            ? $"Node {nodeId} not found (may have been garbage collected or detached from the visual tree)."
            : $"Node {nodeId}: {extra}";
        return Error(
            DiagnosticErrorCodes.StaleNode,
            msg,
            "Call get_snapshot, search, or get_interactables to refresh node IDs.",
            new { nodeId });
    }

    public static HandlerErrorResult InvalidParam(string paramName, string message)
        => Error(
            DiagnosticErrorCodes.InvalidParam,
            message,
            $"Provide a valid value for '{paramName}'.",
            new { param = paramName });

    public static HandlerErrorResult Unsupported(string operation, string elementType)
        => Error(
            DiagnosticErrorCodes.UnsupportedOperation,
            $"Operation '{operation}' is not supported on element type '{elementType}'.",
            null,
            new { operation, elementType });
}
