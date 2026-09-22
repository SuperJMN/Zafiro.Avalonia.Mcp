using Avalonia;
using Zafiro.Avalonia.Mcp.Protocol.Models;

namespace Zafiro.Avalonia.Mcp.AppHost.Provenance;

internal static class ProviderInspector
{
    public static ValueProvider Read(
        object? declared, object? entry, AvaloniaObject target, InspectionContext context, int depth)
    {
        var evidence = AvaloniaDiagnosticsAdapter.Provider(declared, entry);
        if (evidence.Kind == "storedValue")
            context.Unavailable("assignmentSyntax",
                "Stored values do not retain whether they came from a literal, a resolved static resource, or programmatic assignment.");
        if (evidence.Kind == "binding" && evidence.Path is null)
            context.Unavailable("bindingPath", "This binding does not retain a readable path.");
        if (evidence.ErrorType is not null and not "None")
            context.Unavailable("fallbackUsage", "The binding exposes its error state but not whether its fallback supplied this value.");

        var source = evidence.Kind == "templateBinding" ? (target as StyledElement)?.TemplatedParent : evidence.Source;
        return new ValueProvider
        {
            Kind = evidence.Kind,
            Type = evidence.Type,
            Value = ProvenanceIdentity.Value(evidence.Value),
            Path = ProvenanceIdentity.Value(evidence.Path),
            Key = ProvenanceIdentity.Value(evidence.Key),
            Source = source is null ? null : ProvenanceIdentity.Source(source),
            SourceProperty = evidence.SourceProperty is null ? null : ProvenanceIdentity.Property(evidence.SourceProperty),
            IsRunning = evidence.IsRunning,
            ErrorType = evidence.ErrorType,
            FallbackValue = ProvenanceIdentity.Value(evidence.FallbackValue),
            Resource = evidence.Kind == "dynamicResource"
                ? ResourceInspector.Find(evidence.ResourceHost, evidence.Key, evidence.ThemeVariant, context, depth)
                : null
        };
    }
}
