using System.ComponentModel;
using System.Text.Json;
using Avalonia;
using Avalonia.Threading;
using Zafiro.Avalonia.Mcp.AppHost.Selectors;
using Zafiro.Avalonia.Mcp.Protocol;
using Zafiro.Avalonia.Mcp.Protocol.Messages;

namespace Zafiro.Avalonia.Mcp.AppHost.Handlers;

public sealed class PropertyValuesHandler : IRequestHandler
{
    public string Method => ProtocolMethods.GetPropertyValues;

    public async Task<object> Handle(DiagnosticRequest request)
    {
        string? selector = null;
        string? propertyName = null;

        if (request.Params is JsonElement p)
        {
            if (p.TryGetProperty("selector", out var s)) selector = s.GetString();
            if (p.TryGetProperty("propertyName", out var pn)) propertyName = pn.GetString();
        }

        if (propertyName is null)
            return new { error = "propertyName is required" };

        return await Dispatcher.UIThread.InvokeAsync<object>(() =>
        {
            var (visual, error) = SelectorRequestHelper.ResolveSingle(selector);
            if (visual is null) return error!;
            if (visual is not AvaloniaObject ao) return new { error = "Not an AvaloniaObject" };

            return BuildResult(ao, propertyName);
        });
    }

    internal static object BuildResult(AvaloniaObject ao, string propertyName)
    {
        var prop = AvaloniaPropertyRegistry.Instance.GetRegistered(ao)
            .Concat(AvaloniaPropertyRegistry.Instance.GetRegisteredAttached(ao.GetType()))
            .FirstOrDefault(p => string.Equals(p.Name, propertyName, StringComparison.OrdinalIgnoreCase));

        if (prop is null)
            return new { error = $"Property '{propertyName}' not found on this element" };

        var type = prop.PropertyType;
        var underlying = Nullable.GetUnderlyingType(type) ?? type;

        if (underlying.IsEnum)
        {
            var values = Enum.GetNames(underlying);
            return new { propertyName = prop.Name, type = underlying.Name, values };
        }

        if (underlying == typeof(bool))
        {
            return new { propertyName = prop.Name, type = "Boolean", values = new[] { "true", "false" } };
        }

        var converter = TypeDescriptor.GetConverter(underlying);
        if (converter.GetStandardValuesSupported())
        {
            var stdValues = converter.GetStandardValues();
            if (stdValues is not null)
            {
                var values = stdValues.Cast<object?>()
                    .Select(v => v?.ToString())
                    .Where(v => v is not null)
                    .ToArray();
                return new { propertyName = prop.Name, type = underlying.Name, values };
            }
        }

        return new { propertyName = prop.Name, type = underlying.Name, values = (string[]?)null, note = "No predefined values — accepts any value of this type" };
    }
}
