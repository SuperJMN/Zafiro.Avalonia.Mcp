using System.Text.Json;
using Avalonia.Controls;
using Avalonia.Threading;
using Xunit;
using Zafiro.Avalonia.Mcp.AppHost.Handlers;

namespace Zafiro.Avalonia.Mcp.Tests.Handlers;

[Collection("Avalonia")]
public class PseudoClassHandlerTests
{
    [Fact]
    public void PseudoClass_Activate_AddsPseudoClassToElement()
    {
        var (result, classes) = Dispatcher.UIThread.Invoke(() =>
        {
            var button = new Button();
            var response = Serialize(PseudoClassHandler.PseudoClass(button, "pointerover", true));
            return (response, button.Classes.ToList());
        });

        Assert.True(result.GetProperty("success").GetBoolean());
        Assert.Equal(":pointerover", result.GetProperty("pseudoClass").GetString());
        Assert.Contains(":pointerover", classes);
    }

    [Fact]
    public void PseudoClass_Deactivate_RemovesPseudoClassFromElement()
    {
        var classes = Dispatcher.UIThread.Invoke(() =>
        {
            var button = new Button();
            PseudoClassHandler.PseudoClass(button, ":pointerover", true);
            PseudoClassHandler.PseudoClass(button, ":pointerover", false);
            return button.Classes.ToList();
        });

        Assert.DoesNotContain(":pointerover", classes);
    }

    [Fact]
    public void PseudoClass_WithoutName_ListsActivePseudoClasses()
    {
        var result = Dispatcher.UIThread.Invoke(() =>
        {
            var button = new Button { IsEnabled = false };
            button.Classes.Add("primary");
            return Serialize(PseudoClassHandler.PseudoClass(button, null, null));
        });

        var pseudoClasses = result.GetProperty("pseudoClasses").EnumerateArray().Select(e => e.GetString()).ToList();
        Assert.Contains(":disabled", pseudoClasses);
        Assert.DoesNotContain("primary", pseudoClasses);
    }

    private static JsonElement Serialize(object value) => JsonSerializer.SerializeToElement(value);
}
