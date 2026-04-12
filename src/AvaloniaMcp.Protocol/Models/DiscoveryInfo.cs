using System.Text.Json.Serialization;

namespace AvaloniaMcp.Protocol.Models;

public sealed class DiscoveryInfo
{
    [JsonPropertyName("pid")]
    public int Pid { get; init; }

    [JsonPropertyName("pipeName")]
    public required string PipeName { get; init; }

    [JsonPropertyName("processName")]
    public required string ProcessName { get; init; }

    [JsonPropertyName("startTime")]
    public DateTimeOffset StartTime { get; init; }

    [JsonPropertyName("protocolVersion")]
    public string ProtocolVersion { get; init; } = "1.0.0";
}
