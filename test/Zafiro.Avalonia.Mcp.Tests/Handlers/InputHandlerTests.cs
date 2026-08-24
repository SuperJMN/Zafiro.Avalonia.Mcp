using System.Text.Json;
using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Input;
using Xunit;
using Zafiro.Avalonia.Mcp.AppHost.Handlers;
using Zafiro.Avalonia.Mcp.Protocol.Messages;

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
    public void Click_MenuItemCommand_EvaluatesCanExecuteOnceAndReportsObservedExecution()
    {
        var command = new ChangingCanExecuteCommand();
        var menuItem = new MenuItem { Header = "Run", Command = command };
        command.Reset();

        var result = InputHandler.Click(menuItem);

        Assert.Equal(1, command.CanExecuteCalls);
        Assert.True(command.WasExecuted);
        var json = JsonSerializer.SerializeToElement(result, JsonOptions);
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

        var failure = Assert.IsType<HandlerErrorResult>(result);
        Assert.Equal(DiagnosticErrorCodes.UnsupportedOperation, failure.Error.Code);
        Assert.Contains("could not be verified", failure.Error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Click_DisabledButton_ReturnsStructuredFailureWithoutRaisingClick()
    {
        var clicked = false;
        var button = new Button { IsEnabled = false };
        button.Click += (_, _) => clicked = true;

        var result = InputHandler.Click(button);

        var failure = Assert.IsType<HandlerErrorResult>(result);
        Assert.Equal(DiagnosticErrorCodes.UnsupportedOperation, failure.Error.Code);
        Assert.Contains("disabled", failure.Error.Message, StringComparison.OrdinalIgnoreCase);
        Assert.False(clicked);
    }

    [Fact]
    public void Click_CommandCannotExecute_ReturnsStructuredFailureWithoutExecuting()
    {
        var command = new RecordingCommand(canExecute: false);
        var button = new Button { Command = command };

        var result = InputHandler.Click(button);

        var failure = Assert.IsType<HandlerErrorResult>(result);
        Assert.Equal(DiagnosticErrorCodes.UnsupportedOperation, failure.Error.Code);
        Assert.Contains("disabled", failure.Error.Message, StringComparison.OrdinalIgnoreCase);
        Assert.False(command.WasExecuted);
    }

    [Fact]
    public void Click_ButtonCommand_EvaluatesCanExecuteOnceAndReusesTheDecision()
    {
        var command = new ChangingCanExecuteCommand();
        var button = new Button { Command = command };
        command.Reset();

        var result = InputHandler.Click(button);

        Assert.Equal(1, command.CanExecuteCalls);
        Assert.True(command.WasExecuted);
        var json = JsonSerializer.SerializeToElement(result, JsonOptions);
        Assert.Equal("command", json.GetProperty("method").GetString());
    }

    [Fact]
    public void Click_ButtonUnderDisabledAncestor_ReturnsStructuredFailureWithoutRaisingClick()
    {
        var clicked = false;
        var button = new Button();
        button.Click += (_, _) => clicked = true;
        var panel = new StackPanel { IsEnabled = false, Children = { button } };
        var window = new Window { Width = 100, Height = 60, Content = panel };
        window.ApplyTemplate();
        window.Measure(new Size(100, 60));
        window.Arrange(new Rect(0, 0, 100, 60));

        try
        {
            var result = InputHandler.Click(button);

            var failure = Assert.IsType<HandlerErrorResult>(result);
            Assert.Equal(DiagnosticErrorCodes.UnsupportedOperation, failure.Error.Code);
            Assert.False(clicked);
        }
        finally
        {
            window.Close();
        }
    }

    [Fact]
    public void Click_TextBlockInsideButton_InvokesInteractiveAncestor()
    {
        var text = new TextBlock { Text = "Save" };
        var clicked = false;
        var button = new Button
        {
            Template = new FuncControlTemplate<Button>((_, _) => text)
        };
        button.Click += (_, _) => clicked = true;
        button.ApplyTemplate();
        button.Measure(new Size(100, 40));
        button.Arrange(new Rect(0, 0, 100, 40));

        var result = InputHandler.Click(text);

        Assert.True(clicked);
        var json = JsonSerializer.SerializeToElement(result, JsonOptions);
        Assert.True(json.GetProperty("success").GetBoolean());
        Assert.Equal(NodeRegistry.GetOrRegister(button), json.GetProperty("nodeId").GetInt32());
        Assert.Equal("click_event", json.GetProperty("method").GetString());
    }

    [Fact]
    public void Click_StandaloneTextBlock_ReturnsClearStructuredFailure()
    {
        var result = InputHandler.Click(new TextBlock { Text = "Just text" });

        var failure = Assert.IsType<HandlerErrorResult>(result);
        Assert.Equal(DiagnosticErrorCodes.UnsupportedOperation, failure.Error.Code);
        Assert.Contains("interactive ancestor", failure.Error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Select_ReportsActualIndexAfterSynchronousSelectionEffects()
    {
        var list = new ListBox { ItemsSource = new[] { "First", "Second" } };
        list.SelectedIndex = 0;
        list.SelectionChanged += (_, _) =>
        {
            if (list.SelectedIndex == 1)
                list.SelectedIndex = 0;
        };

        var result = SelectionHandler.Select(list, index: 1, text: null);

        Assert.Equal(0, list.SelectedIndex);
        var json = JsonSerializer.SerializeToElement(result, JsonOptions);
        Assert.True(json.GetProperty("success").GetBoolean());
        Assert.Equal(0, json.GetProperty("selectedIndex").GetInt32());
    }

    [Fact]
    public void Click_ListBoxItemThatIsNotAContainer_DoesNotClaimSelection()
    {
        var item = new ListBoxItem { Content = "Detached container" };
        var list = new ListBox
        {
            Template = new FuncControlTemplate<ListBox>((_, _) => item)
        };
        list.ApplyTemplate();
        list.Measure(new Size(200, 120));
        list.Arrange(new Rect(0, 0, 200, 120));
        Assert.Equal(-1, list.IndexFromContainer(item));

        var result = InputHandler.Click(item);

        var failure = Assert.IsType<HandlerErrorResult>(result);
        Assert.Equal(DiagnosticErrorCodes.UnsupportedOperation, failure.Error.Code);
        Assert.Contains("container", failure.Error.Message, StringComparison.OrdinalIgnoreCase);
    }

    private sealed class RecordingCommand : ICommand
    {
        private readonly bool _canExecute;

        public RecordingCommand(bool canExecute = true)
        {
            _canExecute = canExecute;
        }

        public bool WasExecuted { get; private set; }

        public event EventHandler? CanExecuteChanged
        {
            add { }
            remove { }
        }

        public bool CanExecute(object? parameter) => _canExecute;

        public void Execute(object? parameter) => WasExecuted = true;
    }

    private sealed class ChangingCanExecuteCommand : ICommand
    {
        public int CanExecuteCalls { get; private set; }
        public bool WasExecuted { get; private set; }

        public bool CanExecute(object? parameter) => ++CanExecuteCalls == 1;
        public void Execute(object? parameter) => WasExecuted = true;
        public void Reset() => CanExecuteCalls = 0;

        public event EventHandler? CanExecuteChanged
        {
            add { }
            remove { }
        }
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

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
