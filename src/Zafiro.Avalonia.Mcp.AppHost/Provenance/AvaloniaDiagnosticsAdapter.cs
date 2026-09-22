using System.Collections;
using System.Collections.Concurrent;
using System.Reflection;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Styling;

namespace Zafiro.Avalonia.Mcp.AppHost.Provenance;

// Private value-store access is confined here; never call entry.HasValue/GetValue or frame.IsActive,
// which can subscribe dormant bindings and style activators
internal static class AvaloniaDiagnosticsAdapter
{
    private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    private static readonly ConcurrentDictionary<(Type, string, MemberTypes), MemberInfo> Members = new();
    private static readonly Type? ValueEntryType = typeof(AvaloniaObject).Assembly.GetType("Avalonia.PropertyStore.IValueEntry");

    public static bool IsSupported => typeof(AvaloniaObject).Assembly.GetName().Version?.Major is 11 or 12;

    public static int PropertyId(AvaloniaProperty property) =>
        (int)RequiredProperty(typeof(AvaloniaProperty), "Id").GetValue(property)!;

    public static AvaloniaObject? InheritanceParent(AvaloniaObject target) =>
        (AvaloniaObject?)RequiredProperty(typeof(AvaloniaObject), "InheritanceParent").GetValue(target);

    public static IReadOnlyList<FrameEvidence> Frames(AvaloniaObject target, InspectionContext context)
    {
        if (!IsSupported)
        {
            context.Unavailable("valueStore", "This Avalonia runtime version is not supported by the diagnostics adapter.");
            return [];
        }
        if (ValueEntryType is null)
            throw new MissingMemberException("Avalonia.PropertyStore.IValueEntry metadata is unavailable.");

        var store = Store(target);
        var frames = (IList)Property(store, "Frames")!;
        var result = new List<FrameEvidence>();
        var remainingEntries = 1024;
        for (var i = frames.Count - 1; i >= 0; i--)
        {
            if (result.Count == InspectionContext.MaxItems || remainingEntries == 0)
            {
                context.Truncate("valueFrames");
                break;
            }

            var frame = frames[i]!;
            var style = OptionalProperty(frame, "Source") as StyleBase;
            var priority = (BindingPriority)Property(frame, "Priority")!;
            var framePriority = Property(frame, "FramePriority")!.ToString()!;
            var count = (int)Property(frame, "EntryCount")!;
            var entries = new List<EntryEvidence>();
            for (var j = 0; j < count && remainingEntries > 0; j++, remainingEntries--)
            {
                var entry = Method(frame.GetType(), "GetEntry", [typeof(int)]).Invoke(frame, [j])!;
                var property = (AvaloniaProperty)RequiredProperty(ValueEntryType, "Property").GetValue(entry)!;
                entries.Add(new EntryEvidence(property, entry));
            }
            if (entries.Count < count)
                context.Truncate("valueEntries");

            result.Add(new FrameEvidence(frame, style, priority, framePriority, i,
                Active(frame, style, context), entries));
        }
        return result;
    }

    public static EffectiveEvidence Effective(AvaloniaObject target, AvaloniaProperty property)
    {
        var store = Store(target);
        var value = Method(store.GetType(), "GetEffectiveValue", [typeof(AvaloniaProperty)]).Invoke(store, [property]);
        var bindings = Field(store, "_localValueBindings") as IDictionary;
        var localBinding = bindings?[PropertyId(property)];
        var uncommon = value is null ? null : OptionalField(value, "_uncommon");
        return new EffectiveEvidence(
            value is null ? null : Property(value, "ValueEntry"),
            value is null ? null : Property(value, "BaseValueEntry"),
            localBinding,
            value is null ? BindingPriority.Unset : (BindingPriority)Property(value, "BasePriority")!,
            value is null ? null : OptionalField(value, "_baseValue"),
            property.IsDirect ? null : value is null
                ? OptionalProperty(property.GetMetadata(target), "CoerceValue") is not null
                : (bool?)Property(value, "HasCoercion"),
            value is null ? null : (bool?)Property(value, "IsCoercedDefaultValue"),
            uncommon is null ? null : OptionalField(uncommon, "_uncoercedValue"),
            uncommon is null ? null : OptionalField(uncommon, "_uncoercedBaseValue"));
    }

