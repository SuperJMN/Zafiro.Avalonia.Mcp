using Avalonia.Controls;
using Xunit;
using Zafiro.Avalonia.Mcp.Tool.Preview;

namespace Zafiro.Avalonia.Mcp.Tests.Preview;

[Collection("Avalonia")]
public sealed class PreviewAxamlLoaderTests
{
    public PreviewAxamlLoaderTests(AvaloniaTestFixture _)
    {
    }

    [Fact]
    public void Load_AppliesDesignDataContext()
    {
        var directory = Path.Combine(Path.GetTempPath(), "avalonia-mcp-preview-loader-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var axamlPath = Path.Combine(directory, "DesignView.axaml");

        try
        {
            File.WriteAllText(axamlPath, """
                <UserControl xmlns="https://github.com/avaloniaui"
                             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
                             xmlns:preview="clr-namespace:Zafiro.Avalonia.Mcp.Tests.Preview;assembly=Zafiro.Avalonia.Mcp.Tests">
                  <Design.DataContext>
                    <preview:PreviewDesignData Title="Design title" />
                  </Design.DataContext>
                  <TextBlock Name="TitleText" Text="{Binding Title}" />
                </UserControl>
                """);

            var root = PreviewAxamlLoader.Load(axamlPath, typeof(PreviewDesignData).Assembly);
            var control = Assert.IsType<UserControl>(root);
            var data = Assert.IsType<PreviewDesignData>(control.DataContext);

            Assert.Equal("Design title", data.Title);
        }
        finally
        {
            try
            {
                Directory.Delete(directory, recursive: true);
            }
            catch
            {
            }
        }
    }
}

public sealed class PreviewDesignData
{
    public string? Title { get; set; }
}
