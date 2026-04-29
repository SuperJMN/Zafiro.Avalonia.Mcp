using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Xunit;
using Zafiro.Avalonia.Mcp.AppHost.Handlers;
using Zafiro.Avalonia.Mcp.Protocol.Messages;

namespace Zafiro.Avalonia.Mcp.Tests.Handlers;

/// <summary>
/// Tests for FillFormHandler.FillFormCore — driven through a synthetic resolver so the
/// dispatcher is never engaged (avoids the headless SetupWithoutStarting() deadlock).
/// </summary>
[Collection("Avalonia")]
public class FillFormHandlerTests
{
    public FillFormHandlerTests(AvaloniaTestFixture _)
    {
        NodeRegistry.Clear();
    }

    private static Func<string, (Visual? visual, DiagnosticError? error)> ResolverFor(
        Dictionary<string, Visual> map)
    {
        return selector =>
        {
            if (map.TryGetValue(selector, out var v))
                return (v, null);
            return (null, new DiagnosticError(
                "no element matched selector",
                DiagnosticErrorCodes.NoMatch,
                null,
                new { selector }));
        };
    }

    [Fact]
    public void FillForm_AllSucceed_ForTextBoxCheckBoxAndSlider()
    {
        var email = new TextBox();
        var remember = new CheckBox();
        var volume = new Slider { Minimum = 0, Maximum = 100, Value = 0 };

        var resolver = ResolverFor(new()
        {
            ["TextBox#Email"] = email,
            ["CheckBox#Remember"] = remember,
            ["Slider#Volume"] = volume,
        });

        var fields = new[]
        {
            new FieldRequest("TextBox#Email", Value: "user@example.com"),
            new FieldRequest("CheckBox#Remember", Checked: true),
            new FieldRequest("Slider#Volume", Number: 75),
        };

        var response = FillFormHandler.FillFormCore(fields, submit: null, resolver);

        Assert.Equal(3, response.SuccessCount);
        Assert.Equal(0, response.FailureCount);
        Assert.All(response.Results, r => Assert.True(r.Ok));
        Assert.Equal("value", response.Results[0].Applied);
        Assert.Equal("checked", response.Results[1].Applied);
        Assert.Equal("number", response.Results[2].Applied);
        Assert.Equal("user@example.com", email.Text);
        Assert.True(remember.IsChecked);
        Assert.Equal(75, volume.Value);
        Assert.Null(response.Submit);
    }

    [Fact]
    public void FillForm_MixedOutcomes_DoesNotAbort_AndReportsEachField()
    {
        var goodBox = new TextBox();
        var slider = new Slider { Minimum = 0, Maximum = 100 };

        var resolver = ResolverFor(new()
        {
            ["TextBox#Good"] = goodBox,
            ["Slider#Volume"] = slider,
            // "TextBox#Missing" is intentionally absent
        });

        var fields = new[]
        {
            new FieldRequest("TextBox#Good", Value: "ok"),                       // OK
            new FieldRequest("TextBox#Missing", Value: "x"),                      // selector miss -> NO_MATCH
            new FieldRequest("Slider#Volume", Checked: true),                     // type mismatch -> UNSUPPORTED_OPERATION
        };

        var response = FillFormHandler.FillFormCore(fields, submit: null, resolver);

        Assert.Equal(3, response.Results.Count);
        Assert.Equal(1, response.SuccessCount);
        Assert.Equal(2, response.FailureCount);

        Assert.True(response.Results[0].Ok);
        Assert.Equal("ok", goodBox.Text);

        Assert.False(response.Results[1].Ok);
        Assert.Equal(DiagnosticErrorCodes.NoMatch, response.Results[1].ErrorInfo!.Code);

        Assert.False(response.Results[2].Ok);
        Assert.Equal(DiagnosticErrorCodes.UnsupportedOperation, response.Results[2].ErrorInfo!.Code);
    }

