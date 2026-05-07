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

internal static class PreviewNativeAssetResolverSource
{
    internal const string ResourceName = "Zafiro.Avalonia.Mcp.Tool.Preview.PreviewNativeAssetResolver.cs";

    private static readonly Lazy<string> Source = new(LoadSource);

    public static string Code => Source.Value;

    private static string LoadSource()
    {
        var assembly = typeof(PreviewNativeAssetResolverSource).Assembly;
        using var stream = assembly.GetManifestResourceStream(ResourceName)
            ?? throw new InvalidOperationException($"Embedded preview native asset resolver source '{ResourceName}' was not found.");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
