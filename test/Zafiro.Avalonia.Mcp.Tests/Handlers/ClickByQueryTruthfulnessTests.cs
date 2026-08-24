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
        var button = new Button { Name = "DisabledAction", IsEnabled = false };

        var result = HandleWithHosted(button, "DisabledAction");

        var failure = Assert.IsType<HandlerErrorResult>(result);
        Assert.Equal(DiagnosticErrorCodes.UnsupportedOperation, failure.Error.Code);
        Assert.Contains("disabled", failure.Error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ClickByQuery_CommandCannotExecute_ReturnsStructuredDisabledFailure()
    {
        var button = new Button
        {
            Name = "BlockedCommand",
            Command = new FixedCommand(canExecute: false)
        };

        var result = HandleWithHosted(button, "BlockedCommand");

        var failure = Assert.IsType<HandlerErrorResult>(result);
        Assert.Equal(DiagnosticErrorCodes.UnsupportedOperation, failure.Error.Code);
        Assert.Contains("disabled", failure.Error.Message, StringComparison.OrdinalIgnoreCase);
    }

    private static object HandleWithHosted(Control target, string query)
    {
        var window = Dispatcher.UIThread.Invoke(() =>
        {
            var host = new Window
            {
                Width = 120,
                Height = 80,
                IsVisible = true,
                Content = target
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
            var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(5);
            while (!task.IsCompleted && DateTime.UtcNow < deadline)
            {
                Dispatcher.UIThread.RunJobs();
                Thread.Sleep(1);
            }

            if (!task.IsCompleted)
                throw new TimeoutException("ClickByQueryHandler did not complete.");

            return task.GetAwaiter().GetResult();
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
