using Avalonia;

namespace Zafiro.Avalonia.Mcp.AppHost;

public static class DiagnosticExtensions
{
    private static DiagnosticServer? _server;

    public static AppBuilder UseMcpDiagnostics(this AppBuilder builder)
    {
        builder.AfterSetup(_ =>
        {
            _server = new DiagnosticServer();
            _server.Start();

            AppDomain.CurrentDomain.ProcessExit += (_, _) => StopMcpDiagnostics();
            Console.CancelKeyPress += (_, _) => StopMcpDiagnostics();
        });

        return builder;
    }

    public static void StopMcpDiagnostics()
    {
        _server?.Dispose();
        _server = null;
    }
}
