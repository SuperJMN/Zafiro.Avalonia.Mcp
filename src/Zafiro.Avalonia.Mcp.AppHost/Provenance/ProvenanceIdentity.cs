using System.Globalization;
using System.Runtime.CompilerServices;
using Avalonia;
using Avalonia.Media;
using Zafiro.Avalonia.Mcp.AppHost.Handlers;
using Zafiro.Avalonia.Mcp.Protocol.Models;

namespace Zafiro.Avalonia.Mcp.AppHost.Provenance;

internal static class ProvenanceIdentity
{
    private static readonly ConditionalWeakTable<object, Identifier> Ids = new();
    private static int _nextId;

    public static string Id(object value) =>
        Ids.GetValue(value, _ => new Identifier($"source:{Interlocked.Increment(ref _nextId)}")).Value;

    public static RuntimeSource Source(object value) =>
        new(Id(value), value.GetType().FullName ?? value.GetType().Name,
            value is Visual visual ? NodeRegistry.GetOrRegister(visual) : null);

    public static PropertyIdentity Property(AvaloniaProperty property) =>
        new($"property:{AvaloniaDiagnosticsAdapter.PropertyId(property)}", property.Name,
            property.OwnerType.FullName ?? property.OwnerType.Name,
            property.PropertyType.FullName ?? property.PropertyType.Name,
            property.IsDirect ? "direct" : property.IsAttached ? "attached" : "styled", property.Inherits);

    public static string? Value(object? value)
    {
        var text = value switch
        {
            null => null,
            string s => s,
            bool b => b ? "true" : "false",
            byte or sbyte or short or ushort or int or uint or long or ulong or float or double or decimal =>
                Convert.ToString(value, CultureInfo.InvariantCulture),
            Enum e => e.ToString(),
            Type type => type.FullName,
            Color color => ColorValue(color),
            ISolidColorBrush brush when value.GetType().Assembly == typeof(Brush).Assembly => ColorValue(brush.Color),
            Thickness thickness => thickness.ToString(),
            CornerRadius radius => radius.ToString(),
            Size size => size.ToString(),
            Point point => point.ToString(),
            Rect rect => rect.ToString(),
            TimeSpan time => time.ToString("c", CultureInfo.InvariantCulture),
            _ when ReferenceEquals(value, AvaloniaProperty.UnsetValue) => "{UnsetValue}",
            _ => $"<{value.GetType().FullName}>"
        };
        return text is { Length: > 256 } ? text[..256] + "..." : text;
    }

    private static string ColorValue(Color color) => $"#{color.A:x2}{color.R:x2}{color.G:x2}{color.B:x2}";

    private sealed record Identifier(string Value);
}
