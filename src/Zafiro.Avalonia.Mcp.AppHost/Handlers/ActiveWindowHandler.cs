using Avalonia.Controls;
using Avalonia.Threading;
using Zafiro.Avalonia.Mcp.Protocol;
using Zafiro.Avalonia.Mcp.Protocol.Messages;

namespace Zafiro.Avalonia.Mcp.AppHost.Handlers;

/// <summary>
/// Returns the currently active (focused/topmost) window and a list of all open windows.
/// Uses <see cref="Window.IsActive"/> to determine the active window.
/// </summary>
public sealed class ActiveWindowHandler : IRequestHandler
{
    public string Method => ProtocolMethods.GetActiveWindow;

    public async Task<object> Handle(DiagnosticRequest request)
    {
        return await Dispatcher.UIThread.InvokeAsync<object>(() =>
        {
            var windows = NodeRegistry.GetWindows().ToList();

            var openWindows = windows.Select(w => new
            {
                nodeId = NodeRegistry.GetOrRegister(w),
                title = w.Title ?? "(untitled)",
                isActive = w.IsActive
            }).ToList();

            var active = openWindows.FirstOrDefault(w => w.isActive);

            if (active is null)
                return new { activeWindow = (object?)null, openWindows };

            return (object)new { activeWindow = active, openWindows };
        });
    }
}
