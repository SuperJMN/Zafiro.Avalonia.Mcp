namespace Zafiro.Avalonia.Mcp.AppHost;

public enum TransportKind
{
    /// <summary>NamedPipe on desktop (Windows/Linux/macOS), TCP loopback on Android.</summary>
    Auto,
    /// <summary>Force NamedPipes (will fail in Android sandboxes).</summary>
    NamedPipe,
    /// <summary>Force TCP loopback. Useful for cross-process debugging on desktop, mandatory on Android.</summary>
    Tcp
}

public sealed class McpDiagnosticsOptions
{
    public TransportKind Transport { get; set; } = TransportKind.Auto;
}
