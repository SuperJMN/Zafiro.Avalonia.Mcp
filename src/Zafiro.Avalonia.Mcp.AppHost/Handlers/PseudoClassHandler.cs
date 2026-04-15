using System.Text.Json;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;
using Zafiro.Avalonia.Mcp.Protocol;
using Zafiro.Avalonia.Mcp.Protocol.Messages;

namespace Zafiro.Avalonia.Mcp.AppHost.Handlers;

public sealed class PseudoClassHandler : IRequestHandler
{
    public string Method => ProtocolMethods.GetPseudoClasses;

    public async Task<object> Handle(DiagnosticRequest request)
    {
        int nodeId = 0;
        string? pseudoClass = null;
        bool? isActive = null;

        if (request.Params is JsonElement p)
        {
            if (p.TryGetProperty("nodeId", out var nid)) nodeId = nid.GetInt32();
            if (p.TryGetProperty("pseudoClass", out var pc)) pseudoClass = pc.GetString();
            if (p.TryGetProperty("isActive", out var ia)) isActive = ia.GetBoolean();
        }

        return await Dispatcher.UIThread.InvokeAsync<object>(() =>
        {
            var visual = NodeRegistry.Resolve(nodeId);
            if (visual is not StyledElement styled) return new { error = $"Node {nodeId} not found or not a StyledElement" };

            // List pseudo-classes (classes starting with ':')
            if (pseudoClass is null)
            {
                var classes = styled.Classes.Where(c => c.StartsWith(':')).ToList();
                return new { nodeId, pseudoClasses = classes };
            }

            var fullName = pseudoClass.StartsWith(':') ? pseudoClass : $":{pseudoClass}";

            // Set or unset pseudo-class
            if (isActive.HasValue)
            {
                // Use IPseudoClasses to properly activate framework-managed pseudo-classes
                // like :pointerover, :pressed, :focus, :disabled etc.
                ((IPseudoClasses)styled.Classes).Set(fullName, isActive.Value);
                return new { success = true, pseudoClass = fullName, isActive = isActive.Value };
            }

            // Query current state
            var current = styled.Classes.Contains(fullName);
            return new { pseudoClass = fullName, isActive = current };
        });
    }
}
