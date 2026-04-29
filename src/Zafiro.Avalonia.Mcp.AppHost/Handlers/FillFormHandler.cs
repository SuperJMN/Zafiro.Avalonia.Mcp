using System.Text.Json;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Threading;
using Zafiro.Avalonia.Mcp.AppHost.Selectors;
using Zafiro.Avalonia.Mcp.Protocol;
using Zafiro.Avalonia.Mcp.Protocol.Messages;
using Zafiro.Avalonia.Mcp.Protocol.Selectors;

namespace Zafiro.Avalonia.Mcp.AppHost.Handlers;

/// <summary>
/// Composite handler that fills multiple form fields in a single MCP round-trip and
/// optionally clicks a submit selector at the end. Per-field errors do NOT abort the batch
/// so the AI agent gets a complete picture of what succeeded and what failed.
/// </summary>
public sealed class FillFormHandler : IRequestHandler
{
    public string Method => ProtocolMethods.FillForm;

    public async Task<object> Handle(DiagnosticRequest request)
    {
        var (fields, submit, parseError) = ParseRequest(request.Params);
        if (parseError is not null)
            return HandlerResult.Error(parseError.Code, parseError.Message, parseError.Suggested, parseError.Details);

        return await Dispatcher.UIThread.InvokeAsync<object>(() =>
            FillFormCore(fields!, submit, ResolveSelector));
    }

    /// <summary>
    /// Pure core: drives the per-field execution against a caller-supplied resolver. Exposed
    /// for tests so they can construct controls in-memory and bypass the dispatcher entirely.
    /// </summary>
    internal static FillFormResponse FillFormCore(
        IReadOnlyList<FieldRequest> fields,
        string? submit,
        Func<string, (Visual? visual, DiagnosticError? error)> resolver)
    {
        var results = new List<FieldResult>(fields.Count);
        var success = 0;
        var failure = 0;

        foreach (var field in fields)
        {
            var result = ExecuteField(field, resolver);
            results.Add(result);
            if (result.Ok) success++; else failure++;
        }

        SubmitResult? submitResult = null;
        if (!string.IsNullOrWhiteSpace(submit))
        {
            var (visual, error) = resolver(submit);
            if (visual is null)
            {
                submitResult = new SubmitResult(false, error);
            }
            else
            {
                try
                {
                    InputHandler.Click(visual);
                    submitResult = new SubmitResult(true);
                }
                catch (Exception ex)
                {
                    submitResult = new SubmitResult(false, new DiagnosticError(
                        ex.Message, DiagnosticErrorCodes.Internal));
                }
            }
        }

        return new FillFormResponse(results, success, failure, submitResult);
    }

    private static FieldResult ExecuteField(
        FieldRequest field,
        Func<string, (Visual? visual, DiagnosticError? error)> resolver)
    {
        if (string.IsNullOrWhiteSpace(field.Selector))
        {
            return FieldResult.Failure("", new DiagnosticError(
                "selector is required",
                DiagnosticErrorCodes.InvalidParam,
                "Each field must have a non-empty 'selector'."));
        }

        // Decide which value-key was supplied (priority: value > checked > select > number).
        var hasValue = field.Value is not null;
        var hasChecked = field.Checked is not null;
        var hasSelect = field.SelectText is not null || field.SelectIndex is not null;
        var hasNumber = field.Number is not null;

        if (!hasValue && !hasChecked && !hasSelect && !hasNumber)
        {
            return FieldResult.Failure(field.Selector, new DiagnosticError(
                "field has no recognised value-key (value, checked, select, number)",
                DiagnosticErrorCodes.InvalidParam,
                "Supply exactly one of: value, checked, select, number."));
        }

        var (visual, resolveError) = resolver(field.Selector);
        if (visual is null)
            return FieldResult.Failure(field.Selector, resolveError ?? new DiagnosticError(
                "selector did not resolve", DiagnosticErrorCodes.NoMatch));

        var nodeId = NodeRegistry.GetOrRegister(visual);

        if (hasValue)
            return ApplyValue(field, visual, nodeId);
        if (hasChecked)
            return ApplyChecked(field, visual, nodeId);
        if (hasSelect)
            return ApplySelect(field, visual, nodeId);
        return ApplyNumber(field, visual, nodeId);
    }

