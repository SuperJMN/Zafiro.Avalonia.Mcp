using System.Text.Json;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Threading;
using Zafiro.Avalonia.Mcp.AppHost.Selectors;
using Zafiro.Avalonia.Mcp.Protocol;
using Zafiro.Avalonia.Mcp.Protocol.Messages;

namespace Zafiro.Avalonia.Mcp.AppHost.Handlers;

public sealed class ActionHandler : IRequestHandler
{
    public string Method => "action";

    public async Task<object> Handle(DiagnosticRequest request)
    {
        string? selector = null;
        string action = "";

        if (request.Params is JsonElement p)
        {
            if (p.TryGetProperty("selector", out var s)) selector = s.GetString();
            if (p.TryGetProperty("action", out var a)) action = a.GetString() ?? "";
        }

        return await Dispatcher.UIThread.InvokeAsync<object>(() =>
        {
            var (visual, error) = SelectorRequestHelper.ResolveSingle(selector);
            if (visual is null) return error!;
            return Perform(visual, action);
        });
    }

    internal static object Perform(Visual visual, string action)
    {
        var nodeId = NodeRegistry.GetOrRegister(visual);
        switch (action.ToLowerInvariant())
        {
            case "focus":
                if (visual is IInputElement ie)
                {
                    ie.Focus();
                    return new { success = true, nodeId, action = "focus" };
                }
                return new { error = "Element cannot receive focus", nodeId };

            case "enable":
                if (visual is Control ce)
                {
                    ce.IsEnabled = true;
                    return new { success = true, nodeId, action = "enable" };
                }
                return new { error = "Not a Control", nodeId };

            case "disable":
                if (visual is Control cd)
                {
                    cd.IsEnabled = false;
                    return new { success = true, nodeId, action = "disable" };
                }
                return new { error = "Not a Control", nodeId };

            case "bringintoview":
                if (visual is Control cb)
                {
                    cb.BringIntoView();
                    return new { success = true, nodeId, action = "bringintoview" };
                }
                return new { error = "Not a Control", nodeId };

            default:
                return new { error = $"Unknown action: {action}. Supported: Focus, Enable, Disable, BringIntoView", nodeId };
        }
    }
}
