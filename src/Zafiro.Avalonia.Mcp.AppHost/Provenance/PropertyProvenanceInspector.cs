using System.Reflection;
using Avalonia;
using Avalonia.Data;
using Avalonia.Diagnostics;
using Avalonia.Styling;
using Avalonia.Threading;
using Zafiro.Avalonia.Mcp.Protocol.Models;

namespace Zafiro.Avalonia.Mcp.AppHost.Provenance;

internal static class PropertyProvenanceInspector
{
    public static StylesSnapshot Styles(
        StyledElement target, bool includeInactive, IReadOnlyCollection<AvaloniaProperty>? filter)
    {
        Dispatcher.UIThread.VerifyAccess();
        var context = new InspectionContext(8);
        var classes = Classes(target, context);
        var styles = new List<StyleSourceInfo>();
        try
        {
            var frames = AvaloniaDiagnosticsAdapter.Frames(target, context);
            var remaining = InspectionContext.MaxItems;
            foreach (var frame in frames)
            {
                if (frame.Style is not { } style || (!includeInactive && frame.Active == false))
                    continue;

                var setters = new List<StyleSetterInfo>();
                var declared = AvaloniaDiagnosticsAdapter.DeclaredSetters(style);
                for (var i = 0; declared is not null && i < declared.Count; i++)
                {
                    if (remaining-- <= 0)
                    {
                        context.Truncate("styleSetters");
                        break;
                    }
                    if (declared[i] is not Setter { Property: { } property } setter ||
                        (filter is not null && !filter.Contains(property)))
                        continue;

                    var entry = frame.Entries.FirstOrDefault(item => item.Property == property)?.Entry;
                    var effective = AvaloniaDiagnosticsAdapter.Effective(target, property);
                    var winner = WinningFrame(frames, property, effective.Entry, target.GetDiagnostic(property).Priority, context);
                    setters.Add(new StyleSetterInfo(ProvenanceIdentity.Property(property),
                        ProviderInspector.Read(setter.Value, entry, target, context, 0),
                        property.IsDirect ? null : IsWinner(frame, winner, entry, effective.Entry,
                            target.GetDiagnostic(property).Priority), i));
                }
                if (filter is not null && setters.Count == 0)
                    continue;
                styles.Add(new StyleSourceInfo
                {
                    SourceId = ProvenanceIdentity.Id(style),
                    Kind = style is ControlTheme ? "controlTheme" : "style",
                    Selector = Selector(style),
                    TargetType = (style as ControlTheme)?.TargetType?.FullName,
                    Scope = Scope(style, target, context),
                    ParentSourceId = style.Parent is null ? null : ProvenanceIdentity.Id(style.Parent),
                    BasedOnSourceId = style is ControlTheme { BasedOn: { } basedOn } ? ProvenanceIdentity.Id(basedOn) : null,
                    Active = frame.Active,
                    ApplicationState = frame.Active switch { true => "attachedActive", false => "attachedInactive", _ => "attachedUnknown" },
                    Priority = frame.Priority.ToString(),
                    FramePriority = frame.FramePriority,
                    FrameOrder = frame.Order,
                    Setters = setters
                });
                if (remaining <= 0)
                    break;
            }
            if (styles.Count > 0)
                MissingSourceLocation(context);
        }
        catch (Exception exception) when (IsAdapterFailure(exception))
        {
            AdapterFailure(context, exception);
        }
        return new StylesSnapshot(ProvenanceIdentity.Source(target), context.Timestamp,
            classes, styles, context.Resolution());
    }

    public static PropertyExplanation Explain(
        AvaloniaObject target, AvaloniaProperty property, bool includeCandidates, int maxDepth)
    {
        Dispatcher.UIThread.VerifyAccess();
        var context = new InspectionContext(maxDepth);
        return Explain(target, property, includeCandidates, context, 0,
            new HashSet<AvaloniaObject>(ReferenceEqualityComparer.Instance));
    }

