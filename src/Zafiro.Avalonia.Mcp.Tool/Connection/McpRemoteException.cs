using Zafiro.Avalonia.Mcp.Protocol.Messages;

namespace Zafiro.Avalonia.Mcp.Tool.Connection;

/// <summary>
/// Thrown by <see cref="AppConnection"/> when the remote AppHost replied with a structured
/// <see cref="DiagnosticError"/>. Carries the recovery hint so the tool layer can surface it to the AI.
/// </summary>
public sealed class McpRemoteException : Exception
{
    public DiagnosticError? ErrorInfo { get; }

    public McpRemoteException(string message, DiagnosticError? info)
        : base(message)
    {
        ErrorInfo = info;
    }
}
