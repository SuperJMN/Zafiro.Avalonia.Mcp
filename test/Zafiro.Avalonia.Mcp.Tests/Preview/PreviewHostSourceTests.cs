using Xunit;
using Zafiro.Avalonia.Mcp.Tool.Preview;

namespace Zafiro.Avalonia.Mcp.Tests.Preview;

public sealed class PreviewHostSourceTests
{
    [Fact]
    public void GeneratedHostSource_PreservesDesignDataContextLoading()
    {
        Assert.Contains("designMode: true", PreviewHostSource.Code);
        Assert.Contains("Design.GetDataContext", PreviewHostSource.Code);
        Assert.Contains("control.DataContext = designDataContext", PreviewHostSource.Code);
    }

    [Fact]
    public void GeneratedHostSource_SeparatesAppAssemblyFromXamlAssembly()
    {
        Assert.Contains("options.XamlAssemblyPath", PreviewHostSource.Code);
        Assert.Contains("Load(options.AxamlPath, xamlAssembly)", PreviewHostSource.Code);
        Assert.Contains("\"xaml-assembly\"", PreviewHostSource.Code);
    }

    [Fact]
    public void GeneratedHostSource_FallsBackToCopiedNativeRuntimeAssets()
    {
        Assert.Contains("FindNativeLibraryInAppBase", PreviewHostSource.Code);
        Assert.Contains("runtimes", PreviewHostSource.Code);
    }
}

public sealed class PreviewDesignData
{
    public string? Title { get; set; }
}
