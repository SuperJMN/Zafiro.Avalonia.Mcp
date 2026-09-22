namespace Zafiro.Avalonia.Mcp.Protocol.Models;

public sealed record ClassState(IReadOnlyList<string> Classes, IReadOnlyList<string> PseudoClasses);

public sealed record PropertyIdentity(
    string Id, string Name, string Owner, string Type, string RegistrationKind, bool Inherits);

public sealed record RuntimeSource(string SourceId, string Type, int? NodeId = null);

public sealed record SourceLocation(string Uri, int? Line = null, int? Column = null);

public sealed record UnavailableEvidence(string Evidence, string Reason);

public sealed record ProvenanceResolution(
    string Status, IReadOnlyList<UnavailableEvidence> Unavailable, bool Truncated = false);

public sealed record ValueProvider
{
    public required string Kind { get; init; }
    public string? Value { get; init; }
    public string? Type { get; init; }
    public string? Path { get; init; }
    public string? Key { get; init; }
    public RuntimeSource? Source { get; init; }
    public PropertyIdentity? SourceProperty { get; init; }
    public bool? IsRunning { get; init; }
    public string? ErrorType { get; init; }
    public string? FallbackValue { get; init; }
    public ResourceOrigin? Resource { get; init; }
}

public sealed record ResourceOrigin(
    RuntimeSource? Dictionary,
    RuntimeSource? LookupHost,
    RuntimeSource? Scope,
    string? ThemeVariant,
    ProvenanceResolution Resolution);

public sealed record PropertyOrigin
{
    public required string Kind { get; init; }
    public string? SourceId { get; init; }
    public string? Selector { get; init; }
    public RuntimeSource? Scope { get; init; }
    public RuntimeSource? TemplatedParent { get; init; }
    public SourceLocation? Location { get; init; }
    public int? FrameOrder { get; init; }
    public string? FramePriority { get; init; }
    public required ValueProvider Provider { get; init; }
    public PropertyExplanation? InheritedFrom { get; init; }
}

public sealed record PropertyCandidate(
    string Priority, bool? Active, bool? Wins, string Reason, PropertyOrigin Origin);

public sealed record PropertyBaseValue(string? Value, string? Priority, PropertyOrigin? Origin);

public sealed record PropertyExplanation
{
    public required RuntimeSource Target { get; init; }
    public required DateTimeOffset Timestamp { get; init; }
    public required PropertyIdentity Property { get; init; }
    public string? EffectiveValue { get; init; }
    public string? Priority { get; init; }
    public required ClassState ClassState { get; init; }
    public PropertyOrigin? Origin { get; init; }
    public PropertyBaseValue? BaseValue { get; init; }
    public bool IsCurrentValueOverride { get; init; }
    public bool? HasCoercion { get; init; }
    public bool? IsCoercedDefault { get; init; }
    public string? UncoercedValue { get; init; }
    public string? DefaultValue { get; init; }
    public IReadOnlyList<PropertyCandidate>? Candidates { get; init; }
    public required ProvenanceResolution Resolution { get; init; }
}

public sealed record StyleSetterInfo(
    PropertyIdentity Property, ValueProvider Provider, bool? Wins, int DeclarationOrder);

public sealed record StyleSourceInfo
{
    public required string SourceId { get; init; }
    public required string Kind { get; init; }
    public string? Selector { get; init; }
    public string? TargetType { get; init; }
    public RuntimeSource? Scope { get; init; }
    public string? ParentSourceId { get; init; }
    public string? BasedOnSourceId { get; init; }
    public SourceLocation? Location { get; init; }
    public bool? Active { get; init; }
    public required string ApplicationState { get; init; }
    public required string Priority { get; init; }
    public required string FramePriority { get; init; }
    public int FrameOrder { get; init; }
    public required IReadOnlyList<StyleSetterInfo> Setters { get; init; }
}

public sealed record StylesSnapshot(
    RuntimeSource Target,
    DateTimeOffset Timestamp,
    ClassState ClassState,
    IReadOnlyList<StyleSourceInfo> Styles,
    ProvenanceResolution Resolution);
