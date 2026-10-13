using System.ComponentModel;
using System.Globalization;
using System.Reflection;
using Avalonia.Media;

namespace Zafiro.Avalonia.Mcp.AppHost.Handlers;

/// <summary>
/// The string conversion contract shared by <c>get_prop_values</c> (what is advertised) and
/// <c>set_prop</c> (what is accepted). Nullable types are unwrapped before conversion, and
/// <see cref="NullToken"/> assigns null explicitly, which is different from clearing the local value.
/// </summary>
internal static class PropertyValueConverter
{
    /// <summary>Assigns null to any property whose type accepts it.</summary>
    public const string NullToken = "{x:Null}";

    public const string UnsetToken = "unset";

    public static bool AcceptsNull(Type type) => !type.IsValueType || Nullable.GetUnderlyingType(type) is not null;

    public static Type Underlying(Type type) => Nullable.GetUnderlyingType(type) ?? type;

    /// <summary>"Boolean?" for <c>Nullable&lt;bool&gt;</c> instead of the CLR's "Nullable`1".</summary>
    public static string DisplayName(Type type) =>
        Nullable.GetUnderlyingType(type) is { } underlying ? underlying.Name + "?" : type.Name;

    /// <summary>
    /// Tokens accepted as an explicit null for <paramref name="type"/>. Plain "null" is only a null
    /// where it cannot be a legitimate value, so a string property can still be set to the text "null".
    /// </summary>
    public static bool IsNullLiteral(string value, Type type)
    {
        if (!AcceptsNull(type)) return false;
        if (value == NullToken) return true;
        return Underlying(type) != typeof(string) && Underlying(type) != typeof(object)
               && value.Trim().Equals("null", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Example input for types whose format is not obvious from the type name.</summary>
    public static string? FormatHint(Type type)
    {
        var underlying = Underlying(type);
        if (underlying == typeof(DateTime)) return "ISO 8601 date or date-time, e.g. 2026-01-15 or 2026-01-15T13:45:00";
        if (underlying == typeof(DateTimeOffset)) return "ISO 8601 date-time with offset, e.g. 2026-01-15T13:45:00+01:00";
        if (underlying == typeof(TimeSpan)) return "[d.]hh:mm[:ss[.fff]], e.g. 01:30:00";
        if (underlying == typeof(DateOnly)) return "yyyy-MM-dd, e.g. 2026-01-15";
        if (underlying == typeof(TimeOnly)) return "HH:mm[:ss], e.g. 13:45";
        return null;
    }

    public static bool TryConvert(string value, Type targetType, out object? result)
    {
        if (IsNullLiteral(value, targetType))
        {
            result = null;
            return true;
        }

        result = ConvertNonNull(value, Underlying(targetType));
        return result is not null;
    }

    private static object? ConvertNonNull(string value, Type type)
    {
        if (type == typeof(string) || type == typeof(object)) return value;

        var text = value.Trim();
        var culture = CultureInfo.InvariantCulture;

        if (type == typeof(bool)) return bool.TryParse(text, out var b) ? b : null;
        if (type.IsEnum) return Enum.TryParse(type, text, true, out var e) ? e : null;
        if (type == typeof(DateTime))
            return DateTime.TryParse(text, culture, DateTimeStyles.RoundtripKind, out var dt) ? dt : null;
        if (type == typeof(DateTimeOffset))
            return DateTimeOffset.TryParse(text, culture, DateTimeStyles.None, out var dto) ? dto : null;
        if (type == typeof(TimeSpan)) return TimeSpan.TryParse(text, culture, out var ts) ? ts : null;
        if (type == typeof(DateOnly)) return DateOnly.TryParse(text, culture, out var d) ? d : null;
        if (type == typeof(TimeOnly)) return TimeOnly.TryParse(text, culture, out var t) ? t : null;
        if (type.IsPrimitive || type == typeof(decimal)) return TryInvoke(() => Convert.ChangeType(text, type, culture));

        if (type == typeof(IBrush) || type == typeof(ISolidColorBrush) || type == typeof(Brush))
            return Color.TryParse(text, out var color) ? new SolidColorBrush(color) : null;

        // Avalonia value types (Thickness, CornerRadius, GridLength, Color, ...) expose Parse(string).
        var parse = type.GetMethod("Parse", BindingFlags.Public | BindingFlags.Static, [typeof(string)]);
        if (parse is not null && type.IsAssignableFrom(parse.ReturnType))
            return TryInvoke(() => parse.Invoke(null, [text]));

        var converter = TypeDescriptor.GetConverter(type);
        if (converter.CanConvertFrom(typeof(string)))
            return TryInvoke(() => converter.ConvertFromInvariantString(text));

        return null;
    }

    private static object? TryInvoke(Func<object?> convert)
    {
        try
        {
            return convert();
        }
        catch (Exception ex) when (ex is FormatException or OverflowException or ArgumentException or InvalidCastException
                                       or NotSupportedException or TargetInvocationException)
        {
            return null;
        }
    }
}
