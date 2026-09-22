using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;
using Xunit;
using Zafiro.Avalonia.Mcp.AppHost.Handlers;

namespace Zafiro.Avalonia.Mcp.Tests.Handlers;

[Collection("Avalonia")]
public class WaitForHandlerTests
{
    public WaitForHandlerTests(AvaloniaTestFixture _)
    {
        NodeRegistry.Clear();
    }

    [Fact]
    public void Enabled_DoesNotMatchButtonWhoseCommandCannotExecute()
    {
        var window = Dispatcher.UIThread.Invoke(() =>
        {
            var button = new Button
            {
                Name = "BlockedAction",
                Command = new TestCommand(canExecute: false)
            };
            var host = new Window { Width = 120, Height = 80, IsVisible = true, Content = button };
            host.ApplyTemplate();
            host.Measure(new Size(120, 80));
            host.Arrange(new Rect(0, 0, 120, 80));
            NodeRegistry.GetOrRegister(host);
            return host;
        });

        try
        {
            Assert.NotNull(Complete(WaitForHandler.PollUntilCondition("BlockedAction", "exists", null, 120)));
            var result = Complete(WaitForHandler.PollUntilCondition("BlockedAction", "enabled", null, 120));

            Assert.Null(result);
        }
        finally
        {
            Dispatcher.UIThread.Invoke(window.Close);
        }
    }

    private static object? Complete(Task<object?> task) =>
        task.WaitAsync(TimeSpan.FromSeconds(2)).GetAwaiter().GetResult();
}
