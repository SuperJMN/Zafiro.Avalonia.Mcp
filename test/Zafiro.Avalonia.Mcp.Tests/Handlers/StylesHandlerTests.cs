using Avalonia.Controls;
using Avalonia.Styling;
using Avalonia.Threading;
using Xunit;
using Zafiro.Avalonia.Mcp.AppHost.Handlers;
using Zafiro.Avalonia.Mcp.Protocol;

namespace Zafiro.Avalonia.Mcp.Tests.Handlers;

[Collection("Avalonia")]
public class StylesHandlerTests
{
    public StylesHandlerTests(AvaloniaTestFixture _)
    {
        NodeRegistry.Clear();
    }

    [Fact]
    public void GetStyles_ReportsTheDeclaredSetter_NotTheOverridingLocalValue()
    {
        Dispatcher.UIThread.Invoke(() =>
        {
            var border = new Border { Width = 99 };
            border.Classes.Add("highlight");
            var style = new Style(selector => selector.OfType<Border>().Class("highlight"));
            style.Setters.Add(new Setter(Border.WidthProperty, 42d));
            var window = new Window { Content = border };
            window.Styles.Add(style);
            window.Show();

            try
            {
                var snapshot = ProtocolSerializer.ToElement(StylesHandler.GetStyles(border, false, null));

                Assert.True(snapshot.TryGetProperty("styles", out var styles), snapshot.ToString());
                var applied = Assert.Single(styles.EnumerateArray());
                Assert.Equal("Border.highlight", applied.GetProperty("selector").GetString());
                Assert.True(applied.GetProperty("active").GetBoolean());
                var setter = Assert.Single(applied.GetProperty("setters").EnumerateArray());
                Assert.Equal("42", setter.GetProperty("provider").GetProperty("value").GetString());
                Assert.Equal(99, border.Width);
            }
            finally
            {
                window.Close();
            }
        });
    }
}