    public static (AvaloniaObject? Supplier, bool Complete) InheritedSupplier(
        AvaloniaObject target, AvaloniaProperty property, InspectionContext context, int depth)
    {
        var current = Property(Store(target), "InheritanceAncestor");
        var visited = new HashSet<object>(ReferenceEqualityComparer.Instance);
        while (current is not null)
        {
            if (depth++ >= context.MaxDepth)
            {
                context.Truncate("inheritanceDepth");
                return (null, false);
            }
            if (!visited.Add(current))
            {
                context.Unavailable("inheritance", "A property-inheritance cycle was detected.");
                return (null, false);
            }
            if (Method(current.GetType(), "GetEffectiveValue", [typeof(AvaloniaProperty)]).Invoke(current, [property]) is not null)
                return ((AvaloniaObject)Property(current, "Owner")!, true);
            current = Property(current, "InheritanceAncestor");
        }
        return (null, true);
    }

    private static bool IsBinding(object? entry) =>
        entry is not null && (entry is BindingExpressionBase ||
            IsFrameworkType(entry, "Avalonia.PropertyStore.LocalValueBindingObserver") ||
            IsFrameworkType(entry, "Avalonia.PropertyStore.BindingEntry") ||
            IsFrameworkType(entry, "Avalonia.PropertyStore.TypedBindingEntry") ||
            IsFrameworkType(entry, "Avalonia.PropertyStore.SourceUntypedBindingEntry"));

    public static IList<SetterBase>? DeclaredSetters(StyleBase style) =>
        OptionalField(style, "_setters") as IList<SetterBase>;

    public static object? OwnResources(object node) => OptionalField(node, "_resources");
    public static object? OwnStyles(object node) => OptionalField(node, "_styles");
    public static IDictionary? ThemeDictionaries(ResourceDictionary dictionary) =>
        OptionalField(dictionary, "_themeDictionary") as IDictionary;
    public static IList? MergedDictionaries(ResourceDictionary dictionary) =>
        OptionalField(dictionary, "_mergedDictionaries") as IList;

    public static ProviderEvidence Provider(object? declared, object? entry)
    {
        var runtime = entry ?? declared;
        var typeName = runtime?.GetType().FullName;
        var running = KnownExpression(entry) ? (bool?)OptionalProperty(entry!, "IsRunning") : null;
        var cached = KnownExpression(entry) ? OptionalField(entry!, "_value") : null;
        if (runtime is not null && IsFrameworkType(runtime, "Avalonia.Markup.Xaml.MarkupExtensions.DynamicResource"))
            return new ProviderEvidence("dynamicResource", typeName, cached, running,
                Key: OptionalField(runtime, "_resourceKey") ?? OptionalProperty(runtime, "ResourceKey"),
                ResourceHost: OptionalField(runtime, "_host"),
                ThemeVariant: OptionalField(runtime, "_themeVariant"));

        if (runtime is not null && IsFrameworkType(runtime, "Avalonia.Data.TemplateBinding"))
            return new ProviderEvidence("templateBinding", typeName, cached, running,
                SourceProperty: OptionalField(runtime, "_property") as AvaloniaProperty ??
                                OptionalProperty(runtime, "Property") as AvaloniaProperty);

        if (runtime is not null && (IsBinding(runtime) || IsDeclaredBinding(runtime)))
        {
            var knownExpression = IsFrameworkType(runtime, "Avalonia.Data.Core.BindingExpression");
            var path = declared is not null && IsDeclaredBinding(declared) ? OptionalProperty(declared, "Path") as string : null;
            if (path is null && knownExpression)
                path = OptionalProperty(runtime, "Description") as string;
            object? source = null;
            if (knownExpression && OptionalField(runtime, "_source") is WeakReference<object?> weak)
                weak.TryGetTarget(out source);
            return new ProviderEvidence("binding", typeName, cached, running,
                Path: path, Source: source,
                ErrorType: KnownExpression(entry) ? OptionalProperty(entry!, "ErrorType")?.ToString() : null,
                FallbackValue: knownExpression ? OptionalProperty(runtime, "FallbackValue") : null);
        }

        var stored = entry is null ? declared : StoredValue(entry);
        return new ProviderEvidence("storedValue", stored?.GetType().FullName, stored, null);
    }

