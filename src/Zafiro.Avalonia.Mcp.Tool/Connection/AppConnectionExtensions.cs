using System.Text;
using System.Text.Json;
using ModelContextProtocol.Protocol;

namespace Zafiro.Avalonia.Mcp.Tool.Connection;

internal static class AppConnectionExtensions
{
    /// <summary>
    /// Sends a request and returns the result as a string.
    /// Catches all exceptions and returns them as "Error: ..." text so the AI
    /// always sees the actual failure reason instead of the generic MCP error wrapper.
    /// Structured <see cref="McpRemoteException"/> responses include the stable error code
    /// and the recovery hint suggested by the AppHost handler.
    /// </summary>
    public static async Task<string> InvokeAsync(
        this AppConnection conn,
        string method,
        object? parameters = null,
        string empty = "No result")
    {
        try
        {
            var result = await conn.SendAsync(method, parameters);
            return result?.ToString() ?? empty;
        }
        catch (McpRemoteException ex)
        {
            return FormatRemoteError(ex);
        }
        catch (Exception ex)
        {
            return $"Error: {ex.Message}";
        }
    }

    /// <summary>
    /// Sends a request and returns rich content blocks (for image/binary results).
    /// On error returns a single TextContentBlock with the error message.
    /// </summary>
    public static async Task<IReadOnlyList<ContentBlock>> InvokeRichAsync(
        this AppConnection conn,
        string method,
        object? parameters,
        Func<JsonElement, IReadOnlyList<ContentBlock>> onSuccess)
    {
        try
        {
            var result = await conn.SendAsync(method, parameters);
            if (result is null)
                return [new TextContentBlock { Text = "No result" }];
            return onSuccess(result.Value);
        }
        catch (McpRemoteException ex)
        {
            return [new TextContentBlock { Text = FormatRemoteError(ex) }];
        }
        catch (Exception ex)
        {
            return [new TextContentBlock { Text = $"Error: {ex.Message}" }];
        }
    }

    private static string FormatRemoteError(McpRemoteException ex)
    {
        var sb = new StringBuilder("Error");
        if (ex.ErrorInfo?.Code is { Length: > 0 } code)
            sb.Append(" [").Append(code).Append(']');
        sb.Append(": ").Append(ex.Message);
        if (ex.ErrorInfo?.Suggested is { Length: > 0 } hint)
            sb.Append("\nHint: ").Append(hint);
        return sb.ToString();
    }
}