    private static PropertyExplanation Explain(
        AvaloniaObject target, AvaloniaProperty property, bool includeCandidates,
        InspectionContext context, int depth, HashSet<AvaloniaObject> visited)
    {
        var classes = Classes(target as StyledElement, context);
        var diagnostic = target.GetDiagnostic(property);
        var defaultValue = property.GetMetadata(target) is IStyledPropertyMetadata metadata ? metadata.DefaultValue : null;
        PropertyOrigin? origin = null;
        PropertyBaseValue? baseValue = null;
        EffectiveEvidence? effective = null;
        var candidates = includeCandidates ? new List<PropertyCandidate>() : null;
        try
        {
            if (!AvaloniaDiagnosticsAdapter.IsSupported)
                context.Unavailable("valueStore", "This Avalonia runtime version is not supported by the diagnostics adapter.");
            else if (depth >= context.MaxDepth)
                context.Truncate("inheritanceDepth");
            else if (!visited.Add(target))
                context.Unavailable("inheritance", "A property-inheritance cycle was detected.");
            else
            {
                effective = AvaloniaDiagnosticsAdapter.Effective(target, property);
                var frames = property.IsDirect ? [] : AvaloniaDiagnosticsAdapter.Frames(target, context);
                if (property.IsDirect)
                {
                    context.Unavailable("directAssignment", "Direct properties do not retain styled-property precedence or assignment history.");
                    origin = LocalOrigin(target, diagnostic.Value, effective.LocalBinding, context, depth) with { Kind = "directProperty" };
                }
                else if (diagnostic.Priority == BindingPriority.Inherited)
                {
                    var parent = AvaloniaDiagnosticsAdapter.InheritanceParent(target);
                    if (parent is not null)
                        origin = InheritedOrigin(parent, property, context, depth, visited);
                    else
                        context.Unavailable("inheritance", "Avalonia reports inheritance but the supplying inheritance parent is unavailable.");
                }
                else
                {
                    origin = Origin(target, property, diagnostic.Priority,
                        effective.HasCoercion == true ? effective.UncoercedValue : diagnostic.Value,
                        effective.Entry, effective.LocalBinding, frames, context, depth, defaultValue);
                }

                if (diagnostic.Priority == BindingPriority.Animation)
                {
                    var basePriority = effective.BasePriority;
                    var basePriorityKnown = true;
                    var baseOrigin = Origin(target, property, basePriority,
                        effective.HasCoercion == true ? effective.UncoercedBaseValue : effective.BaseValue,
                        effective.BaseEntry, effective.LocalBinding, frames, context, depth, defaultValue);
                    var baseDisplay = ProvenanceIdentity.Value(basePriority == BindingPriority.Unset ? defaultValue : effective.BaseValue);
                    if (basePriority == BindingPriority.Unset && property.Inherits)
                    {
                        var inherited = AvaloniaDiagnosticsAdapter.InheritedSupplier(target, property, context, depth);
                        if (inherited.Supplier is { } supplier)
                        {
                            baseOrigin = InheritedOrigin(supplier, property, context, depth, visited);
                            basePriority = BindingPriority.Inherited;
                            baseDisplay = baseOrigin.InheritedFrom!.EffectiveValue;
                        }
                        else if (!inherited.Complete)
                        {
                            baseOrigin = null;
                            baseDisplay = null;
                            basePriorityKnown = false;
                        }
                    }
                    if (effective.BasePriority == BindingPriority.Unset && effective.HasCoercion == true)
                    {
                        baseDisplay = null;
                        context.Unavailable("animationBaseValue", "No coerced base value is retained; inspection does not execute coercion callbacks.");
                    }
                    baseValue = new PropertyBaseValue(baseDisplay, basePriorityKnown ? basePriority.ToString() : null, baseOrigin);
                    context.Unavailable("animationTimeline", "The retained value entry does not identify the originating animation or transition timeline.");
                }

                if (includeCandidates && !property.IsDirect)
                {
                    if (effective.LocalBinding is { } localBinding && diagnostic.Priority != BindingPriority.LocalValue)
                    {
                        var provider = AvaloniaDiagnosticsAdapter.Provider(null, localBinding);
                        candidates!.Add(new PropertyCandidate(BindingPriority.LocalValue.ToString(), provider.IsRunning, false,
                            ReferenceEquals(provider.Value, AvaloniaProperty.UnsetValue) ? "unsetEntry" :
                            diagnostic.Priority < BindingPriority.LocalValue ? "lowerPriority" : "notSelectedByRuntime",
                            LocalOrigin(target, null, localBinding, context, depth)));
                    }
                    else if (diagnostic.Priority == BindingPriority.Animation && baseValue?.Priority == nameof(BindingPriority.LocalValue) &&
                             baseValue.Origin is { } baseOrigin)
                    {
                        candidates!.Add(new PropertyCandidate(BindingPriority.LocalValue.ToString(), true, false,
                            "lowerPriority", baseOrigin));
                    }
                    var winner = WinningFrame(frames, property, effective.Entry, diagnostic.Priority, context);
                    foreach (var frame in frames)
                    {
                        var entry = frame.Entries.FirstOrDefault(item => item.Property == property)?.Entry;
                        if (entry is null)
                            continue;
                        if (candidates!.Count == InspectionContext.MaxItems)
                        {
                            context.Truncate("candidates");
                            break;
                        }
                        var wins = IsWinner(frame, winner, entry, effective.Entry, diagnostic.Priority);
                        var reason = frame.Active == false ? "inactive" :
                            wins == true ? "selectedRuntimeEntry" :
                            wins is null ? "sourceIdentityAmbiguous" :
                            frame.Priority > diagnostic.Priority ? "lowerPriority" :
                            frame.Priority == diagnostic.Priority && winner is not null && frame.FramePriority != winner.FramePriority ? "lowerFramePriority" :
                            frame.Priority == diagnostic.Priority && winner is not null ? "lowerRuntimeFrameOrder" :
                            "notSelectedByRuntime";
                        candidates.Add(new PropertyCandidate(frame.Priority.ToString(), frame.Active, wins, reason,
                            FrameOrigin(target, property, frame, entry, context, depth)));
                    }
                }
            }
        }
        catch (Exception exception) when (IsAdapterFailure(exception))
        {
            AdapterFailure(context, exception);
        }

        return new PropertyExplanation
        {
            Target = ProvenanceIdentity.Source(target),
            Timestamp = context.Timestamp,
            Property = ProvenanceIdentity.Property(property),
            EffectiveValue = ProvenanceIdentity.Value(diagnostic.Value),
            Priority = property.IsDirect ? null : diagnostic.Priority.ToString(),
            ClassState = classes,
            Origin = origin,
            BaseValue = baseValue,
            IsCurrentValueOverride = diagnostic.IsOverriddenCurrentValue,
            HasCoercion = effective?.HasCoercion,
            IsCoercedDefault = effective?.IsCoercedDefault,
            UncoercedValue = effective?.HasCoercion == true ? ProvenanceIdentity.Value(effective.UncoercedValue) : null,
            DefaultValue = property.IsDirect ? null : ProvenanceIdentity.Value(defaultValue),
            Candidates = candidates,
            Resolution = context.Resolution()
        };
    }

