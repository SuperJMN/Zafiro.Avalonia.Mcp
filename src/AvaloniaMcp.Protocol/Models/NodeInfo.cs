using System.Text.Json.Serialization;

namespace AvaloniaMcp.Protocol.Models;

public sealed class NodeInfo
{
    [JsonPropertyName("nodeId")]
    public int NodeId { get; init; }

    [JsonPropertyName("type")]
    public required string Type { get; init; }

    [JsonPropertyName("name")]
    public string? Name { get; init; }

    [JsonPropertyName("bounds")]
    public BoundsInfo? Bounds { get; init; }

    [JsonPropertyName("isVisible")]
    public bool IsVisible { get; init; } = true;

    [JsonPropertyName("text")]
    public string? Text { get; init; }

    [JsonPropertyName("isEnabled")]
    public bool? IsEnabled { get; init; }

    [JsonPropertyName("isFocused")]
    public bool? IsFocused { get; init; }

    [JsonPropertyName("isInteractive")]
    public bool? IsInteractive { get; init; }

    [JsonPropertyName("automationId")]
    public string? AutomationId { get; init; }

    [JsonPropertyName("role")]
    public string? Role { get; init; }

    [JsonPropertyName("className")]
    public string? ClassName { get; init; }

    [JsonPropertyName("parentId")]
    public int? ParentId { get; init; }

    [JsonPropertyName("children")]
    public List<NodeInfo>? Children { get; init; }
}

public sealed class BoundsInfo
{
    [JsonPropertyName("x")]
    public double X { get; init; }

    [JsonPropertyName("y")]
    public double Y { get; init; }

    [JsonPropertyName("width")]
    public double Width { get; init; }

    [JsonPropertyName("height")]
    public double Height { get; init; }
}
