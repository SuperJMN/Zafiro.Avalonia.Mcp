using Avalonia;
using Zafiro.Avalonia.Mcp.AppHost.Discovery;
using Zafiro.Avalonia.Mcp.AppHost.Transport;

namespace Zafiro.Avalonia.Mcp.AppHost;

public static class DiagnosticExtensions
{
    private static DiagnosticServer? _server;

    public static AppBuilder UseMcpDiagnostics(this AppBuilder builder)
        => UseMcpDiagnostics(builder, configure: null);

    public static AppBuilder UseMcpDiagnostics(this AppBuilder builder, Action<McpDiagnosticsOptions>? configure)
    {
        builder.AfterSetup(_ =>
        {
            var options = new McpDiagnosticsOptions();
            configure?.Invoke(options);

            var transport = CreateTransport(options.Transport);
            _server = new DiagnosticServer(transport);
            _server.Start();

            AppDomain.CurrentDomain.ProcessExit += (_, _) => StopMcpDiagnostics();
            if (!OperatingSystem.IsAndroid() && !OperatingSystem.IsIOS() && !OperatingSystem.IsBrowser())
            {
                Console.CancelKeyPress += (_, _) => StopMcpDiagnostics();
            }
        });

        return builder;
    }

    private static IDiagnosticTransport CreateTransport(TransportKind kind)
    {
        var resolved = kind == TransportKind.Auto
            ? (IsAndroid() ? TransportKind.Tcp : TransportKind.NamedPipe)
            : kind;

        return resolved switch
        {
            TransportKind.Tcp => new TcpLoopbackTransport(),
            TransportKind.NamedPipe => new NamedPipeTransport(DiscoveryWriter.PipeName),
            _ => new NamedPipeTransport(DiscoveryWriter.PipeName)
        };
    }

    private static bool IsAndroid()
        => OperatingSystem.IsAndroid();

    public static void StopMcpDiagnostics()
    {
        _server?.Dispose();
        _server = null;
    }
}
