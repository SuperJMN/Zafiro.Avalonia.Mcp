using System.Text.Json;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Threading;
using Xunit;
using Zafiro.Avalonia.Mcp.AppHost.Handlers;

namespace Zafiro.Avalonia.Mcp.Tests.Handlers;

[Collection("Avalonia")]
public class PropertyValuesHandlerTests
{
    public PropertyValuesHandlerTests(AvaloniaTestFixture _)
    {
        NodeRegistry.Clear();
    }

    private static JsonElement Run(Func<object> f)
    {
        var result = Dispatcher.UIThread.Invoke(f);
        return JsonDocument.Parse(JsonSerializer.Serialize(result)).RootElement;
    }

    [Fact]
    public void EnumProperty_ReturnsAllEnumNames()
    {
        var doc = Run(() => PropertyValuesHandler.BuildResult(new Button(), "HorizontalAlignment"));

        Assert.Equal("HorizontalAlignment", doc.GetProperty("propertyName").GetString());
        Assert.Equal("HorizontalAlignment", doc.GetProperty("type").GetString());

        var names = doc.GetProperty("values").EnumerateArray().Select(v => v.GetString()).ToArray();
        Assert.Contains(nameof(HorizontalAlignment.Left), names);
        Assert.Contains(nameof(HorizontalAlignment.Center), names);
        Assert.Contains(nameof(HorizontalAlignment.Stretch), names);
    }

    [Fact]
    public void BooleanProperty_ReturnsTrueFalse()
    {
        var doc = Run(() => PropertyValuesHandler.BuildResult(new Button(), "IsEnabled"));

        Assert.Equal("Boolean", doc.GetProperty("type").GetString());
        var values = doc.GetProperty("values").EnumerateArray().Select(v => v.GetString()).ToArray();
        Assert.Equal(new[] { "true", "false" }, values);
    }

    [Fact]
    public void UnknownProperty_ReturnsError()
    {
        var doc = Run(() => PropertyValuesHandler.BuildResult(new Button(), "DoesNotExist"));

        Assert.True(doc.TryGetProperty("error", out var err));
        Assert.Contains("DoesNotExist", err.GetString());
    }

    [Fact]
    public void PropertyName_IsCaseInsensitive()
    {
        var doc = Run(() => PropertyValuesHandler.BuildResult(new Button(), "horizontalalignment"));

        Assert.Equal("HorizontalAlignment", doc.GetProperty("propertyName").GetString());
    }

    [Fact]
    public void UnconstrainedProperty_ReturnsNullValuesAndNote()
    {
        var doc = Run(() => PropertyValuesHandler.BuildResult(new Button(), "Width"));

        Assert.Equal(JsonValueKind.Null, doc.GetProperty("values").ValueKind);
        Assert.True(doc.TryGetProperty("note", out _));
    }
}
