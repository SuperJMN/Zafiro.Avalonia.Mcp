using System.Text.Json;
using System.Text.Json.Serialization;

namespace Zafiro.Avalonia.Mcp.Protocol.Messages;

public sealed class DiagnosticResponse
{
    [JsonPropertyName("id")]
    public required string Id { get; init; }

    [JsonPropertyName("result")]
    public JsonElement? Result { get; init; }

    /// <summary>
    /// Legacy free-form error text. Always mirrors <see cref="ErrorInfo"/>.<see cref="DiagnosticError.Message"/>
    /// when <see cref="ErrorInfo"/> is set.
    /// </summary>
    [JsonPropertyName("error")]
    public string? Error { get; init; }

    /// <summary>
    /// Structured error payload with stable machine code and optional recovery hint.
    /// </summary>
    [JsonPropertyName("errorInfo")]
    public DiagnosticError? ErrorInfo { get; init; }

    public static DiagnosticResponse Success(string id, JsonElement result) =>
        new() { Id = id, Result = result };

    public static DiagnosticResponse Failure(string id, string error) =>
        new() { Id = id, Error = error };

    public static DiagnosticResponse Failure(string id, DiagnosticError error) =>
        new() { Id = id, Error = error.Message, ErrorInfo = error };
}
