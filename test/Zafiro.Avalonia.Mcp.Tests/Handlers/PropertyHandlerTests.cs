using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;
using Xunit;
using Zafiro.Avalonia.Mcp.AppHost.Handlers;
using Zafiro.Avalonia.Mcp.Protocol.Models;

namespace Zafiro.Avalonia.Mcp.Tests.Handlers;

[Collection("Avalonia")]
public class PropertyHandlerTests
{
    public PropertyHandlerTests(AvaloniaTestFixture _)
    {
        NodeRegistry.Clear();
    }

    [Fact]
    public void GetProperties_QualifiesAttachedPropertiesAndReportsOwners()
    {
        var properties = Dispatcher.UIThread.Invoke(() =>
        {
            var button = new Button
            {
                Width = 280,
                Height = 200
            };
            button.SetValue(AttachedOwner.WidthProperty, 7d);

            return Assert.IsType<List<PropertyInfo>>(PropertyHandler.GetProperties(
                button,
                ["Width", "AttachedOwner.Width"]));
        });

        var width = Assert.Single(properties, property => property.Name == "Width");
        Assert.Equal("Layoutable", width.Owner);
        Assert.Equal("280", width.Value);

        var attachedWidth = Assert.Single(properties, property => property.Name == "AttachedOwner.Width");
        Assert.Equal("AttachedOwner", attachedWidth.Owner);
        Assert.Equal("7", attachedWidth.Value);
    }

    private sealed class AttachedOwner : AvaloniaObject
    {
        public static readonly AttachedProperty<double> WidthProperty =
            AvaloniaProperty.RegisterAttached<AttachedOwner, Control, double>("Width", 0);
    }
}