    [Fact]
    public void FillForm_Submit_Click_Succeeds_WhenSelectorResolves()
    {
        var box = new TextBox();
        var clicked = false;
        var btn = new Button();
        btn.Click += (_, _) => clicked = true;

        var resolver = ResolverFor(new()
        {
            ["TextBox#Email"] = box,
            ["Button#SignIn"] = btn,
        });

        var fields = new[] { new FieldRequest("TextBox#Email", Value: "user@example.com") };
        var response = FillFormHandler.FillFormCore(fields, submit: "Button#SignIn", resolver);

        Assert.NotNull(response.Submit);
        Assert.True(response.Submit!.Ok);
        Assert.True(clicked);
    }

    [Fact]
    public void FillForm_Submit_Failure_WhenSelectorMatchesNothing()
    {
        var box = new TextBox();
        var resolver = ResolverFor(new() { ["TextBox#Email"] = box });

        var fields = new[] { new FieldRequest("TextBox#Email", Value: "user@example.com") };
        var response = FillFormHandler.FillFormCore(fields, submit: "Button#Missing", resolver);

        Assert.NotNull(response.Submit);
        Assert.False(response.Submit!.Ok);
        Assert.Equal(DiagnosticErrorCodes.NoMatch, response.Submit.ErrorInfo!.Code);
    }

    [Fact]
    public void FillForm_RunsSubmit_EvenWhenAFieldFailed()
    {
        var box = new TextBox();
        var clicked = false;
        var btn = new Button();
        btn.Click += (_, _) => clicked = true;
        var resolver = ResolverFor(new()
        {
            ["TextBox#Good"] = box,
            ["Button#Submit"] = btn,
        });

        var fields = new[]
        {
            new FieldRequest("TextBox#Good", Value: "ok"),
            new FieldRequest("TextBox#Missing", Value: "x"),
        };

        var response = FillFormHandler.FillFormCore(fields, submit: "Button#Submit", resolver);

        Assert.Equal(1, response.SuccessCount);
        Assert.Equal(1, response.FailureCount);
        Assert.True(response.Submit!.Ok);
        Assert.True(clicked);
    }

    [Fact]
    public void FillForm_Secret_Redacts_AppliedValue()
    {
        var pwd = new TextBox();
        var resolver = ResolverFor(new() { ["TextBox#Password"] = pwd });

        var fields = new[] { new FieldRequest("TextBox#Password", Value: "hunter2", Secret: true) };
        var response = FillFormHandler.FillFormCore(fields, submit: null, resolver);

        Assert.True(response.Results[0].Ok);
        Assert.Equal("value (redacted)", response.Results[0].Applied);
        // The actual value still gets applied to the control:
        Assert.Equal("hunter2", pwd.Text);
    }

    [Fact]
    public void FillForm_Field_WithNoValueKey_ReturnsInvalidParam()
    {
        var box = new TextBox();
        var resolver = ResolverFor(new() { ["TextBox#X"] = box });

        var fields = new[] { new FieldRequest("TextBox#X") };
        var response = FillFormHandler.FillFormCore(fields, submit: null, resolver);

        Assert.False(response.Results[0].Ok);
        Assert.Equal(DiagnosticErrorCodes.InvalidParam, response.Results[0].ErrorInfo!.Code);
    }

    [Fact]
    public void FillForm_Select_ByText_MatchesItem()
    {
        var combo = new ComboBox();
        combo.ItemsSource = new[] { "France", "Spain", "Germany" };
        var resolver = ResolverFor(new() { ["ComboBox#Country"] = combo });

        var fields = new[] { new FieldRequest("ComboBox#Country", SelectText: "Spain") };
        var response = FillFormHandler.FillFormCore(fields, submit: null, resolver);

        Assert.True(response.Results[0].Ok);
        Assert.Equal("select", response.Results[0].Applied);
        Assert.Equal(1, combo.SelectedIndex);
    }
}
