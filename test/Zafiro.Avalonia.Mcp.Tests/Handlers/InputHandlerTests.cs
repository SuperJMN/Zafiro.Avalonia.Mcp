using System.Text.Json;
using System.Windows.Input;
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
    public void Click_MenuItemCommand_ClosesOwningMenu()
    {
        var command = new RecordingCommand();
        var menu = new TestMenu();
        var menuItem = new MenuItem
        {
            Header = "Delete From Internal Storage",
            Command = command
        };
        menu.Items.Add(menuItem);
        menu.Open();

        var result = InputHandler.Click(menuItem);

        Assert.True(command.WasExecuted);
        Assert.True(menu.WasClosed);
        Assert.False(menu.IsOpen);

        var json = JsonSerializer.SerializeToElement(result, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

        Assert.True(json.GetProperty("success").GetBoolean());
        Assert.Equal("menu_command", json.GetProperty("method").GetString());
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

    private sealed class RecordingCommand : ICommand
    {
        public bool WasExecuted { get; private set; }

        public event EventHandler? CanExecuteChanged
        {
            add { }
            remove { }
        }

        public bool CanExecute(object? parameter) => true;

        public void Execute(object? parameter) => WasExecuted = true;
    }

    private sealed class TestMenu : MenuBase
    {
        public bool WasClosed { get; private set; }

        public override void Close()
        {
            WasClosed = true;
            IsOpen = false;
        }

        public override void Open()
        {
            WasClosed = false;
            IsOpen = true;
        }
    }
}
