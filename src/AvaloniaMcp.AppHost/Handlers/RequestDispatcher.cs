using AvaloniaMcp.Protocol;
using AvaloniaMcp.Protocol.Messages;

namespace AvaloniaMcp.AppHost.Handlers;

public sealed class RequestDispatcher
{
    private readonly Dictionary<string, IRequestHandler> _handlers = new();

    public RequestDispatcher()
    {
        // Connection
        Register(new PingHandler());
        Register(new ListWindowsHandler());

        // Tree inspection
        Register(new TreeHandler());
        Register(new SearchHandler());
        Register(new AncestorsHandler());
        Register(new ScreenTextHandler());
        Register(new InteractablesHandler());

        // Properties & Styles
        Register(new PropertyHandler());
        Register(new SetPropertyHandler());
        Register(new StylesHandler());

        // Input & Interaction
        Register(new InputHandler());
        Register(new KeyboardHandler());
        Register(new KeyUpHandler());
        Register(new TextInputHandler());
        Register(new ActionHandler());
        Register(new PseudoClassHandler());

        // Interaction
        Register(new SelectionHandler());
        Register(new ToggleHandler());
        Register(new SetValueHandler());
        Register(new ScrollHandler());

        // Wait
        Register(new WaitForHandler());
        Register(new ClickAndWaitHandler());
        Register(new ClickByQueryHandler());

        // Capture
        Register(new ScreenshotHandler());
        Register(new RecordingHandler());
        Register(new StopRecordingHandler());

        // Resources
        Register(new ResourceHandler());
        Register(new ListAssetsHandler());
        Register(new OpenAssetHandler());
    }

    private void Register(IRequestHandler handler)
    {
        _handlers[handler.Method] = handler;
    }

    public async Task<string> Dispatch(string json)
    {
        DiagnosticRequest? request;
        try
        {
            request = ProtocolSerializer.Deserialize<DiagnosticRequest>(json);
        }
        catch
        {
            return ProtocolSerializer.Serialize(
                DiagnosticResponse.Failure("unknown", "Invalid JSON"));
        }

        if (request is null)
        {
            return ProtocolSerializer.Serialize(
                DiagnosticResponse.Failure("unknown", "Empty request"));
        }

        if (_handlers.TryGetValue(request.Method, out var handler))
        {
            try
            {
                var result = await handler.Handle(request);
                return ProtocolSerializer.Serialize(
                    DiagnosticResponse.Success(request.Id, ProtocolSerializer.ToElement(result)));
            }
            catch (Exception ex)
            {
                return ProtocolSerializer.Serialize(
                    DiagnosticResponse.Failure(request.Id, ex.Message));
            }
        }

        return ProtocolSerializer.Serialize(
            DiagnosticResponse.Failure(request.Id, $"Unknown method: {request.Method}"));
    }
}
