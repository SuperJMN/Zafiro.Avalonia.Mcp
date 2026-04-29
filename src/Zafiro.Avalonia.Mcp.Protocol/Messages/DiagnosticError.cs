using System.Text.Json.Serialization;

namespace Zafiro.Avalonia.Mcp.Protocol.Messages;

/// <summary>
/// Structured error payload returned alongside <see cref="DiagnosticResponse.Error"/>.
/// </summary>
/// <param name="Message">Human readable error text. Mirrored into the legacy <c>error</c> field for back-compat.</param>
/// <param name="Code">Stable machine-readable code from <see cref="DiagnosticErrorCodes"/>.</param>
/// <param name="Suggested">Optional recovery hint such as "call get_snapshot to refresh node IDs".</param>
/// <param name="Details">Optional structured context (e.g. <c>{ selector = "...", count = 3 }</c>).</param>
public sealed record DiagnosticError(
    [property: JsonPropertyName("message")] string Message,
    [property: JsonPropertyName("code")] string Code,
    [property: JsonPropertyName("suggested")] string? Suggested = null,
    [property: JsonPropertyName("details")] object? Details = null);
