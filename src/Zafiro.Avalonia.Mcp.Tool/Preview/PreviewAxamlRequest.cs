using System.Text.Json.Serialization;

namespace Zafiro.Avalonia.Mcp.Tool.Preview;

internal sealed record PreviewAxamlRequest(
    string AxamlPath,
    string? ProjectPath = null,
    string? AssemblyPath = null,
    string? EntryType = null,
    string? TargetFramework = null,
    string Configuration = "Debug",
    int Width = 1024,
    int Height = 768,
    bool Build = true);

internal sealed record PreviewTarget(
    string AxamlPath,
    string AssemblyPath,
    string XamlAssemblyPath,
    string? ProjectPath,
    string? EntryType,
    string? TargetFramework,
    string Configuration);

internal sealed class PreviewValidationException : Exception
{
    public PreviewValidationException(string code, string message, string? suggested = null, object? details = null) : base(message)
    {
        Code = code;
        Suggested = suggested;
        Details = details;
    }

    public string Code { get; }
    public string? Suggested { get; }
    public object? Details { get; }
}

internal sealed record PreviewHostExitDetails(
    [property: JsonPropertyName("exitCode")] int ExitCode,
    [property: JsonPropertyName("standardOutput")] string StandardOutput,
    [property: JsonPropertyName("standardError")] string StandardError,
    [property: JsonPropertyName("previewHostProjectPath")] string? PreviewHostProjectPath = null,
    [property: JsonPropertyName("connected")] bool Connected = false);
