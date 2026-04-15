using System.Text.Json;

namespace Zafiro.Avalonia.Mcp.Protocol;

public static class ProtocolSerializer
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
    };

    public static string Serialize<T>(T value) =>
        JsonSerializer.Serialize(value, Options);

    public static T? Deserialize<T>(string json) =>
        JsonSerializer.Deserialize<T>(json, Options);

    public static JsonElement ToElement<T>(T value) =>
        JsonSerializer.SerializeToElement(value, Options);
}