    private static bool IsDeclaredBinding(object value) =>
        IsFrameworkType(value, "Avalonia.Data.Binding") ||
        IsFrameworkType(value, "Avalonia.Data.MultiBinding") ||
        IsFrameworkType(value, "Avalonia.Markup.Xaml.MarkupExtensions.CompiledBindingExtension");

    private static bool KnownExpression(object? value) =>
        value is BindingExpressionBase && value.GetType().Assembly.GetName().Name is "Avalonia.Base" or "Avalonia.Markup.Xaml";

    private static bool IsFrameworkType(object value, string name) =>
        value.GetType().Assembly.GetName().Name is "Avalonia.Base" or "Avalonia.Markup.Xaml" &&
        value.GetType().FullName?.StartsWith(name, StringComparison.Ordinal) == true;

    private static object? StoredValue(object entry) =>
        entry is Setter setter ? setter.Value : OptionalField(entry, "_value");

    private static object? Field(object target, string name) =>
        RequiredField(target.GetType(), name).GetValue(target);

    private static object? OptionalField(object target, string name) =>
        FindField(target.GetType(), name)?.GetValue(target);

    private static object? Property(object target, string name) =>
        RequiredProperty(target.GetType(), name).GetValue(target);

    private static object? OptionalProperty(object target, string name)
    {
        for (var type = target.GetType(); type is not null; type = type.BaseType)
        {
            if (type.GetProperty(name, Flags | BindingFlags.DeclaredOnly) is { } property)
                return property.GetValue(target);
        }
        return null;
    }

    private static object Store(AvaloniaObject target) =>
        RequiredField(typeof(AvaloniaObject), "_values").GetValue(target)!;

    private static bool? Active(object frame, StyleBase? style, InspectionContext context)
    {
        if (style is null)
        {
            if (IsFrameworkType(frame, "Avalonia.PropertyStore.ImmediateValueFrame"))
                return true;
            context.Unavailable("frameActivation", $"Activation is unavailable for {frame.GetType().FullName}.");
            return null;
        }

        var activator = Field(frame, "_activator");
        if (activator is null)
            return true;
        if (activator.GetType().Assembly != typeof(Style).Assembly)
        {
            context.Unavailable("styleActivation", "Custom activators are not evaluated by the inspector.");
            return null;
        }

        return (bool)Method(activator.GetType(), "EvaluateIsActive", []).Invoke(activator, null)!;
    }

    private static FieldInfo RequiredField(Type type, string name) =>
        (FieldInfo)Members.GetOrAdd((type, name, MemberTypes.Field), key =>
            FindField(key.Item1, key.Item2) ??
            throw new MissingFieldException(key.Item1.FullName, key.Item2));

    private static FieldInfo? FindField(Type type, string name)
    {
        for (var current = type; current is not null; current = current.BaseType)
        {
            if (current.GetField(name, Flags | BindingFlags.DeclaredOnly) is { } field)
                return field;
        }
        return null;
    }

    private static PropertyInfo RequiredProperty(Type type, string name) =>
        (PropertyInfo)Members.GetOrAdd((type, name, MemberTypes.Property), key =>
            key.Item1.GetProperty(key.Item2, Flags) ??
            throw new MissingMemberException(key.Item1.FullName, key.Item2));

    private static MethodInfo Method(Type type, string name, Type[] parameters) =>
        type.GetMethod(name, Flags, null, parameters, null) ??
        throw new MissingMethodException(type.FullName, name);
}

internal sealed record EntryEvidence(AvaloniaProperty Property, object Entry);

internal sealed record FrameEvidence(
    object Frame, StyleBase? Style, BindingPriority Priority, string FramePriority,
    int Order, bool? Active, IReadOnlyList<EntryEvidence> Entries);

internal sealed record EffectiveEvidence(
    object? Entry, object? BaseEntry, object? LocalBinding, BindingPriority BasePriority,
    object? BaseValue, bool? HasCoercion, bool? IsCoercedDefault, object? UncoercedValue, object? UncoercedBaseValue);

internal sealed record ProviderEvidence(
    string Kind, string? Type, object? Value, bool? IsRunning,
    object? Key = null, object? ResourceHost = null, object? ThemeVariant = null,
    AvaloniaProperty? SourceProperty = null, string? Path = null, object? Source = null,
    string? ErrorType = null, object? FallbackValue = null);
