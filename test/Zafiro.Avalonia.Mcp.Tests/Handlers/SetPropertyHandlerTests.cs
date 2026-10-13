using System.Text.Json;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Threading;
using Xunit;
using Zafiro.Avalonia.Mcp.AppHost.Handlers;
using Zafiro.Avalonia.Mcp.Protocol.Messages;

namespace Zafiro.Avalonia.Mcp.Tests.Handlers;

[Collection("Avalonia")]
public class SetPropertyHandlerTests
{
    public SetPropertyHandlerTests(AvaloniaTestFixture _)
    {
        NodeRegistry.Clear();
    }

    private static T Run<T>(Func<T> f) => Dispatcher.UIThread.Invoke(f);

    [Theory]
    [InlineData("true", true)]
    [InlineData("False", false)]
    public void NullableBoolean_AcceptsAdvertisedValues(string value, bool expected)
    {
        var flag = Run(() => new ToggleSwitch { IsChecked = null });

        var result = Run(() => SetPropertyHandler.SetProperty(flag, "IsChecked", value));

        Assert.IsNotType<HandlerErrorResult>(result);
        Assert.Equal(expected, Run(() => flag.IsChecked));
    }

    [Theory]
    [InlineData("{x:Null}")]
    [InlineData("null")]
    public void NullableProperty_NullToken_AssignsNullInsteadOfClearing(string value)
    {
        var flag = Run(() => new ToggleSwitch { IsThreeState = true, IsChecked = true });

        Run(() => SetPropertyHandler.SetProperty(flag, "IsChecked", value));

        Assert.Null(Run(() => flag.IsChecked));
        Assert.True(Run(() => flag.IsSet(ToggleButton.IsCheckedProperty)));
    }

    [Fact]
    public void Unset_ClearsLocalValue()
    {
        var flag = Run(() => new ToggleSwitch { IsChecked = true });

        Run(() => SetPropertyHandler.SetProperty(flag, "IsChecked", "unset"));

        Assert.False(Run(() => flag.IsSet(ToggleButton.IsCheckedProperty)));
    }

    [Fact]
    public void NullableDateTime_AcceptsIsoDate()
    {
        var picker = Run(() => new CalendarDatePicker());

        var result = Run(() => SetPropertyHandler.SetProperty(picker, "SelectedDate", "2026-01-15"));

        Assert.IsNotType<HandlerErrorResult>(result);
        Assert.Equal(new DateTime(2026, 1, 15), Run(() => picker.SelectedDate));
    }

    [Fact]
    public void DateTime_AcceptsIsoDate()
    {
        var picker = Run(() => new CalendarDatePicker());

        var result = Run(() => SetPropertyHandler.SetProperty(picker, "DisplayDate", "2026-01-15"));

        Assert.IsNotType<HandlerErrorResult>(result);
        Assert.Equal(new DateTime(2026, 1, 15), Run(() => picker.DisplayDate));
    }

    [Fact]
    public void AvaloniaValueType_IsParsed()
    {
        var border = Run(() => new Border());

        Run(() => SetPropertyHandler.SetProperty(border, "Margin", "1,2,3,4"));

        Assert.Equal(new Thickness(1, 2, 3, 4), Run(() => border.Margin));
    }

    [Fact]
    public void StringProperty_KeepsLiteralNullText()
    {
        var box = Run(() => new TextBox());

        Run(() => SetPropertyHandler.SetProperty(box, "Text", "null"));

        Assert.Equal("null", Run(() => box.Text));
    }

    [Fact]
    public void InvalidValue_ReturnsInvalidParamWithReadableType()
    {
        var picker = Run(() => new CalendarDatePicker());

        var result = Run(() => SetPropertyHandler.SetProperty(picker, "SelectedDate", "not a date"));

        var error = Assert.IsType<HandlerErrorResult>(result).Error;
        Assert.Equal(DiagnosticErrorCodes.InvalidParam, error.Code);
        Assert.Contains("DateTime?", error.Message);
        Assert.DoesNotContain("Nullable`1", error.Message);
        Assert.Contains("{x:Null}", error.Suggested);
    }

    [Fact]
    public void UnknownProperty_ReturnsInvalidParam()
    {
        var border = Run(() => new Border());

        var result = Run(() => SetPropertyHandler.SetProperty(border, "DoesNotExist", "1"));

        Assert.Equal(DiagnosticErrorCodes.InvalidParam, Assert.IsType<HandlerErrorResult>(result).Error.Code);
    }

    [Fact]
    public void GetPropValues_AdvertisesNullability()
    {
        var doc = Run(() =>
        {
            var result = PropertyValuesHandler.BuildResult(new ToggleSwitch(), "IsChecked");
            return JsonDocument.Parse(JsonSerializer.Serialize(result)).RootElement;
        });

        Assert.True(doc.GetProperty("nullable").GetBoolean());
        Assert.Equal("{x:Null}", doc.GetProperty("nullValue").GetString());
    }
}
