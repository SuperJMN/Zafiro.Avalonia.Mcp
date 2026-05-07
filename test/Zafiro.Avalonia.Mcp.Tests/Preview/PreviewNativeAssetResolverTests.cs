using Xunit;
using Zafiro.Avalonia.Mcp.Tool.Preview;

namespace Zafiro.Avalonia.Mcp.Tests.Preview;

public sealed class PreviewNativeAssetResolverTests
{
    [Fact]
    public void FindNativeLibraryInAppBase_PrefersExactLinuxRid()
    {
        using var temp = new RuntimeAssetsDirectory();
        var arm = temp.WriteNativeAsset("linux-arm64", "libSkiaSharp.so");
        var x64 = temp.WriteNativeAsset("linux-x64", "libSkiaSharp.so");

        var resolved = PreviewNativeAssetResolver.FindNativeLibraryInAppBase("SkiaSharp", temp.Root, "linux-x64");

        Assert.Equal(x64, resolved);
        Assert.NotEqual(arm, resolved);
    }

    [Fact]
    public void FindNativeLibraryInAppBase_UsesNonLinuxLibraryNamingPattern()
    {
        using var temp = new RuntimeAssetsDirectory();
        var x64 = temp.WriteNativeAsset("win-x64", "SkiaSharp.dll");
        temp.WriteNativeAsset("win-arm64", "SkiaSharp.dll");

        var resolved = PreviewNativeAssetResolver.FindNativeLibraryInAppBase("SkiaSharp", temp.Root, "win-x64");

        Assert.Equal(x64, resolved);
    }

    [Fact]
    public void FindNativeLibraryInAppBase_FallsBackDeterministicallyToCompatibleRid()
    {
        using var temp = new RuntimeAssetsDirectory();
        temp.WriteNativeAsset("linux-arm64", "libSkiaSharp.so");
        var sameArchitectureFallback = temp.WriteNativeAsset("linux-x64", "libSkiaSharp.so");

        var resolved = PreviewNativeAssetResolver.FindNativeLibraryInAppBase("SkiaSharp", temp.Root, "linux-musl-x64");

        Assert.Equal(sameArchitectureFallback, resolved);
    }

    [Fact]
    public void FindNativeLibraryInAppBase_UsesDeterministicOsFallbackWhenExactRidIsMissing()
    {
        using var temp = new RuntimeAssetsDirectory();
        var fallback = temp.WriteNativeAsset("osx-x64", "libSkiaSharp.dylib");

        var resolved = PreviewNativeAssetResolver.FindNativeLibraryInAppBase("SkiaSharp", temp.Root, "osx-arm64");

        Assert.Equal(fallback, resolved);
    }

    private sealed class RuntimeAssetsDirectory : IDisposable
    {
        public RuntimeAssetsDirectory()
        {
            Root = Path.Combine(Path.GetTempPath(), "avalonia-mcp-native-assets", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Root);
        }

        public string Root { get; }

        public string WriteNativeAsset(string rid, string fileName)
        {
            var directory = Path.Combine(Root, "runtimes", rid, "native");
            Directory.CreateDirectory(directory);
            var path = Path.Combine(directory, fileName);
            File.WriteAllText(path, string.Empty);
            return path;
        }

        public void Dispose()
        {
            try
            {
                Directory.Delete(Root, recursive: true);
            }
            catch
            {
            }
        }
    }
}