    private static PropertyOrigin? Origin(
        AvaloniaObject target, AvaloniaProperty property, BindingPriority priority, object? value,
        object? entry, object? localBinding, IReadOnlyList<FrameEvidence> frames,
        InspectionContext context, int depth, object? defaultValue)
    {
        if (priority == BindingPriority.LocalValue)
            return LocalOrigin(target, value, localBinding, context, depth);
        if (priority == BindingPriority.Unset)
            return new PropertyOrigin
            {
                Kind = "metadataDefault",
                Provider = new ValueProvider { Kind = "defaultValue", Value = ProvenanceIdentity.Value(defaultValue) }
            };

        var frame = WinningFrame(frames, property, entry, priority, context);
        if (frame is not null && entry is not null)
            return FrameOrigin(target, property, frame, entry, context, depth);
        context.Unavailable("winningSource", "The effective entry could not be uniquely linked to a captured runtime frame.");
        return null;
    }

    private static PropertyOrigin LocalOrigin(
        AvaloniaObject target, object? value, object? binding, InspectionContext context, int depth)
    {
        context.Unavailable("assignmentLocation", "Local assignments do not retain their AXAML or programmatic source location.");
        return new PropertyOrigin
        {
            Kind = binding is null ? "localValue" : "localBinding",
            SourceId = ProvenanceIdentity.Id(binding ?? target),
            Scope = ProvenanceIdentity.Source(target),
            Provider = ProviderInspector.Read(value, binding, target, context, depth)
        };
    }

    private static PropertyOrigin InheritedOrigin(
        AvaloniaObject parent, AvaloniaProperty property, InspectionContext context, int depth,
        HashSet<AvaloniaObject> visited) =>
        new()
        {
            Kind = "inherited",
            SourceId = ProvenanceIdentity.Id(parent),
            Scope = ProvenanceIdentity.Source(parent),
            Provider = new ValueProvider { Kind = "inherited" },
            InheritedFrom = Explain(parent, property, false, context, depth + 1, visited)
        };

