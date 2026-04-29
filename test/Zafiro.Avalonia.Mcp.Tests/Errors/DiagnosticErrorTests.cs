using System.Text.Json;
using Zafiro.Avalonia.Mcp.AppHost.Handlers;
using Zafiro.Avalonia.Mcp.Protocol;
using Zafiro.Avalonia.Mcp.Protocol.Messages;
using Xunit;

namespace Zafiro.Avalonia.Mcp.Tests.Errors;

public class DiagnosticErrorTests
{
    [Fact]
    public void DiagnosticError_Minimal_RoundTrips()
    {
        var error = new DiagnosticError("boom", DiagnosticErrorCodes.Internal);

        var json = ProtocolSerializer.Serialize(error);
        var parsed = ProtocolSerializer.Deserialize<DiagnosticError>(json);

        Assert.NotNull(parsed);
        Assert.Equal("boom", parsed.Message);
        Assert.Equal("INTERNAL", parsed.Code);
        Assert.Null(parsed.Suggested);
        Assert.Null(parsed.Details);
        Assert.DoesNotContain("\"suggested\"", json);
        Assert.DoesNotContain("\"details\"", json);
    }

    [Fact]
    public void DiagnosticError_WithSuggestionAndDetails_RoundTrips()
    {
        var error = new DiagnosticError(
            "selector matched 3 elements",
            DiagnosticErrorCodes.AmbiguousSelector,
            "use a more specific selector or pick the first occurrence",
            new { selector = "Button", count = 3 });

        var json = ProtocolSerializer.Serialize(error);
        var parsed = ProtocolSerializer.Deserialize<DiagnosticError>(json);

        Assert.NotNull(parsed);
        Assert.Equal("AMBIGUOUS_SELECTOR", parsed.Code);
        Assert.Equal("use a more specific selector or pick the first occurrence", parsed.Suggested);
        Assert.NotNull(parsed.Details);

        using var doc = JsonDocument.Parse(json);
        var details = doc.RootElement.GetProperty("details");
        Assert.Equal("Button", details.GetProperty("selector").GetString());
        Assert.Equal(3, details.GetProperty("count").GetInt32());
    }

    [Fact]
    public void DiagnosticResponse_Failure_StructuredMirrorsLegacyError()
    {
        var error = new DiagnosticError("missing", DiagnosticErrorCodes.InvalidParam, "supply nodeId");

        var response = DiagnosticResponse.Failure("req-1", error);

        Assert.Equal("missing", response.Error);
        Assert.NotNull(response.ErrorInfo);
        Assert.Equal("INVALID_PARAM", response.ErrorInfo!.Code);
        Assert.Equal("supply nodeId", response.ErrorInfo.Suggested);
    }

    [Fact]
    public void DiagnosticResponse_Failure_Structured_RoundTrips()
    {
        var error = new DiagnosticError("oops", DiagnosticErrorCodes.StaleNode, "refresh");
        var response = DiagnosticResponse.Failure("r-1", error);

        var json = ProtocolSerializer.Serialize(response);
        var parsed = ProtocolSerializer.Deserialize<DiagnosticResponse>(json);

        Assert.NotNull(parsed);
        Assert.Equal("oops", parsed.Error);
        Assert.NotNull(parsed.ErrorInfo);
        Assert.Equal("STALE_NODE", parsed.ErrorInfo!.Code);
        Assert.Equal("refresh", parsed.ErrorInfo.Suggested);
    }

    [Fact]
    public void DiagnosticResponse_LegacyFailure_HasNoErrorInfo()
    {
        var response = DiagnosticResponse.Failure("legacy", "just a string");

        Assert.Equal("just a string", response.Error);
        Assert.Null(response.ErrorInfo);

        var json = ProtocolSerializer.Serialize(response);
        Assert.DoesNotContain("\"errorInfo\"", json);
    }

    [Fact]
    public void HandlerResult_Error_PopulatesAllFields()
    {
        var result = HandlerResult.Error("CUSTOM", "msg", "hint", new { x = 1 });

        Assert.Equal("CUSTOM", result.Error.Code);
        Assert.Equal("msg", result.Error.Message);
        Assert.Equal("hint", result.Error.Suggested);
        Assert.NotNull(result.Error.Details);
    }

    [Fact]
    public void HandlerResult_StaleNode_UsesStableCodeAndHint()
    {
        var result = HandlerResult.StaleNode(42);

        Assert.Equal(DiagnosticErrorCodes.StaleNode, result.Error.Code);
        Assert.Contains("42", result.Error.Message);
        Assert.NotNull(result.Error.Suggested);
        Assert.Contains("get_snapshot", result.Error.Suggested!);
    }
}
