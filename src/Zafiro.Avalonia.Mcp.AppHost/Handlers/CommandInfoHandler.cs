using System.Reflection;
using System.Text.Json;
using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Zafiro.Avalonia.Mcp.AppHost.Selectors;
using Zafiro.Avalonia.Mcp.Protocol;
using Zafiro.Avalonia.Mcp.Protocol.Messages;

namespace Zafiro.Avalonia.Mcp.AppHost.Handlers;

/// <summary>
/// Returns ICommand state for any control that exposes a Command property.
/// Explains why a button is greyed out: command CanExecute, IsEnabled, or parent disabled.
/// </summary>
public sealed class CommandInfoHandler : IRequestHandler
{
    public string Method => ProtocolMethods.GetCommandInfo;

    public Task<object> Handle(DiagnosticRequest request)
    {
        string? selector = null;
        if (request.Params is JsonElement p && p.TryGetProperty("selector", out var s))
            selector = s.GetString();

        if (string.IsNullOrWhiteSpace(selector))
            return Task.FromResult<object>(new { error = "selector is required" });

        if (Dispatcher.UIThread.CheckAccess())
            return Task.FromResult(Resolve(selector!));

        return Dispatcher.UIThread.InvokeAsync<object>(() => Resolve(selector!)).GetTask();
    }

    private static object Resolve(string selector)
    {
        var matches = new SelectorEngine().Resolve(selector);
        if (matches.Count == 0)
            return new { error = $"No elements matched selector: {selector}" };

        var visual = matches[0];
        var matchedCount = matches.Count > 1 ? (int?)matches.Count : null;
        var nodeId = NodeRegistry.GetOrRegister(visual);

        var result = Analyze(visual);

        if (matchedCount is not null)
            return new { nodeId, result.commandType, result.parameter, result.canExecute, result.isEnabled, result.enableReason, matchedCount, error = result.commandError };

        return (object)new { nodeId, result.commandType, result.parameter, result.canExecute, result.isEnabled, result.enableReason, error = result.commandError };
    }

    /// <summary>
    /// Analyzes the command state of a visual. Can be called directly in tests without a dispatcher.
    /// </summary>
    public static (string? commandType, string? parameter, bool? canExecute, bool isEnabled, string enableReason, string? commandError) Analyze(Visual visual)
    {
        var type = visual.GetType();
        var commandProp = type.GetProperty("Command",
            BindingFlags.Public | BindingFlags.Instance | BindingFlags.FlattenHierarchy);
        var paramProp = type.GetProperty("CommandParameter",
            BindingFlags.Public | BindingFlags.Instance | BindingFlags.FlattenHierarchy);

        ICommand? command = null;
        object? parameter = null;
        string? commandError = null;

        if (commandProp is not null && typeof(ICommand).IsAssignableFrom(commandProp.PropertyType))
        {
            try { command = commandProp.GetValue(visual) as ICommand; }
            catch (Exception ex) { commandError = ex.Message; }
        }

        if (paramProp is not null)
        {
            try { parameter = paramProp.GetValue(visual); }
            catch { /* ignore */ }
        }

        string? commandType = command?.GetType().FullName;
        string? parameterStr = parameter?.ToString();

        bool? canExecute = null;
        if (command is not null)
        {
            try { canExecute = command.CanExecute(parameter); }
            catch (Exception ex) { commandError ??= ex.Message; }
        }

        // Check own IsEnabled and parent IsEnabled explicitly so this works even without a live visual tree.
        bool ownEnabled = visual is not InputElement ownIe || ownIe.IsEnabled;
        bool parentDisabled = visual.GetVisualAncestors()
            .OfType<InputElement>()
            .Any(a => !a.IsEnabled);
        bool isEnabled = ownEnabled && !parentDisabled;

        string enableReason;
        if (command != null && canExecute == false)
        {
            enableReason = "command_cannot_execute";
        }
        else if (!isEnabled)
        {
            enableReason = parentDisabled ? "parent_disabled" : "is_enabled_false";
        }
        else
        {
            enableReason = "enabled";
        }

        return (commandType, parameterStr, canExecute, isEnabled, enableReason, commandError);
    }
}
