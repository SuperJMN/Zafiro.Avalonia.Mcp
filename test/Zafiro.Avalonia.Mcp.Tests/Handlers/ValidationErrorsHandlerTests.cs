using Avalonia.Controls;
using Xunit;
using Zafiro.Avalonia.Mcp.AppHost.Handlers;
using Zafiro.Avalonia.Mcp.Protocol;
using Zafiro.Avalonia.Mcp.Protocol.Messages;
using System.Text.Json;

namespace Zafiro.Avalonia.Mcp.Tests.Handlers;

[Collection("Avalonia")]
public class ValidationErrorsHandlerTests
{
    public ValidationErrorsHandlerTests(AvaloniaTestFixture _) { }

    [Fact]
    public void Returns_EmptyList_WhenNoErrors()
    {
        var clean = new TextBox { Name = "clean" };

        var result = Scan(null, [clean]);

        Assert.Equal(0, result.GetProperty("count").GetInt32());
        Assert.Equal(0, result.GetProperty("items").GetArrayLength());
    }

    [Fact]
    public void Returns_Item_WhenControlHasValidationError()
    {
        var textBox = new TextBox { Name = "emailBox" };
        DataValidationErrors.SetErrors(textBox, [new Exception("required")]);

        var result = Scan(null, [textBox]);

        Assert.Equal(1, result.GetProperty("count").GetInt32());
        var item = result.GetProperty("items")[0];
        Assert.True(item.GetProperty("hasErrors").GetBoolean());
        Assert.Equal("emailBox", item.GetProperty("name").GetString());

        var errors = item.GetProperty("errors");
        Assert.Equal(1, errors.GetArrayLength());
        Assert.Equal("required", errors[0].GetProperty("message").GetString());
        Assert.Equal("Exception", errors[0].GetProperty("source").GetString());
    }

    [Fact]
    public void Returns_Only_ErrorControls_Not_CleanOnes()
    {
        var clean = new TextBox { Name = "clean" };
        var dirty = new TextBox { Name = "dirty" };
        DataValidationErrors.SetErrors(dirty, [new Exception("bad value")]);

        var result = Scan(null, [clean, dirty]);

        Assert.Equal(1, result.GetProperty("count").GetInt32());
        Assert.Equal("dirty", result.GetProperty("items")[0].GetProperty("name").GetString());
    }

    [Fact]
    public void Scope_IsApp_WhenScanningWithoutOverride()
    {
        // When no overrideCandidates, ScanForErrors falls through to GetWindows()
        // which returns [] in headless mode → scope is "app"
        var result = Scan(null, null);

        Assert.Equal("app", result.GetProperty("scope").GetString());
    }

    [Fact]
    public void MultipleErrors_AllReported()
    {
        var textBox = new TextBox { Name = "field" };
        DataValidationErrors.SetErrors(textBox, [new Exception("error1"), new Exception("error2")]);

        var result = Scan(null, [textBox]);

        Assert.Equal(1, result.GetProperty("count").GetInt32());
        Assert.Equal(2, result.GetProperty("items")[0].GetProperty("errors").GetArrayLength());
    }

    private static JsonElement Scan(string? selector, IEnumerable<Control>? candidates) =>
        JsonSerializer.SerializeToElement(ValidationErrorsHandler.ScanForErrors(selector, candidates));
}

