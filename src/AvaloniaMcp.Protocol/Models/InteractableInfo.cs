using System.Text.Json.Serialization;

namespace AvaloniaMcp.Protocol.Models;

public sealed class InteractableInfo
{
    [JsonPropertyName("nodeId")]
    public int NodeId { get; init; }

    [JsonPropertyName("type")]
    public required string Type { get; init; }

    [JsonPropertyName("role")]
    public required string Role { get; init; }

    [JsonPropertyName("text")]
    public string? Text { get; init; }

    [JsonPropertyName("name")]
    public string? Name { get; init; }

    [JsonPropertyName("automationId")]
    public string? AutomationId { get; init; }

    [JsonPropertyName("isEnabled")]
    public bool IsEnabled { get; init; }

    [JsonPropertyName("isFocused")]
    public bool IsFocused { get; init; }

    [JsonPropertyName("value")]
    public string? Value { get; init; }
}
