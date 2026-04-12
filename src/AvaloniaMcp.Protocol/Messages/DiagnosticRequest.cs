using System.Text.Json;
using System.Text.Json.Serialization;

namespace AvaloniaMcp.Protocol.Messages;

public sealed class DiagnosticRequest
{
    [JsonPropertyName("method")]
    public required string Method { get; init; }

    [JsonPropertyName("params")]
    public JsonElement? Params { get; init; }

    [JsonPropertyName("id")]
    public required string Id { get; init; }
}
