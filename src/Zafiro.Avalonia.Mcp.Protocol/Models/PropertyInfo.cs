using System.Text.Json.Serialization;

namespace Zafiro.Avalonia.Mcp.Protocol.Models;

public sealed class PropertyInfo
{
    [JsonPropertyName("name")]
    public required string Name { get; init; }

    [JsonPropertyName("type")]
    public required string Type { get; init; }

    [JsonPropertyName("value")]
    public string? Value { get; init; }

    [JsonPropertyName("priority")]
    public string? Priority { get; init; }
}