    private static FieldResult ApplyValue(FieldRequest field, Visual visual, int nodeId)
    {
        // Reject controls that are clearly not text-input hosts so we return a clean type-mismatch.
        if (visual is ToggleButton or ToggleSwitch or Slider or ProgressBar or NumericUpDown
            or SelectingItemsControl)
        {
            return UnsupportedFor(field, visual, nodeId, "value");
        }

        var raw = TextInputHandler.TextInput(visual, field.Value, pressEnter: false);
        if (TryGetError(raw) is { } err)
            return FieldResult.Failure(field.Selector, new DiagnosticError(
                err, DiagnosticErrorCodes.UnsupportedOperation, null, new { nodeId }));

        return FieldResult.Success(field.Selector, nodeId, RedactedApplied("value", field.Secret));
    }

    private static FieldResult ApplyChecked(FieldRequest field, Visual visual, int nodeId)
    {
        if (visual is not ToggleButton && visual is not ToggleSwitch)
            return UnsupportedFor(field, visual, nodeId, "checked");

        var raw = ToggleHandler.Toggle(visual, field.Checked);
        if (TryGetError(raw) is { } err)
            return FieldResult.Failure(field.Selector, new DiagnosticError(
                err, DiagnosticErrorCodes.UnsupportedOperation, null, new { nodeId }));

        return FieldResult.Success(field.Selector, nodeId, "checked");
    }

    private static FieldResult ApplySelect(FieldRequest field, Visual visual, int nodeId)
    {
        var raw = SelectionHandler.Select(visual, field.SelectIndex, field.SelectText);
        if (TryGetError(raw) is { } err)
        {
            // SelectionHandler returns "did not resolve to a SelectingItemsControl" for type mismatch.
            var code = err.Contains("SelectingItemsControl", StringComparison.OrdinalIgnoreCase)
                ? DiagnosticErrorCodes.UnsupportedOperation
                : DiagnosticErrorCodes.InvalidParam;
            return FieldResult.Failure(field.Selector, new DiagnosticError(err, code, null, new { nodeId }));
        }

        return FieldResult.Success(field.Selector, nodeId, "select");
    }

    private static FieldResult ApplyNumber(FieldRequest field, Visual visual, int nodeId)
    {
        if (visual is not Slider && visual is not ProgressBar && visual is not NumericUpDown)
            return UnsupportedFor(field, visual, nodeId, "number");

        var raw = SetValueHandler.SetValue(visual, field.Number!.Value);
        if (TryGetError(raw) is { } err)
            return FieldResult.Failure(field.Selector, new DiagnosticError(
                err, DiagnosticErrorCodes.UnsupportedOperation, null, new { nodeId }));

        return FieldResult.Success(field.Selector, nodeId, "number");
    }

    private static FieldResult UnsupportedFor(FieldRequest field, Visual visual, int nodeId, string applied)
    {
        return FieldResult.Failure(field.Selector, new DiagnosticError(
            $"value-key '{applied}' is not supported on element type '{visual.GetType().Name}'.",
            DiagnosticErrorCodes.UnsupportedOperation,
            null,
            new { nodeId, elementType = visual.GetType().Name, applied }));
    }

    private static string RedactedApplied(string applied, bool secret)
        => secret ? $"{applied} (redacted)" : applied;

    /// <summary>
    /// Helpers (Toggle/SetValue/etc.) return either a success anonymous object or
    /// <c>new { error = "...", nodeId }</c>. This pulls out the message if present.
    /// </summary>
    private static string? TryGetError(object? result)
    {
        if (result is null) return null;
        var prop = result.GetType().GetProperty("error");
        if (prop is null) return null;
        return prop.GetValue(result) as string;
    }

