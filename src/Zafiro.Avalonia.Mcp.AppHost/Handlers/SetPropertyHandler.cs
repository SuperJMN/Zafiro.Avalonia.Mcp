using System.Text.Json;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;
using Zafiro.Avalonia.Mcp.AppHost.Selectors;
using Zafiro.Avalonia.Mcp.Protocol;
using Zafiro.Avalonia.Mcp.Protocol.Messages;

namespace Zafiro.Avalonia.Mcp.AppHost.Handlers;

public sealed class SetPropertyHandler : IRequestHandler
{
    public string Method => ProtocolMethods.SetProperty;

    public async Task<object> Handle(DiagnosticRequest request)
    {
        string? selector = null;
        string propName = "";
        string? value = null;

        if (request.Params is JsonElement p)
        {
            if (p.TryGetProperty("selector", out var s)) selector = s.GetString();
            if (p.TryGetProperty("propertyName", out var pn)) propName = pn.GetString() ?? "";
            if (p.TryGetProperty("value", out var v)) value = v.GetString();
        }

        return await Dispatcher.UIThread.InvokeAsync<object>(() =>
        {
            var (visual, error) = SelectorRequestHelper.ResolveSingle(selector);
            if (visual is null) return error!;
            return SetProperty(visual, propName, value);
        });
    }

    internal static object SetProperty(global::Avalonia.Visual visual, string propName, string? value)
    {
        var nodeId = NodeRegistry.GetOrRegister(visual);
        if (visual is not AvaloniaObject ao) return new { error = $"selector did not resolve to AvaloniaObject", nodeId };

        var prop = FindProperty(ao, propName);
        if (prop is null)
            return HandlerResult.InvalidParam("propertyName", $"Property '{propName}' not found on {visual.GetType().Name}");

        if (value is null or PropertyValueConverter.UnsetToken)
        {
            ao.ClearValue(prop);
            return new { success = true, nodeId, property = prop.Name, action = "unset" };
        }

        if (!PropertyValueConverter.TryConvert(value, prop.PropertyType, out var converted))
        {
            var typeName = PropertyValueConverter.DisplayName(prop.PropertyType);
            var hint = PropertyValueConverter.FormatHint(prop.PropertyType) is { } format
                ? $"Expected {format}."
                : $"Call get_prop_values for the accepted values of {prop.Name}.";
            if (PropertyValueConverter.AcceptsNull(prop.PropertyType))
                hint += $" Use '{PropertyValueConverter.NullToken}' to assign null or 'unset' to clear the local value.";
            return HandlerResult.Error(
                DiagnosticErrorCodes.InvalidParam,
                $"Cannot convert '{value}' to {typeName}",
                hint,
                new { param = "value", property = prop.Name, type = typeName });
        }

        ao.SetValue(prop, converted);
        return converted is null
            ? new { success = true, nodeId, property = prop.Name, action = "null" }
            : (object)new { success = true, nodeId, property = prop.Name, value };
    }

    private static AvaloniaProperty? FindProperty(AvaloniaObject ao, string name) =>
        AvaloniaPropertyRegistry.Instance.GetRegistered(ao)
            .Concat(AvaloniaPropertyRegistry.Instance.GetRegisteredAttached(ao.GetType()))
            .FirstOrDefault(p => p.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
}
