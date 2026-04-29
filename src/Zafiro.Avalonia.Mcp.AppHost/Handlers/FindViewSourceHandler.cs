using System.Text.Json;
using Avalonia;
using Avalonia.Platform;
using Avalonia.Threading;
using Zafiro.Avalonia.Mcp.AppHost.Selectors;
using Zafiro.Avalonia.Mcp.Protocol;
using Zafiro.Avalonia.Mcp.Protocol.Messages;

namespace Zafiro.Avalonia.Mcp.AppHost.Handlers;

/// <summary>
/// Maps a runtime control to its AXAML source file (avares:// URL).
/// </summary>
public sealed class FindViewSourceHandler : IRequestHandler
{
    public string Method => ProtocolMethods.FindViewSource;

    public async Task<object> Handle(DiagnosticRequest request)
    {
        string? selector = null;
        if (request.Params is JsonElement p && p.TryGetProperty("selector", out var s))
            selector = s.GetString();

        return await Dispatcher.UIThread.InvokeAsync<object>(() =>
        {
            var (visual, error) = SelectorRequestHelper.ResolveSingle(selector);
            if (visual is null) return error!;
            return Find(visual);
        });
    }

    internal static object Find(Visual visual)
    {
        var nodeId = NodeRegistry.GetOrRegister(visual);
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
                    AssetLoader.Open(uri).Dispose();
                    return new
                    {
                        nodeId,
                        controlType = controlType.FullName,
                        matchedType = type.FullName,
                        axamlUrl = candidate,
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
            axamlUrl = (string?)null,
            message = "No AXAML source found for this control or its base types"
        };
    }
}
