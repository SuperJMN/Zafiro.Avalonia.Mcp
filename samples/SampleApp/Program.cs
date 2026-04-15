using Avalonia;
using System;
using Zafiro.Avalonia.Mcp.AppHost;

namespace SampleApp;

class Program
{
    [STAThread]
    public static void Main(string[] args) => BuildAvaloniaApp()
        .StartWithClassicDesktopLifetime(args);

    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .UseMcpDiagnostics()
            .WithInterFont()
            .LogToTrace();
}