    private static (Visual? visual, DiagnosticError? error) ResolveSelector(string selector)
    {
        if (string.IsNullOrWhiteSpace(selector))
            return (null, new DiagnosticError(
                "selector is required",
                DiagnosticErrorCodes.InvalidParam));

        IReadOnlyList<Visual> matches;
        try
        {
            matches = SelectorEngine.Default.Resolve(selector);
        }
        catch (SelectorParseException ex)
        {
            return (null, new DiagnosticError(
                ex.Message,
                DiagnosticErrorCodes.InvalidSelector,
                "Check selector syntax (e.g. type, #name, .class, [property=value]).",
                new { position = ex.Position, selector }));
        }

        if (matches.Count == 0)
            return (null, new DiagnosticError(
                "no element matched selector",
                DiagnosticErrorCodes.NoMatch,
                "Refine the selector or call get_snapshot to see available elements.",
                new { selector }));

        if (matches.Count > 1)
            return (null, new DiagnosticError(
                "selector matched more than one element",
                DiagnosticErrorCodes.AmbiguousSelector,
                "Tighten the selector (e.g. add #name or :nth-of-type) so it resolves to a single element.",
                new { selector, count = matches.Count }));

        return (matches[0], null);
    }

    internal static (List<FieldRequest>? fields, string? submit, DiagnosticError? error) ParseRequest(JsonElement? paramsElement)
    {
        if (paramsElement is not JsonElement p || p.ValueKind != JsonValueKind.Object)
            return (null, null, new DiagnosticError(
                "params must be a JSON object with a 'fields' array",
                DiagnosticErrorCodes.InvalidParam,
                "Send {\"fields\":[...], \"submit\"?: \"...\"}."));

        if (!p.TryGetProperty("fields", out var fieldsEl) || fieldsEl.ValueKind != JsonValueKind.Array)
            return (null, null, new DiagnosticError(
                "'fields' must be an array",
                DiagnosticErrorCodes.InvalidParam,
                "Send a non-empty 'fields' array of {selector, value|checked|select|number} objects."));

        var fields = new List<FieldRequest>(fieldsEl.GetArrayLength());
        foreach (var item in fieldsEl.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Object)
                return (null, null, new DiagnosticError(
                    "each entry in 'fields' must be an object",
                    DiagnosticErrorCodes.InvalidParam));

            string selector = item.TryGetProperty("selector", out var s) ? s.GetString() ?? "" : "";
            string? value = item.TryGetProperty("value", out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;
            bool? @checked = item.TryGetProperty("checked", out var c) && (c.ValueKind == JsonValueKind.True || c.ValueKind == JsonValueKind.False) ? c.GetBoolean() : null;
            int? selectIndex = null;
            string? selectText = null;
            if (item.TryGetProperty("select", out var sel))
            {
                if (sel.ValueKind == JsonValueKind.Number && sel.TryGetInt32(out var idx)) selectIndex = idx;
                else if (sel.ValueKind == JsonValueKind.String) selectText = sel.GetString();
            }
            double? number = item.TryGetProperty("number", out var n) && n.ValueKind == JsonValueKind.Number ? n.GetDouble() : null;
            bool secret = item.TryGetProperty("secret", out var sec) && sec.ValueKind == JsonValueKind.True;

            fields.Add(new FieldRequest(selector, value, @checked, selectText, selectIndex, number, secret));
        }

        string? submit = p.TryGetProperty("submit", out var sub) && sub.ValueKind == JsonValueKind.String ? sub.GetString() : null;
        return (fields, submit, null);
    }
}

/// <summary>One field instruction inside a fill_form request.</summary>
public sealed record FieldRequest(
    string Selector,
    string? Value = null,
    bool? Checked = null,
    string? SelectText = null,
    int? SelectIndex = null,
    double? Number = null,
    bool Secret = false);

/// <summary>Per-field outcome in the fill_form response.</summary>
public sealed record FieldResult(
    string Selector,
    bool Ok,
    int? NodeId = null,
    string? Applied = null,
    DiagnosticError? ErrorInfo = null)
{
    public static FieldResult Success(string selector, int nodeId, string applied)
        => new(selector, true, nodeId, applied);

    public static FieldResult Failure(string selector, DiagnosticError error)
        => new(selector, false, null, null, error);
}

/// <summary>Outcome of the optional 'submit' click in a fill_form response.</summary>
public sealed record SubmitResult(bool Ok, DiagnosticError? ErrorInfo = null);

/// <summary>Top-level fill_form response.</summary>
public sealed record FillFormResponse(
    IReadOnlyList<FieldResult> Results,
    int SuccessCount,
    int FailureCount,
    SubmitResult? Submit = null);
