using System.Text.Json;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Platform;
using Avalonia.Threading;
using Zafiro.Avalonia.Mcp.Protocol;
using Zafiro.Avalonia.Mcp.Protocol.Messages;

namespace Zafiro.Avalonia.Mcp.AppHost.Handlers;

public sealed class ResourceHandler : IRequestHandler
{
    public string Method => ProtocolMethods.GetResources;

    public async Task<object> Handle(DiagnosticRequest request)
    {
        int? nodeId = null;
        var onlySelf = false;

        if (request.Params is JsonElement p)
        {
            if (p.TryGetProperty("nodeId", out var nid)) nodeId = nid.GetInt32();
            if (p.TryGetProperty("onlySelf", out var os)) onlySelf = os.GetBoolean();
        }

        return await Dispatcher.UIThread.InvokeAsync<object>(() =>
        {
            IResourceDictionary? resources;

            if (nodeId.HasValue)
            {
                var visual = NodeRegistry.Resolve(nodeId.Value);
                if (visual is not StyledElement styled) return new { error = $"Node {nodeId} not found" };
                resources = styled.Resources;
            }
            else
            {
                resources = Application.Current?.Resources;
            }

            if (resources is null) return new { error = "No resources found" };

            var result = new List<object>();
            CollectResources(resources, result, onlySelf);
            return result;
        });
    }

    private static void CollectResources(IResourceDictionary dict, List<object> results, bool onlySelf)
    {
        foreach (var kvp in dict)
        {
            results.Add(new
            {
                key = kvp.Key.ToString(),
                type = kvp.Value?.GetType().Name ?? "null",
                value = kvp.Value?.ToString()
            });
        }

        if (!onlySelf)
        {
            foreach (var merged in dict.MergedDictionaries)
            {
                if (merged is IResourceDictionary mergedDict)
                    CollectResources(mergedDict, results, false);
            }
        }
    }
}

public sealed class ListAssetsHandler : IRequestHandler
{
    public string Method => ProtocolMethods.ListAssets;

    public Task<object> Handle(DiagnosticRequest request)
    {
        var assets = new List<string>();
        var assemblies = AppDomain.CurrentDomain.GetAssemblies();

        foreach (var assembly in assemblies)
        {
            try
            {
                var names = assembly.GetManifestResourceNames();
                foreach (var name in names)
                {
                    if (name.EndsWith(".axaml", StringComparison.OrdinalIgnoreCase) ||
                        name.EndsWith(".xaml", StringComparison.OrdinalIgnoreCase) ||
                        name.EndsWith(".png", StringComparison.OrdinalIgnoreCase) ||
                        name.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase) ||
                        name.EndsWith(".svg", StringComparison.OrdinalIgnoreCase) ||
                        name.EndsWith(".ttf", StringComparison.OrdinalIgnoreCase) ||
                        name.EndsWith(".otf", StringComparison.OrdinalIgnoreCase))
                    {
                        var asmName = assembly.GetName().Name;
                        assets.Add($"avares://{asmName}/{name.Replace('.', '/')}");
                    }
                }
            }
            catch { }
        }

        return Task.FromResult<object>(new { assets });
    }
}

public sealed class OpenAssetHandler : IRequestHandler
{
    public string Method => ProtocolMethods.OpenAsset;

    public Task<object> Handle(DiagnosticRequest request)
    {
        string? assetUrl = null;
        if (request.Params is JsonElement p && p.TryGetProperty("assetUrl", out var u))
            assetUrl = u.GetString();

        if (assetUrl is null) return Task.FromResult<object>(new { error = "No assetUrl provided" });

        try
        {
            var uri = new Uri(assetUrl);
            var assetLoader = AssetLoader.Open(uri);
            using var ms = new MemoryStream();
            assetLoader.CopyTo(ms);
            var base64 = Convert.ToBase64String(ms.ToArray());

            var mimeType = assetUrl switch
            {
                _ when assetUrl.EndsWith(".png") => "image/png",
                _ when assetUrl.EndsWith(".jpg") => "image/jpeg",
                _ when assetUrl.EndsWith(".svg") => "image/svg+xml",
                _ when assetUrl.EndsWith(".axaml") => "application/xml",
                _ when assetUrl.EndsWith(".xaml") => "application/xml",
                _ => "application/octet-stream"
            };

            return Task.FromResult<object>(new { data = base64, mimeType, sizeBytes = ms.Length });
        }
        catch (Exception ex)
        {
            return Task.FromResult<object>(new { error = $"Failed to open asset: {ex.Message}" });
        }
    }
}
