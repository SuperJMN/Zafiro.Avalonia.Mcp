using Avalonia;

namespace AvaloniaMcp.AppHost;

public static class DiagnosticExtensions
{
    private static DiagnosticServer? _server;

    public static AppBuilder UseMcpDiagnostics(this AppBuilder builder)
    {
        builder.AfterSetup(_ =>
        {
            _server = new DiagnosticServer();
            _server.Start();
        });

        return builder;
    }

    public static void StopMcpDiagnostics()
    {
        _server?.Dispose();
        _server = null;
    }
}
