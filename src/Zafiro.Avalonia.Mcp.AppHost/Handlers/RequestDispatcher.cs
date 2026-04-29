using System.Reflection;
using Zafiro.Avalonia.Mcp.Protocol;
using Zafiro.Avalonia.Mcp.Protocol.Messages;
using Zafiro.Avalonia.Mcp.Protocol.Selectors;

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
        Register(new FindByDataContextHandler());

        // Phase 6 diagnostics
        Register(new GetItemHandler());
        Register(new LayoutInfoHandler());
        Register(new ValidationErrorsHandler());
        Register(new CommandInfoHandler());

        // Phase 6.9 — global UI state
        Register(new FocusHandler());
        Register(new ActiveWindowHandler());
        Register(new OpenDialogsHandler());

        // Phase 6.11 — composite form filling
        Register(new FillFormHandler());

        // Phase 6.12 — event subscription / long-poll
        Register(new SubscribeHandler());
        Register(new PollEventsHandler());
        Register(new UnsubscribeHandler());
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
                DiagnosticResponse.Failure("unknown", new DiagnosticError(
                    "Invalid JSON",
                    DiagnosticErrorCodes.InvalidParam,
                    "Send a well-formed DiagnosticRequest JSON object.")));
        }

        if (request is null)
        {
            return ProtocolSerializer.Serialize(
                DiagnosticResponse.Failure("unknown", new DiagnosticError(
                    "Empty request",
                    DiagnosticErrorCodes.InvalidParam,
                    "Send a non-empty DiagnosticRequest JSON object.")));
        }

        if (_handlers.TryGetValue(request.Method, out var handler))
        {
            try
            {
                var result = await handler.Handle(request);

                if (result is HandlerErrorResult err)
                {
                    return ProtocolSerializer.Serialize(
                        DiagnosticResponse.Failure(request.Id, err.Error));
                }

                if (TryProjectLegacyError(result) is { } legacy)
                {
                    return ProtocolSerializer.Serialize(
                        DiagnosticResponse.Failure(request.Id, legacy));
                }

                return ProtocolSerializer.Serialize(
                    DiagnosticResponse.Success(request.Id, ProtocolSerializer.ToElement(result)));
            }
            catch (SelectorParseException ex)
            {
                return ProtocolSerializer.Serialize(
                    DiagnosticResponse.Failure(request.Id, new DiagnosticError(
                        ex.Message,
                        DiagnosticErrorCodes.InvalidSelector,
                        "Check selector syntax (e.g. type, #name, .class, [property=value]).",
                        new { position = ex.Position })));
            }
            catch (KeyNotFoundException ex)
            {
                return ProtocolSerializer.Serialize(
                    DiagnosticResponse.Failure(request.Id, new DiagnosticError(
                        ex.Message,
                        DiagnosticErrorCodes.StaleNode,
                        "Call get_snapshot, search, or get_interactables to refresh node IDs.")));
            }
            catch (TimeoutException ex)
            {
                return ProtocolSerializer.Serialize(
                    DiagnosticResponse.Failure(request.Id, new DiagnosticError(
                        ex.Message,
                        DiagnosticErrorCodes.Timeout,
                        "Increase the timeout or wait for the precondition before retrying.")));
            }
            catch (ArgumentException ex)
            {
                return ProtocolSerializer.Serialize(
                    DiagnosticResponse.Failure(request.Id, new DiagnosticError(
                        ex.Message,
                        DiagnosticErrorCodes.InvalidParam,
                        ex.ParamName is { Length: > 0 } p ? $"Provide a valid value for '{p}'." : null)));
            }
            catch (Exception ex)
            {
                return ProtocolSerializer.Serialize(
                    DiagnosticResponse.Failure(request.Id, new DiagnosticError(
                        ex.Message,
                        DiagnosticErrorCodes.Internal)));
            }
        }

        return ProtocolSerializer.Serialize(
            DiagnosticResponse.Failure(request.Id, new DiagnosticError(
                $"Unknown method: {request.Method}",
                DiagnosticErrorCodes.InvalidParam,
                "Check the MCP tool name; the AppHost did not register a handler for this method.")));
    }

    /// <summary>
    /// Bridges legacy handlers that returned <c>new { error = "..." }</c> on failure into the
    /// structured <see cref="DiagnosticError"/> pipeline. Heuristically maps stale-node phrasing
    /// to <see cref="DiagnosticErrorCodes.StaleNode"/>; everything else falls back to
    /// <see cref="DiagnosticErrorCodes.Internal"/>. New handlers should return
    /// <see cref="HandlerErrorResult"/> via <see cref="HandlerResult"/> instead.
    /// </summary>
    private static DiagnosticError? TryProjectLegacyError(object? result)
    {
        if (result is null) return null;
        var type = result.GetType();
        var prop = type.GetProperty("error", BindingFlags.Public | BindingFlags.Instance);
        if (prop is null || prop.PropertyType != typeof(string)) return null;
        if (prop.GetValue(result) is not string message || string.IsNullOrEmpty(message)) return null;

        var lower = message.ToLowerInvariant();
        var code = lower.Contains("not found") || lower.Contains("stale") || lower.Contains("garbage collected")
            ? DiagnosticErrorCodes.StaleNode
            : DiagnosticErrorCodes.Internal;

        return new DiagnosticError(
            message,
            code,
            code == DiagnosticErrorCodes.StaleNode
                ? "Call get_snapshot, search, or get_interactables to refresh node IDs."
                : null);
    }
}
