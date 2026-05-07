using Xunit;
using Zafiro.Avalonia.Mcp.Tool.Preview;

namespace Zafiro.Avalonia.Mcp.Tests.Preview;

public sealed class PreviewHostSourceTests
{
    [Fact]
    public void GeneratedHostSource_IsLoadedFromEmbeddedTemplate()
    {
        Assert.Contains(PreviewHostSource.ResourceName, typeof(PreviewHostSource).Assembly.GetManifestResourceNames());
    }

    [Fact]
    public void GeneratedHostSource_MatchesEmbeddedTemplateContent()
    {
        using var stream = typeof(PreviewHostSource).Assembly.GetManifestResourceStream(PreviewHostSource.ResourceName);
        Assert.NotNull(stream);

        using var reader = new StreamReader(stream);
        Assert.Equal(reader.ReadToEnd(), PreviewHostSource.Code);
    }
}

public sealed class PreviewDesignData
{
    public string? Title { get; set; }
}
