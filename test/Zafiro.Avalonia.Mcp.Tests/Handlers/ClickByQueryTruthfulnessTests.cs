using System.Text.Json;
using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;
using Xunit;
using Zafiro.Avalonia.Mcp.AppHost.Handlers;
using Zafiro.Avalonia.Mcp.Protocol;
using Zafiro.Avalonia.Mcp.Protocol.Messages;

namespace Zafiro.Avalonia.Mcp.Tests.Handlers;

[Collection("Avalonia")]
public class ClickByQueryTruthfulnessTests
{
    public ClickByQueryTruthfulnessTests(AvaloniaTestFixture _)
    {
        NodeRegistry.Clear();
    }

    [Fact]
    public void ClickByQuery_IsEnabledFalse_ReturnsStructuredDisabledFailure()
    {
        var result = HandleWithHosted(
            () => new Button { Name = "DisabledAction", IsEnabled = false }, "DisabledAction");

        var failure = Assert.IsType<HandlerErrorResult>(result);
        Assert.Equal(DiagnosticErrorCodes.UnsupportedOperation, failure.Error.Code);
        Assert.Contains("disabled", failure.Error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ClickByQuery_CommandCannotExecute_ReturnsStructuredDisabledFailure()
    {
        var result = HandleWithHosted(() => new Button
        {
            Name = "BlockedCommand",
            Command = new FixedCommand(canExecute: false)
        }, "BlockedCommand");

        var failure = Assert.IsType<HandlerErrorResult>(result);
        Assert.Equal(DiagnosticErrorCodes.UnsupportedOperation, failure.Error.Code);
        Assert.Contains("disabled", failure.Error.Message, StringComparison.OrdinalIgnoreCase);
    }

    private static object HandleWithHosted(Func<Control> createTarget, string query)
    {
        var window = Dispatcher.UIThread.Invoke(() =>
        {
            var host = new Window
            {
                Width = 120,
                Height = 80,
                IsVisible = true,
                Content = createTarget()
            };
            host.ApplyTemplate();
            host.Measure(new Size(120, 80));
            host.Arrange(new Rect(0, 0, 120, 80));
            NodeRegistry.GetOrRegister(host);
            return host;
        });

        try
        {
            var task = new ClickByQueryHandler().Handle(new DiagnosticRequest
            {
                Id = "test",
                Method = ProtocolMethods.ClickByQuery,
                Params = JsonSerializer.SerializeToElement(new { query })
            });
            return task.WaitAsync(TimeSpan.FromSeconds(5)).GetAwaiter().GetResult();
        }
        finally
        {
            Dispatcher.UIThread.Invoke(window.Close);
        }
    }

    private sealed class FixedCommand(bool canExecute) : ICommand
    {
        public bool CanExecute(object? parameter) => canExecute;
        public void Execute(object? parameter) { }
        public event EventHandler? CanExecuteChanged
        {
            add { }
            remove { }
        }
    }
}
