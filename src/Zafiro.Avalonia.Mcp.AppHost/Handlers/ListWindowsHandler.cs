using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Threading;
using Zafiro.Avalonia.Mcp.Protocol;
using Zafiro.Avalonia.Mcp.Protocol.Messages;
using Zafiro.Avalonia.Mcp.Protocol.Models;

namespace Zafiro.Avalonia.Mcp.AppHost.Handlers;

public sealed class ListWindowsHandler : IRequestHandler
{
    public string Method => ProtocolMethods.ListWindows;

    public async Task<object> Handle(DiagnosticRequest request)
    {
        return await Dispatcher.UIThread.InvokeAsync<object>(() =>
        {
            var windows = new List<object>();

            if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                foreach (var window in desktop.Windows)
                {
                    windows.Add(new
                    {
                        title = window.Title ?? "(untitled)",
                        type = window.GetType().Name,
                        width = window.Bounds.Width,
                        height = window.Bounds.Height,
                        isActive = window.IsActive
                    });
                }
            }

            return windows;
        });
    }
}
