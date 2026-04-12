using System.Text.Json;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Threading;
using AvaloniaMcp.Protocol;
using AvaloniaMcp.Protocol.Messages;

namespace AvaloniaMcp.AppHost.Handlers;

public sealed class ActionHandler : IRequestHandler
{
    public string Method => "action";

    public async Task<object> Handle(DiagnosticRequest request)
    {
        int nodeId = 0;
        string action = "";

        if (request.Params is JsonElement p)
        {
            if (p.TryGetProperty("nodeId", out var nid)) nodeId = nid.GetInt32();
            if (p.TryGetProperty("action", out var a)) action = a.GetString() ?? "";
        }

        return await Dispatcher.UIThread.InvokeAsync<object>(() =>
        {
            var visual = NodeRegistry.Resolve(nodeId);
            if (visual is null) return new { error = $"Node {nodeId} not found" };

            switch (action.ToLowerInvariant())
            {
                case "focus":
                    if (visual is IInputElement ie)
                    {
                        ie.Focus();
                        return new { success = true, action = "focus" };
                    }
                    return new { error = "Element cannot receive focus" };

                case "enable":
                    if (visual is Control ce)
                    {
                        ce.IsEnabled = true;
                        return new { success = true, action = "enable" };
                    }
                    return new { error = "Not a Control" };

                case "disable":
                    if (visual is Control cd)
                    {
                        cd.IsEnabled = false;
                        return new { success = true, action = "disable" };
                    }
                    return new { error = "Not a Control" };

                case "bringintoview":
                    if (visual is Control cb)
                    {
                        cb.BringIntoView();
                        return new { success = true, action = "bringintoview" };
                    }
                    return new { error = "Not a Control" };

                default:
                    return new { error = $"Unknown action: {action}. Supported: Focus, Enable, Disable, BringIntoView" };
            }
        });
    }
}
