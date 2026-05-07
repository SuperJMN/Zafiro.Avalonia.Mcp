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
    string? ProjectPath,
    string? EntryType,
    string? TargetFramework,
    string Configuration);

internal sealed record PreviewHostOptions(
    string AxamlPath,
    string AssemblyPath,
    string? EntryType,
    int Width,
    int Height);

internal sealed class PreviewValidationException : Exception
{
    public PreviewValidationException(string code, string message, string? suggested = null) : base(message)
    {
        Code = code;
        Suggested = suggested;
    }

    public string Code { get; }
    public string? Suggested { get; }
}
