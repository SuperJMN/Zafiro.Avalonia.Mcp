using System.Text.Json;
using Avalonia.Controls;
using Xunit;
using Zafiro.Avalonia.Mcp.AppHost.Handlers;

namespace Zafiro.Avalonia.Mcp.Tests.Handlers;

[Collection("Avalonia")]
public class InputHandlerTests
{
    public InputHandlerTests(AvaloniaTestFixture _)
    {
        NodeRegistry.Clear();
    }

    [Fact]
    public void Click_SelectsTreeViewItem()
    {
        var item = new TreeViewItem { Header = "Funded" };

        var result = InputHandler.Click(item);

        Assert.True(item.IsSelected);

        var json = JsonSerializer.SerializeToElement(result, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

        Assert.True(json.GetProperty("success").GetBoolean());
        Assert.Equal("treeview_select", json.GetProperty("method").GetString());
    }

    [Fact]
    public void Click_SelectsTreeViewOwnerItem()
    {
        const string funded = "funded";
        var item = new TreeViewItem { Header = "Funded", DataContext = funded };
        var treeView = new TreeView();
        treeView.Items.Add(item);

        var result = InputHandler.Click(item);

        Assert.True(item.IsSelected);
        Assert.Equal(funded, treeView.SelectedItem);

        var json = JsonSerializer.SerializeToElement(result, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

        Assert.True(json.GetProperty("success").GetBoolean());
        Assert.Equal("treeview_select", json.GetProperty("method").GetString());
        Assert.Equal(funded, json.GetProperty("selectedItem").GetString());
    }
}
