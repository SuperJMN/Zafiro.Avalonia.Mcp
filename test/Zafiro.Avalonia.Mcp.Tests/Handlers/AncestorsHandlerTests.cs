using Avalonia.Controls;
using Avalonia.Threading;
using Xunit;
using Zafiro.Avalonia.Mcp.AppHost.Handlers;
using Zafiro.Avalonia.Mcp.Protocol.Models;

namespace Zafiro.Avalonia.Mcp.Tests.Handlers;

[Collection("Avalonia")]
public class AncestorsHandlerTests
{
    [Fact]
    public void GetAncestors_ReturnsClosestParentFirstAndExcludesTarget()
    {
        var ancestors = Dispatcher.UIThread.Invoke(() =>
        {
            var button = new Button();
            var border = new Border { Child = button };
            var root = new StackPanel { Children = { border } };

            var result = Assert.IsType<List<NodeInfo>>(AncestorsHandler.GetAncestors(button));
            GC.KeepAlive(root);
            return result;
        });

        Assert.Collection(
            ancestors,
            parent => Assert.Equal(nameof(Border), parent.Type),
            root => Assert.Equal(nameof(StackPanel), root.Type));
        Assert.DoesNotContain(ancestors, ancestor => ancestor.Type == nameof(Button));
    }
}
