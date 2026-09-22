using Avalonia;
using Zafiro.Avalonia.Mcp.AppHost.Handlers;
using Zafiro.Avalonia.Mcp.Protocol.Messages;

namespace Zafiro.Avalonia.Mcp.AppHost.Provenance;

internal static class PropertyLookup
{
    public static IEnumerable<AvaloniaProperty> Registered(AvaloniaObject target) =>
        AvaloniaPropertyRegistry.Instance.GetRegistered(target)
            .Concat(AvaloniaPropertyRegistry.Instance.GetRegisteredAttached(target.GetType()))
            .Distinct();

    public static string DisplayName(AvaloniaProperty property) =>
        property.IsAttached ? $"{property.OwnerType.Name}.{property.Name}" : property.Name;

    public static bool Matches(AvaloniaProperty property, string name) =>
        string.Equals(DisplayName(property), name, StringComparison.OrdinalIgnoreCase) ||
        string.Equals($"{property.OwnerType.Name}.{property.Name}", name, StringComparison.OrdinalIgnoreCase) ||
        string.Equals($"{property.OwnerType.FullName}.{property.Name}", name, StringComparison.OrdinalIgnoreCase);

    public static (AvaloniaProperty? Property, HandlerErrorResult? Error) Resolve(AvaloniaObject target, string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return (null, HandlerResult.InvalidParam("propertyName", "A property name is required."));
        if (name.Length > 256)
            return (null, HandlerResult.InvalidParam("propertyName", "Property names must not exceed 256 characters."));

        var matches = Registered(target)
            .Where(property => Matches(property, name) ||
                string.Equals(property.Name, name, StringComparison.OrdinalIgnoreCase))
            .Take(3)
            .ToArray();

        return matches.Length switch
        {
            1 => (matches[0], null),
            0 => (null, HandlerResult.InvalidParam("propertyName", $"Property '{name}' is not registered on this element.")),
            _ => (null, HandlerResult.Error(
                DiagnosticErrorCodes.AmbiguousProperty,
                $"Property '{name}' identifies more than one registration.",
                "Use the full owner-qualified property name.",
                new { properties = matches.Select(property => $"{property.OwnerType.FullName}.{property.Name}").ToArray() }))
        };
    }
}
