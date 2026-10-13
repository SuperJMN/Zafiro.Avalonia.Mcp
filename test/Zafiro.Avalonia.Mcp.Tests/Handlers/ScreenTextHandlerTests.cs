using System.Text.Json;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Themes.Fluent;
using Avalonia.Threading;
using Xunit;
using Zafiro.Avalonia.Mcp.AppHost.Handlers;

namespace Zafiro.Avalonia.Mcp.Tests.Handlers;

[Collection("Avalonia")]
public class ScreenTextHandlerTests
{
    public ScreenTextHandlerTests(AvaloniaTestFixture _)
    {
        NodeRegistry.Clear();
    }

    [Fact]
    public void PositionedText_IsReportedAtItsRootPosition_NotTwice() => WithCanvas(canvas =>
    {
        canvas.Children.Add(Positioned(new TextBlock { Text = "Marker" }, 80, 40));
    }, (window, lines) =>
    {
        var marker = Assert.Single(lines, l => l.Text == "Marker");
        Assert.Equal((80d, 40d), (marker.X, marker.Y));
    });

    [Fact]
    public void TemplatedButtonLabel_IsReportedOnce_AtTheButtonPosition() => WithCanvas(canvas =>
    {
        canvas.Children.Add(Positioned(new Button { Content = "Repeated label" }, 240, 85));
    }, (window, lines) =>
    {
        var label = Assert.Single(lines, l => l.Text == "Repeated label");
        Assert.Equal((240d, 85d), (label.X, label.Y));
    });

    [Fact]
    public void DistinctControlsWithEqualText_RemainDistinct() => WithCanvas(canvas =>
    {
        canvas.Children.Add(Positioned(new Button { Content = "Same" }, 10, 10));
        canvas.Children.Add(Positioned(new Button { Content = "Same" }, 15, 12));
    }, (window, lines) =>
    {
        Assert.Equal(2, lines.Count(l => l.Text == "Same"));
    });

    [Fact]
    public void Lines_DeclareTheirRoot() => WithCanvas(canvas =>
    {
        canvas.Children.Add(new TextBlock { Text = "Hello" });
    }, (window, lines) =>
    {
        Assert.Equal(NodeRegistry.GetOrRegister(window), Assert.Single(lines).RootId);
    });

    private static Control Positioned(Control control, double left, double top)
    {
        Canvas.SetLeft(control, left);
        Canvas.SetTop(control, top);
        return control;
    }

    private static void WithCanvas(Action<Canvas> populate, Action<Window, IReadOnlyList<Line>> assert) => Dispatcher.UIThread.Invoke(() =>
    {
        var window = new Window { Width = 500, Height = 300 };
        window.Styles.Add(new FluentTheme());
        var canvas = new Canvas();
        populate(canvas);
        window.Content = canvas;
        window.Show();

        try
        {
            var json = JsonSerializer.SerializeToElement(ScreenTextHandler.BuildResult([canvas], visibleOnly: false));
            var lines = json.GetProperty("lines").EnumerateArray()
                .Select(l => new Line(
                    l.GetProperty("Text").GetString()!,
                    l.GetProperty("X").GetDouble(),
                    l.GetProperty("Y").GetDouble(),
                    l.GetProperty("RootId").GetInt32()))
                .ToList();
            assert(window, lines);
        }
        finally
        {
            window.Close();
        }
    });

    private sealed record Line(string Text, double X, double Y, int RootId);
}
