using Avalonia.Controls;
using Avalonia.Threading;
using Zafiro.Avalonia.Mcp.Protocol;
using Zafiro.Avalonia.Mcp.Protocol.Messages;

namespace Zafiro.Avalonia.Mcp.AppHost.Handlers;

/// <summary>
/// Returns currently open dialog/modal windows.
/// Heuristic: a window is treated as a dialog when its <see cref="Window.Owner"/> property is non-null.
/// This covers the common Avalonia pattern where <c>ShowDialog(owner)</c> sets the Owner automatically.
/// Windows opened with plain <c>Show()</c> (no owner) are NOT included even if they appear modal.
/// The heuristic may miss dialogs opened via third-party modal overlays or custom popup hosts.
/// </summary>
public sealed class OpenDialogsHandler : IRequestHandler
{
    public string Method => ProtocolMethods.GetOpenDialogs;

    public async Task<object> Handle(DiagnosticRequest request)
    {
        return await Dispatcher.UIThread.InvokeAsync<object>(() =>
        {
            var dialogs = new List<object>();

            foreach (var window in NodeRegistry.GetWindows())
            {
                if (window.Owner is not Window ownerWindow)
                    continue;

                var nodeId = NodeRegistry.GetOrRegister(window);
                var ownerNodeId = NodeRegistry.GetOrRegister(ownerWindow);

                dialogs.Add(new
                {
                    nodeId,
                    title = window.Title ?? "(untitled)",
                    isModal = true,
                    owner = new
                    {
                        nodeId = ownerNodeId,
                        title = ownerWindow.Title ?? "(untitled)"
                    }
                });
            }

            return (object)dialogs;
        });
    }
}
