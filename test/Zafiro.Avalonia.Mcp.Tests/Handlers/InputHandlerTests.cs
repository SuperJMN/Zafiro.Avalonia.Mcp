using System.Text.Json;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
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

    [Fact]
    public void Click_OpensButtonFlyout()
    {
        var flyout = new MenuFlyout();
        var button = new Button { Content = "Actions", Flyout = flyout };

        var result = InputHandler.Click(button);

        Assert.True(flyout.IsOpen);
        Assert.Equal(button, flyout.Target);

        var json = JsonSerializer.SerializeToElement(result, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

        Assert.True(json.GetProperty("success").GetBoolean());
        Assert.Equal("flyout", json.GetProperty("method").GetString());
    }

    [Fact]
    public void Click_Fallback_RaisesTypedPointerEvents()
    {
        var control = new Border
        {
            Width = 100,
            Height = 40,
            Focusable = true,
        };

        PointerPressedEventArgs? pressed = null;
        PointerReleasedEventArgs? released = null;
        control.AddHandler(InputElement.PointerPressedEvent, (_, e) => pressed = e);
        control.AddHandler(InputElement.PointerReleasedEvent, (_, e) => released = e);

        var result = InputHandler.Click(control);

        Assert.NotNull(pressed);
        Assert.NotNull(released);
        Assert.Equal(MouseButton.Left, released.InitialPressMouseButton);

        var json = JsonSerializer.SerializeToElement(result, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

        Assert.True(json.GetProperty("success").GetBoolean());
        Assert.Equal("pointer_simulation", json.GetProperty("method").GetString());
    }
}
