using System.Reflection;

namespace Zafiro.Avalonia.Mcp.Tool.Preview;

internal static class PreviewHostSource
{
    internal const string ResourceName = "Zafiro.Avalonia.Mcp.Tool.Preview.PreviewHost.template.cs";

    private static readonly Lazy<string> Template = new(LoadTemplate);

    public static string Code => Template.Value;

    private static string LoadTemplate()
    {
        var assembly = typeof(PreviewHostSource).Assembly;
        using var stream = assembly.GetManifestResourceStream(ResourceName)
            ?? throw new InvalidOperationException($"Embedded preview host template '{ResourceName}' was not found.");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