    private static FrameEvidence? WinningFrame(
        IReadOnlyList<FrameEvidence> frames, AvaloniaProperty property, object? entry,
        BindingPriority priority, InspectionContext context)
    {
        if (entry is null)
            return null;
        FrameEvidence? winner = null;
        foreach (var frame in frames)
        {
            if (frame.Priority != priority || frame.Active == false ||
                !frame.Entries.Any(item => item.Property == property && ReferenceEquals(item.Entry, entry)))
                continue;
            if (winner is not null)
            {
                context.Unavailable("winningSource", "The selected entry is shared by multiple attached frames; its exact frame is not retained.");
                return null;
            }
            winner = frame;
        }
        return winner;
    }

    private static PropertyOrigin FrameOrigin(
        AvaloniaObject target, AvaloniaProperty property, FrameEvidence frame, object entry,
        InspectionContext context, int depth)
    {
        object? declared = null;
        if (frame.Style is { } style)
        {
            var setters = AvaloniaDiagnosticsAdapter.DeclaredSetters(style);
            if (setters is not null)
                declared = setters.Take(InspectionContext.MaxItems).OfType<Setter>().FirstOrDefault(setter => setter.Property == property)?.Value;
            MissingSourceLocation(context);
        }
        else if (frame.Priority == BindingPriority.Template)
            context.Unavailable("originatingTemplate", "The value frame retains the instantiated target, not the original ControlTemplate or DataTemplate.");

        return new PropertyOrigin
        {
            Kind = frame.Style is ControlTheme ? "controlThemeSetter" : frame.Style is not null ? "styleSetter" :
                frame.Priority == BindingPriority.Animation ? "animation" :
                frame.Priority == BindingPriority.Template ? "templateEntry" : "valueEntry",
            SourceId = ProvenanceIdentity.Id(frame.Style ?? frame.Frame),
            Selector = frame.Style is null ? null : Selector(frame.Style),
            Scope = frame.Style is null ? ProvenanceIdentity.Source(target) : Scope(frame.Style, target, context),
            TemplatedParent = target is StyledElement { TemplatedParent: { } parent } ? ProvenanceIdentity.Source(parent) : null,
            FrameOrder = frame.Order,
            FramePriority = frame.FramePriority,
            Provider = ProviderInspector.Read(declared, entry, target, context, depth)
        };
    }

    private static RuntimeSource? Scope(StyleBase style, AvaloniaObject target, InspectionContext context)
    {
        if (style.Owner is { } owner)
            return ProvenanceIdentity.Source(owner);
        if (target is StyledElement styled && ReferenceEquals(styled.Theme, style))
            return ProvenanceIdentity.Source(target);
        context.Unavailable("owningScope", "This style or theme does not retain an owning resource host.");
        return null;
    }

    private static string? Selector(StyleBase style) =>
        style is Style { Selector: { } selector } ? ProvenanceIdentity.Value(selector.ToString()) : null;

    private static ClassState Classes(StyledElement? target, InspectionContext context)
    {
        if (target is null)
            return new ClassState([], []);
        if (target.Classes.Count > InspectionContext.MaxItems ||
            target.Classes.Take(InspectionContext.MaxItems).Any(value => value.Length > 256))
            context.Truncate("classState");
        var classes = target.Classes.Take(InspectionContext.MaxItems).Select(value => ProvenanceIdentity.Value(value)!).ToArray();
        return new ClassState(classes.Where(value => !value.StartsWith(':')).ToArray(),
            classes.Where(value => value.StartsWith(':')).ToArray());
    }

    private static void MissingSourceLocation(InspectionContext context) =>
        context.Unavailable("sourceLocation", "Compiled styles do not retain a reliable AXAML URI and line/column mapping.");

    private static bool IsAdapterFailure(Exception exception) =>
        exception is MissingMemberException or TargetInvocationException or MemberAccessException or InvalidCastException;

    private static bool? IsWinner(FrameEvidence frame, FrameEvidence? winner, object? entry, object? effectiveEntry, BindingPriority priority)
    {
        if (frame.Priority != priority || frame.Active == false || entry is null || !ReferenceEquals(entry, effectiveEntry))
            return false;
        return winner is null ? null : ReferenceEquals(winner.Frame, frame.Frame);
    }

    private static void AdapterFailure(InspectionContext context, Exception exception) =>
        context.Unavailable("runtimeDiagnostics",
            exception is MissingMemberException
                ? $"The Avalonia diagnostics adapter is missing a required runtime member: {exception.Message}"
                : $"The Avalonia diagnostics adapter could not read the retained runtime evidence ({exception.GetType().Name}).");
}
