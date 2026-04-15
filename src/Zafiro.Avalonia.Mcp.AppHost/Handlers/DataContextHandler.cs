using System.Reflection;
using System.Text.Json;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Threading;
using Zafiro.Avalonia.Mcp.Protocol;
using Zafiro.Avalonia.Mcp.Protocol.Messages;

namespace Zafiro.Avalonia.Mcp.AppHost.Handlers;

/// <summary>
/// Returns the DataContext of a control as a reflected object summary.
/// Designed for AI consumers: type name, properties with values (truncated), and commands.
/// </summary>
public sealed class DataContextHandler : IRequestHandler
{
    public string Method => ProtocolMethods.GetDataContext;

    public async Task<object> Handle(DiagnosticRequest request)
    {
        int nodeId = 0;
        if (request.Params is JsonElement p && p.TryGetProperty("nodeId", out var nid))
            nodeId = nid.GetInt32();

        return await Dispatcher.UIThread.InvokeAsync<object>(() =>
        {
            var visual = NodeRegistry.Resolve(nodeId);
            if (visual is not StyledElement styled)
                return new { error = $"Node {nodeId} not found or not a StyledElement" };

            var dc = styled.DataContext;
            if (dc is null)
                return new { nodeId, dataContext = (object?)null, message = "DataContext is null" };

            var dcType = dc.GetType();
            var props = dcType.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(p2 => p2.GetIndexParameters().Length == 0)
                .Select(p2 =>
                {
                    string? value = null;
                    try
                    {
                        var raw = p2.GetValue(dc);
                        value = raw switch
                        {
                            null => "null",
                            string s when s.Length > 120 => s[..117] + "...",
                            _ => raw.ToString() is { Length: > 120 } ts ? ts[..117] + "..." : raw.ToString()
                        };
                    }
                    catch (Exception ex)
                    {
                        value = $"<error: {ex.Message}>";
                    }

                    return new
                    {
                        name = p2.Name,
                        type = p2.PropertyType.Name,
                        value
                    };
                })
                .ToList();

            return new
            {
                nodeId,
                dataContextType = dcType.FullName,
                properties = props
            };
        });
    }
}
