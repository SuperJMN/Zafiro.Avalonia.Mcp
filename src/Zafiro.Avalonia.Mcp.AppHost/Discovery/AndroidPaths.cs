namespace Zafiro.Avalonia.Mcp.AppHost.Discovery;

/// <summary>
/// Resolves Android-specific paths and identifiers via reflection so the AppHost can keep its
/// non-Android target frameworks (net8.0, net10.0) free of <c>Mono.Android</c> references.
/// On desktop targets these properties simply return null.
/// </summary>
internal static class AndroidPaths
{
    private static readonly Lazy<(string? cacheDir, string? packageId)> Cached = new(Resolve);

    public static string? ExternalCacheDir => Cached.Value.cacheDir;
    public static string? PackageId => Cached.Value.packageId;

    private static (string?, string?) Resolve()
    {
        try
        {
            var appType = Type.GetType("Android.App.Application, Mono.Android", throwOnError: false);
            if (appType is null) return (null, null);

            var contextProp = appType.GetProperty("Context");
            var context = contextProp?.GetValue(null);
            if (context is null) return (null, null);

            var contextType = context.GetType();
            var externalCacheDir = contextType.GetProperty("ExternalCacheDir")?.GetValue(context);
            var packageName = contextType.GetProperty("PackageName")?.GetValue(context) as string;

            string? cachePath = null;
            if (externalCacheDir is not null)
            {
                cachePath = externalCacheDir.GetType().GetProperty("AbsolutePath")?.GetValue(externalCacheDir) as string;
            }
            return (cachePath, packageName);
        }
        catch
        {
            return (null, null);
        }
    }
}
