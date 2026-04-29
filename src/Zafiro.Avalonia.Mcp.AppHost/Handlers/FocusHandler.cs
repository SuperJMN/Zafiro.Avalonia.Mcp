using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;
using Zafiro.Avalonia.Mcp.Protocol;
using Zafiro.Avalonia.Mcp.Protocol.Messages;

namespace Zafiro.Avalonia.Mcp.AppHost.Handlers;

/// <summary>
/// Returns the currently keyboard-focused element across all open windows.
/// </summary>
public sealed class FocusHandler : IRequestHandler
{
    public string Method => ProtocolMethods.GetFocus;

    public async Task<object> Handle(DiagnosticRequest request)
    {
        return await Dispatcher.UIThread.InvokeAsync<object>(() =>
        {
            foreach (var root in NodeRegistry.GetRoots())
            {
                var focused = root.FocusManager?.GetFocusedElement();
                if (focused is Visual visual)
                {
                    var nodeId = NodeRegistry.GetOrRegister(visual);
                    var windowNodeId = NodeRegistry.GetOrRegister(root);
                    return new
                    {
                        nodeId,
                        type = visual.GetType().Name,
                        name = (visual as Control)?.Name,
                        windowNodeId
                    };
                }
            }

            return (object)new { focused = (object?)null };
        });
    }
}
