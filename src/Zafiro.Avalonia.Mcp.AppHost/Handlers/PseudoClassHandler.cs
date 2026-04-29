using System.Text.Json;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;
using Zafiro.Avalonia.Mcp.AppHost.Selectors;
using Zafiro.Avalonia.Mcp.Protocol;
using Zafiro.Avalonia.Mcp.Protocol.Messages;

namespace Zafiro.Avalonia.Mcp.AppHost.Handlers;

public sealed class PseudoClassHandler : IRequestHandler
{
    public string Method => ProtocolMethods.GetPseudoClasses;

    public async Task<object> Handle(DiagnosticRequest request)
    {
        string? selector = null;
        string? pseudoClass = null;
        bool? isActive = null;

        if (request.Params is JsonElement p)
        {
            if (p.TryGetProperty("selector", out var s)) selector = s.GetString();
            if (p.TryGetProperty("pseudoClass", out var pc)) pseudoClass = pc.GetString();
            if (p.TryGetProperty("isActive", out var ia)) isActive = ia.GetBoolean();
        }

        return await Dispatcher.UIThread.InvokeAsync<object>(() =>
        {
            var (visual, error) = SelectorRequestHelper.ResolveSingle(selector);
            if (visual is null) return error!;
            return PseudoClass(visual, pseudoClass, isActive);
        });
    }

    internal static object PseudoClass(Visual visual, string? pseudoClass, bool? isActive)
    {
        var nodeId = NodeRegistry.GetOrRegister(visual);
        if (visual is not StyledElement styled)
            return new { error = "selector did not resolve to a StyledElement", nodeId };

        if (pseudoClass is null)
        {
            var classes = styled.Classes.Where(c => c.StartsWith(':')).ToList();
            return new { nodeId, pseudoClasses = classes };
        }

        var fullName = pseudoClass.StartsWith(':') ? pseudoClass : $":{pseudoClass}";

        if (isActive.HasValue)
        {
            ((IPseudoClasses)styled.Classes).Set(fullName, isActive.Value);
            return new { success = true, nodeId, pseudoClass = fullName, isActive = isActive.Value };
        }

        var current = styled.Classes.Contains(fullName);
        return new { nodeId, pseudoClass = fullName, isActive = current };
    }
}
