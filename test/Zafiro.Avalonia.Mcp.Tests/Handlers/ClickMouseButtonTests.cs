using System.Text.Json;
using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Themes.Fluent;
using Avalonia.Threading;
using Xunit;
using Zafiro.Avalonia.Mcp.AppHost.Handlers;
using Zafiro.Avalonia.Mcp.Protocol;
using Zafiro.Avalonia.Mcp.Protocol.Messages;

namespace Zafiro.Avalonia.Mcp.Tests.Handlers;

[Collection("Avalonia")]
public class ClickMouseButtonTests
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    public ClickMouseButtonTests(AvaloniaTestFixture _)
    {
        NodeRegistry.Clear();
    }

    [Fact]
    public void RightClick_OpensTheElementContextMenu()
    {
        var (window, target, menu) = Dispatcher.UIThread.Invoke(() =>
        {
            var menu = new ContextMenu { ItemsSource = new[] { new MenuItem { Header = "Inspect" } } };
            var target = new Border { Name = "Preview", Width = 80, Height = 40, Background = Brushes.Gray, ContextMenu = menu };
            var window = new Window { Width = 200, Height = 100 };
            window.Styles.Add(new FluentTheme());
            window.Content = target;
            window.Show();
            return (window, target, menu);
        });

        try
        {
            var result = Dispatcher.UIThread.Invoke(() => InputHandler.Click(target, MouseButton.Right));

            var json = JsonSerializer.SerializeToElement(result, JsonOptions);
            Assert.True(Dispatcher.UIThread.Invoke(() => menu.IsOpen));
            Assert.True(json.GetProperty("success").GetBoolean());
            Assert.Equal("right", json.GetProperty("button").GetString());
            Assert.True(json.GetProperty("handled").GetBoolean());
            Assert.True(json.GetProperty("contextMenuOpen").GetBoolean());
        }
        finally
        {
            Dispatcher.UIThread.Invoke(() =>
            {
                menu.Close();
                window.Close();
            });
        }
    }

    [Fact]
    public void RightClick_RaisesRightButtonPointerEvents() => Dispatcher.UIThread.Invoke(() =>
    {
        var target = new Border { Width = 80, Height = 40 };
        var pressedWithRight = false;
        MouseButton? releasedButton = null;
        target.AddHandler(InputElement.PointerPressedEvent, (_, e) => pressedWithRight = e.GetCurrentPoint(target).Properties.IsRightButtonPressed);
        target.AddHandler(InputElement.PointerReleasedEvent, (_, e) => releasedButton = e.InitialPressMouseButton);

        InputHandler.Click(target, MouseButton.Right);

        Assert.True(pressedWithRight);
        Assert.Equal(MouseButton.Right, releasedButton);
    });

    [Fact]
    public void MiddleClick_OnButton_RaisesPointerEventsInsteadOfInvokingItsCommand() => Dispatcher.UIThread.Invoke(() =>
    {
        var command = new RecordingCommand();
        var button = new Button { Command = command, Width = 80, Height = 30 };
        var pressedWithMiddle = false;
        MouseButton? releasedButton = null;
        button.AddHandler(InputElement.PointerPressedEvent, (_, e) =>
        {
            pressedWithMiddle = e.GetCurrentPoint(button).Properties.IsMiddleButtonPressed;
            e.Handled = true;
        }, handledEventsToo: true);
        button.AddHandler(InputElement.PointerReleasedEvent, (_, e) => releasedButton = e.InitialPressMouseButton, handledEventsToo: true);

        var result = InputHandler.Click(button, MouseButton.Middle);

        var json = JsonSerializer.SerializeToElement(result, JsonOptions);
        Assert.False(command.WasExecuted);
        Assert.True(pressedWithMiddle);
        Assert.Equal(MouseButton.Middle, releasedButton);
        Assert.Equal("middle", json.GetProperty("button").GetString());
        Assert.Equal("pointer", json.GetProperty("method").GetString());
        Assert.True(json.GetProperty("handled").GetBoolean());
    });

    [Fact]
    public void MiddleClick_NobodyHandlesIt_ReportsAWarning() => Dispatcher.UIThread.Invoke(() =>
    {
        var target = new Border { Width = 80, Height = 40 };

        var result = InputHandler.Click(target, MouseButton.Middle);

        var json = JsonSerializer.SerializeToElement(result, JsonOptions);
        Assert.True(json.GetProperty("success").GetBoolean());
        Assert.False(json.GetProperty("handled").GetBoolean());
        Assert.False(string.IsNullOrWhiteSpace(json.GetProperty("warning").GetString()));
    });

    [Fact]
    public void RightClick_DisabledElement_ReturnsStructuredFailureWithoutRaisingEvents() => Dispatcher.UIThread.Invoke(() =>
    {
        var target = new Border { Width = 80, Height = 40, IsEnabled = false };
        var pressed = false;
        target.AddHandler(InputElement.PointerPressedEvent, (_, _) => pressed = true, handledEventsToo: true);

        var result = InputHandler.Click(target, MouseButton.Right);

        var failure = Assert.IsType<HandlerErrorResult>(result);
        Assert.Equal(DiagnosticErrorCodes.UnsupportedOperation, failure.Error.Code);
        Assert.False(pressed);
    });

    [Fact]
    public void Click_UnknownButton_ReturnsInvalidParam()
    {
        var result = new InputHandler().Handle(new DiagnosticRequest
        {
            Id = "test",
            Method = ProtocolMethods.Click,
            Params = JsonSerializer.SerializeToElement(new { selector = "#Anything", button = "side" })
        }).WaitAsync(TimeSpan.FromSeconds(5)).GetAwaiter().GetResult();

        var failure = Assert.IsType<HandlerErrorResult>(result);
        Assert.Equal(DiagnosticErrorCodes.InvalidParam, failure.Error.Code);
    }

    private sealed class RecordingCommand : ICommand
    {
        public bool WasExecuted { get; private set; }

        public event EventHandler? CanExecuteChanged { add { } remove { } }

        public bool CanExecute(object? parameter) => true;

        public void Execute(object? parameter) => WasExecuted = true;
    }
}
