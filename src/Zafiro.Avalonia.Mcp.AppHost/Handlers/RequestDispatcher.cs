using Zafiro.Avalonia.Mcp.Protocol;
using Zafiro.Avalonia.Mcp.Protocol.Messages;

namespace Zafiro.Avalonia.Mcp.AppHost.Handlers;

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
        Register(new PropertyValuesHandler());

        // Snapshot
        Register(new SnapshotHandler());

        // Input & Interaction
        Register(new InputHandler());
        Register(new TapHandler());
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

        // MVVM / Data inspection
        Register(new DataContextHandler());
        Register(new BindingsHandler());
        Register(new FindViewSourceHandler());
        Register(new GetXamlHandler());
        Register(new DiffTreeHandler());

        // Phase 6 diagnostics
        Register(new GetItemHandler());
        Register(new LayoutInfoHandler());
        Register(new ValidationErrorsHandler());
        Register(new CommandInfoHandler());

        // Phase 6.9 — global UI state
        Register(new FocusHandler());
        Register(new ActiveWindowHandler());
        Register(new OpenDialogsHandler());
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
