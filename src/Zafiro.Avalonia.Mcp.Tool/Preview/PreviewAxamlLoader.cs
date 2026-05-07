using System.Reflection;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace Zafiro.Avalonia.Mcp.Tool.Preview;

internal static class PreviewAxamlLoader
{
    public static object Load(string axamlPath, Assembly assembly)
    {
        using var stream = File.OpenRead(axamlPath);
        var uri = new Uri(axamlPath, UriKind.Absolute);

        var root = AvaloniaRuntimeXamlLoader.Load(stream, assembly, rootInstance: null, uri, designMode: true)
                   ?? throw new InvalidOperationException($"AXAML loader returned null for '{axamlPath}'.");

        if (root is Control control && Design.GetDataContext(control) is { } designDataContext)
        {
            control.DataContext = designDataContext;
        }

        return root;
    }
}
