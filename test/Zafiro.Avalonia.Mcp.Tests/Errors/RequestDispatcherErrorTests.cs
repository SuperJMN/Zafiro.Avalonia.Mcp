using System.Text.Json;
using Zafiro.Avalonia.Mcp.AppHost.Handlers;
using Zafiro.Avalonia.Mcp.Protocol;
using Zafiro.Avalonia.Mcp.Protocol.Messages;
using Zafiro.Avalonia.Mcp.Protocol.Selectors;
using Xunit;

namespace Zafiro.Avalonia.Mcp.Tests.Errors;

public class RequestDispatcherErrorTests
{
    private static string SerializeRequest(string method, object? @params = null) =>
        ProtocolSerializer.Serialize(new DiagnosticRequest
        {
            Id = "test",
            Method = method,
            Params = @params is null ? null : ProtocolSerializer.ToElement(@params)
        });

    private static async Task<DiagnosticResponse> Dispatch(IRequestHandler handler, string method, object? @params = null)
    {
        var dispatcher = new RequestDispatcher();
        InjectHandler(dispatcher, handler);
        var json = await dispatcher.Dispatch(SerializeRequest(method, @params));
        var response = ProtocolSerializer.Deserialize<DiagnosticResponse>(json);
        Assert.NotNull(response);
        return response!;
    }

    private static void InjectHandler(RequestDispatcher dispatcher, IRequestHandler handler)
    {
        var field = typeof(RequestDispatcher).GetField("_handlers",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;
        var dict = (Dictionary<string, IRequestHandler>)field.GetValue(dispatcher)!;
        dict[handler.Method] = handler;
    }

    private sealed class StubHandler : IRequestHandler
    {
        private readonly Func<DiagnosticRequest, Task<object>> _handle;
        public string Method { get; }

        public StubHandler(string method, Func<DiagnosticRequest, Task<object>> handle)
        {
            Method = method;
            _handle = handle;
        }

        public Task<object> Handle(DiagnosticRequest request) => _handle(request);
    }

    [Fact]
    public async Task SelectorParseException_IsConvertedToInvalidSelector()
    {
        var handler = new StubHandler("test_selector_throw",
            _ => throw new SelectorParseException("bad token", 5));

        var response = await Dispatch(handler, "test_selector_throw");

        Assert.NotNull(response.ErrorInfo);
        Assert.Equal(DiagnosticErrorCodes.InvalidSelector, response.ErrorInfo!.Code);
        Assert.Equal(response.ErrorInfo.Message, response.Error);
        Assert.Contains("position 5", response.Error!);
        Assert.NotNull(response.ErrorInfo.Suggested);
    }

    [Fact]
    public async Task KeyNotFoundException_IsConvertedToStaleNode()
    {
        var handler = new StubHandler("test_keynotfound",
            _ => throw new KeyNotFoundException("node 99 missing"));

        var response = await Dispatch(handler, "test_keynotfound");

        Assert.NotNull(response.ErrorInfo);
        Assert.Equal(DiagnosticErrorCodes.StaleNode, response.ErrorInfo!.Code);
        Assert.Contains("get_snapshot", response.ErrorInfo.Suggested!);
        Assert.Equal("node 99 missing", response.Error);
    }

    [Fact]
    public async Task GenericException_IsConvertedToInternal()
    {
        var handler = new StubHandler("test_generic",
            _ => throw new InvalidOperationException("kaboom"));

        var response = await Dispatch(handler, "test_generic");

        Assert.NotNull(response.ErrorInfo);
        Assert.Equal(DiagnosticErrorCodes.Internal, response.ErrorInfo!.Code);
        Assert.Equal("kaboom", response.Error);
    }

    [Fact]
    public async Task HandlerResultError_IsPropagatedAsStructuredFailure()
    {
        var handler = new StubHandler("test_handler_result",
            _ => Task.FromResult<object>(HandlerResult.Error(
                DiagnosticErrorCodes.AmbiguousSelector,
                "matched 3 elements",
                "use a more specific selector",
                new { count = 3 })));

        var response = await Dispatch(handler, "test_handler_result");

        Assert.NotNull(response.ErrorInfo);
        Assert.Equal(DiagnosticErrorCodes.AmbiguousSelector, response.ErrorInfo!.Code);
        Assert.Equal("matched 3 elements", response.Error);
        Assert.Equal("use a more specific selector", response.ErrorInfo.Suggested);
        Assert.Null(response.Result);
    }

    [Fact]
    public async Task HandlerResult_StaleNode_PropagatesWithStableCode()
    {
        var handler = new StubHandler("test_stale",
            _ => Task.FromResult<object>(HandlerResult.StaleNode(7)));

        var response = await Dispatch(handler, "test_stale");

        Assert.NotNull(response.ErrorInfo);
        Assert.Equal(DiagnosticErrorCodes.StaleNode, response.ErrorInfo!.Code);
        Assert.Contains("7", response.Error!);
    }

    [Fact]
    public async Task LegacyAnonymousErrorObject_IsProjectedToStructured()
    {
        var handler = new StubHandler("test_legacy",
            _ => Task.FromResult<object>(new { error = "Node 12 not found" }));

        var response = await Dispatch(handler, "test_legacy");

        Assert.NotNull(response.ErrorInfo);
        Assert.Equal(DiagnosticErrorCodes.StaleNode, response.ErrorInfo!.Code);
        Assert.Equal("Node 12 not found", response.Error);
    }

    [Fact]
    public async Task LegacyArbitraryError_DefaultsToInternal()
    {
        var handler = new StubHandler("test_legacy_other",
            _ => Task.FromResult<object>(new { error = "something else" }));

        var response = await Dispatch(handler, "test_legacy_other");

        Assert.NotNull(response.ErrorInfo);
        Assert.Equal(DiagnosticErrorCodes.Internal, response.ErrorInfo!.Code);
    }

    [Fact]
    public async Task UnknownMethod_ReturnsInvalidParam()
    {
        var dispatcher = new RequestDispatcher();
        var json = await dispatcher.Dispatch(SerializeRequest("definitely_not_a_method"));
        var response = ProtocolSerializer.Deserialize<DiagnosticResponse>(json);

        Assert.NotNull(response);
        Assert.NotNull(response!.ErrorInfo);
        Assert.Equal(DiagnosticErrorCodes.InvalidParam, response.ErrorInfo!.Code);
        Assert.Contains("definitely_not_a_method", response.Error!);
    }

    [Fact]
    public async Task SuccessResult_HasNoErrorInfo()
    {
        var handler = new StubHandler("test_success",
            _ => Task.FromResult<object>(new { ok = true, value = 42 }));

        var response = await Dispatch(handler, "test_success");

        Assert.Null(response.ErrorInfo);
        Assert.Null(response.Error);
        Assert.NotNull(response.Result);
        Assert.True(response.Result!.Value.GetProperty("ok").GetBoolean());
    }
}
