using System.Text.Json;
using System.Text.Json.Serialization;

namespace AvaloniaMcp.Protocol.Messages;

public sealed class DiagnosticResponse
{
    [JsonPropertyName("id")]
    public required string Id { get; init; }

    [JsonPropertyName("result")]
    public JsonElement? Result { get; init; }

    [JsonPropertyName("error")]
    public string? Error { get; init; }

    public static DiagnosticResponse Success(string id, JsonElement result) =>
        new() { Id = id, Result = result };

    public static DiagnosticResponse Failure(string id, string error) =>
        new() { Id = id, Error = error };
}
