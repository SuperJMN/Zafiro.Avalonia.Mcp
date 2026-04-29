using System.Text.Json.Serialization;

namespace Zafiro.Avalonia.Mcp.Protocol.Models;

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

    /// <summary>
    /// Transport kind. "pipe" for Named Pipes (default, desktop), "tcp" for TCP loopback (Android/remote).
    /// Optional for back-compat with v2.0 hosts that only published <see cref="PipeName"/>.
    /// </summary>
    [JsonPropertyName("transport")]
    public string? Transport { get; init; }

    /// <summary>
    /// Endpoint string. For "pipe" this matches <see cref="PipeName"/>; for "tcp" it is "host:port" (e.g. "127.0.0.1:54123").
    /// Optional for back-compat.
    /// </summary>
    [JsonPropertyName("endpoint")]
    public string? Endpoint { get; init; }

    /// <summary>
    /// Android package id (e.g. "com.example.app"). Set only when running on Android; helps the tool side
    /// build the correct <c>adb shell run-as</c> / <c>adb forward</c> commands.
    /// </summary>
    [JsonPropertyName("packageId")]
    public string? PackageId { get; init; }
}
