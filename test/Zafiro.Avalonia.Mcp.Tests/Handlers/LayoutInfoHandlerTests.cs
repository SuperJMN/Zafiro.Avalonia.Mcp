using System.Text.Json;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Threading;
using Xunit;
using Zafiro.Avalonia.Mcp.AppHost.Handlers;

namespace Zafiro.Avalonia.Mcp.Tests.Handlers;

[Collection("Avalonia")]
public class LayoutInfoHandlerTests
{
    public LayoutInfoHandlerTests(AvaloniaTestFixture _)
    {
        NodeRegistry.Clear();
    }

    private static T Run<T>(Func<T> f) => Dispatcher.UIThread.Invoke(f);

    private static JsonElement Analyze(Visual v)
    {
        var result = LayoutInfoHandler.BuildResult(v);
        return JsonDocument.Parse(JsonSerializer.Serialize(result)).RootElement;
    }

    [Fact]
    public void VisibleButton_HasIsVisibleTrue()
    {
        var doc = Run(() =>
        {
            var btn = new Button { Width = 100, Height = 40 };
            return Analyze(btn);
        });

        Assert.True(doc.GetProperty("isVisible").GetBoolean());
        Assert.True(doc.GetProperty("isEffectivelyVisible").GetBoolean());
        Assert.True(doc.GetProperty("isEnabled").GetBoolean());
    }

    [Fact]
    public void HiddenButton_HasIsVisibleFalse()
    {
        var doc = Run(() =>
        {
            var btn = new Button { IsVisible = false };
            return Analyze(btn);
        });

        Assert.False(doc.GetProperty("isVisible").GetBoolean());
    }

    [Fact]
    public void AncestorHidden_HasIsEffectivelyVisibleFalse()
    {
        var doc = Run(() =>
        {
            var btn = new Button();
            var parent = new StackPanel { IsVisible = false, Children = { btn } };
            return Analyze(btn);
        });

        Assert.True(doc.GetProperty("isVisible").GetBoolean(), "Button itself is visible");
        Assert.False(doc.GetProperty("isEffectivelyVisible").GetBoolean(), "Parent hides it");
    }

    [Fact]
    public void ExplicitWidthAndMargin_ReflectedInOutput()
    {
        var doc = Run(() =>
        {
            var btn = new Button
            {
                Width = 80,
                Height = 24,
                Margin = new Thickness(5, 10, 15, 20)
            };
            return Analyze(btn);
        });

        Assert.Equal(80, doc.GetProperty("width").GetDouble());
        Assert.Equal(24, doc.GetProperty("height").GetDouble());

        var margin = doc.GetProperty("margin");
        Assert.Equal(5, margin.GetProperty("l").GetDouble());
        Assert.Equal(10, margin.GetProperty("t").GetDouble());
        Assert.Equal(15, margin.GetProperty("r").GetDouble());
        Assert.Equal(20, margin.GetProperty("b").GetDouble());
    }

    [Fact]
    public void NaNWidth_IsNullInOutput()
    {
        var doc = Run(() =>
        {
            var btn = new Button(); // Width/Height default to NaN
            return Analyze(btn);
        });

        Assert.Equal(JsonValueKind.Null, doc.GetProperty("width").ValueKind);
        Assert.Equal(JsonValueKind.Null, doc.GetProperty("height").ValueKind);
    }

    [Fact]
    public void MaxWidth_InfinityIsNull()
    {
        var doc = Run(() =>
        {
            var btn = new Button(); // MaxWidth defaults to Infinity
            return Analyze(btn);
        });

        Assert.Equal(JsonValueKind.Null, doc.GetProperty("maxWidth").ValueKind);
        Assert.Equal(JsonValueKind.Null, doc.GetProperty("maxHeight").ValueKind);
    }

    [Fact]
    public void ExplicitMaxWidth_IsPresent()
    {
        var doc = Run(() =>
        {
            var btn = new Button { MaxWidth = 200, MaxHeight = 100 };
            return Analyze(btn);
        });

        Assert.Equal(200, doc.GetProperty("maxWidth").GetDouble());
        Assert.Equal(100, doc.GetProperty("maxHeight").GetDouble());
    }

    [Fact]
    public void HorizontalAlignment_ReturnsString()
    {
        var doc = Run(() =>
        {
            var btn = new Button { HorizontalAlignment = HorizontalAlignment.Center };
            return Analyze(btn);
        });

        Assert.Equal("Center", doc.GetProperty("horizontalAlignment").GetString());
    }

    [Fact]
    public void MatchedCount_NullWhenSingle()
    {
        var doc = Run(() =>
        {
            var btn = new Button();
            return Analyze(btn);
        });

        Assert.Equal(JsonValueKind.Null, doc.GetProperty("matchedCount").ValueKind);
    }

    [Fact]
    public void MatchedCount_SetWhenMultiple()
    {
        var doc = Run(() =>
        {
            var btn = new Button();
            return JsonDocument.Parse(JsonSerializer.Serialize(LayoutInfoHandler.BuildResult(btn, 3))).RootElement;
        });

        Assert.Equal(3, doc.GetProperty("matchedCount").GetInt32());
    }

    [Fact]
    public void DisabledButton_HasIsEnabledFalse()
    {
        var doc = Run(() =>
        {
            var btn = new Button { IsEnabled = false };
            return Analyze(btn);
        });

        Assert.False(doc.GetProperty("isEnabled").GetBoolean());
    }

    [Fact]
    public void AfterMeasureArrange_IsMeasureValid()
    {
        var doc = Run(() =>
        {
            var btn = new Button { Width = 80, Height = 30 };
            var sp = new StackPanel { Children = { btn } };
            sp.Measure(Size.Infinity);
            sp.Arrange(new Rect(sp.DesiredSize));
            return Analyze(btn);
        });

        Assert.True(doc.GetProperty("isMeasureValid").GetBoolean());
        Assert.True(doc.GetProperty("isArrangeValid").GetBoolean());
    }
}
