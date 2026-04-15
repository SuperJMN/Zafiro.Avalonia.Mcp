using System.Text.Json;
using Avalonia;
using Avalonia.Platform;
using Avalonia.Threading;
using Zafiro.Avalonia.Mcp.Protocol;
using Zafiro.Avalonia.Mcp.Protocol.Messages;

namespace Zafiro.Avalonia.Mcp.AppHost.Handlers;

/// <summary>
/// Combines find_view_source + open_asset in a single call: finds the AXAML for a runtime control
/// and returns its source code. Saves the AI agent a round-trip.
/// </summary>
public sealed class GetXamlHandler : IRequestHandler
{
    public string Method => ProtocolMethods.GetXaml;

    public async Task<object> Handle(DiagnosticRequest request)
    {
        int nodeId = 0;
        if (request.Params is JsonElement p && p.TryGetProperty("nodeId", out var nid))
            nodeId = nid.GetInt32();

        return await Dispatcher.UIThread.InvokeAsync<object>(() =>
        {
            var visual = NodeRegistry.Resolve(nodeId);
            if (visual is null)
                return new { error = $"Node {nodeId} not found" };

            var controlType = visual.GetType();
            var type = controlType;

            while (type is not null && type != typeof(object))
            {
                var asmName = type.Assembly.GetName().Name;
                var ns = type.Namespace?.Replace('.', '/') ?? "";
                var typeName = type.Name;

                var candidates = new[]
                {
                    $"avares://{asmName}/{ns}/{typeName}.axaml",
                    $"avares://{asmName}/{typeName}.axaml",
                    $"avares://{asmName}/Views/{typeName}.axaml",
                    $"avares://{asmName}/Controls/{typeName}.axaml",
                };

                foreach (var candidate in candidates)
                {
                    try
                    {
                        var uri = new Uri(candidate);
                        using var stream = AssetLoader.Open(uri);
                        using var reader = new StreamReader(stream);
                        var xaml = reader.ReadToEnd();

                        return new
                        {
                            nodeId,
                            controlType = controlType.FullName,
                            matchedType = type.FullName,
                            axamlUrl = candidate,
                            xaml,
                        };
                    }
                    catch
                    {
                        // Asset not found — try next candidate
                    }
                }

                type = type.BaseType;
            }

            return new
            {
                nodeId,
                controlType = controlType.FullName,
                xaml = (string?)null,
                message = "No AXAML source found"
            };
        });
    }
}
