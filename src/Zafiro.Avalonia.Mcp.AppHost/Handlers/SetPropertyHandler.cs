using System.Globalization;
using System.Text.Json;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Threading;
using Zafiro.Avalonia.Mcp.Protocol;
using Zafiro.Avalonia.Mcp.Protocol.Messages;

namespace Zafiro.Avalonia.Mcp.AppHost.Handlers;

public sealed class SetPropertyHandler : IRequestHandler
{
    public string Method => ProtocolMethods.SetProperty;

    public async Task<object> Handle(DiagnosticRequest request)
    {
        int nodeId = 0;
        string propName = "";
        string? value = null;

        if (request.Params is JsonElement p)
        {
            if (p.TryGetProperty("nodeId", out var nid)) nodeId = nid.GetInt32();
            if (p.TryGetProperty("propertyName", out var pn)) propName = pn.GetString() ?? "";
            if (p.TryGetProperty("value", out var v)) value = v.GetString();
        }

        return await Dispatcher.UIThread.InvokeAsync<object>(() =>
        {
            var visual = NodeRegistry.Resolve(nodeId);
            if (visual is not AvaloniaObject ao) return new { error = $"Node {nodeId} not found" };

            if (value is null or "unset")
            {
                var propToUnset = FindProperty(ao, propName);
                if (propToUnset is not null)
                {
                    ao.ClearValue(propToUnset);
                    return new { success = true, property = propName, action = "unset" };
                }
                return new { error = $"Property '{propName}' not found" };
            }

            var prop = FindProperty(ao, propName);
            if (prop is null) return new { error = $"Property '{propName}' not found" };

            var converted = ConvertValue(value, prop.PropertyType);
            if (converted is null) return new { error = $"Cannot convert '{value}' to {prop.PropertyType.Name}" };

            ao.SetValue(prop, converted);
            return new { success = true, property = propName, value };
        });
    }

    private static AvaloniaProperty? FindProperty(AvaloniaObject ao, string name) =>
        AvaloniaPropertyRegistry.Instance.GetRegistered(ao)
            .Concat(AvaloniaPropertyRegistry.Instance.GetRegisteredAttached(ao.GetType()))
            .FirstOrDefault(p => p.Name.Equals(name, StringComparison.OrdinalIgnoreCase));

    private static object? ConvertValue(string value, Type targetType)
    {
        if (targetType == typeof(string) || targetType == typeof(object)) return value;
        if (targetType == typeof(bool)) return bool.TryParse(value, out var b) ? b : null;
        if (targetType == typeof(int)) return int.TryParse(value, out var i) ? i : null;
        if (targetType == typeof(double)) return double.TryParse(value, CultureInfo.InvariantCulture, out var d) ? d : null;
        if (targetType == typeof(float)) return float.TryParse(value, CultureInfo.InvariantCulture, out var f) ? f : null;
        if (targetType.IsEnum) return Enum.TryParse(targetType, value, true, out var e) ? e : null;

        if (targetType == typeof(Thickness))
        {
            var parts = value.Split(',').Select(s => double.Parse(s.Trim(), CultureInfo.InvariantCulture)).ToArray();
            return parts.Length switch
            {
                1 => new Thickness(parts[0]),
                2 => new Thickness(parts[0], parts[1]),
                4 => new Thickness(parts[0], parts[1], parts[2], parts[3]),
                _ => null
            };
        }

        if (targetType == typeof(IBrush) || targetType == typeof(ISolidColorBrush) || targetType == typeof(Brush))
        {
            if (Color.TryParse(value, out var color))
                return new SolidColorBrush(color);
        }

        return null;
    }
}
