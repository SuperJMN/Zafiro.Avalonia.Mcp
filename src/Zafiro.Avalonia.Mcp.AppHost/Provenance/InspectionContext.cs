using Zafiro.Avalonia.Mcp.Protocol.Models;

namespace Zafiro.Avalonia.Mcp.AppHost.Provenance;

internal sealed class InspectionContext
{
    public const int MaxItems = 256;
    public const int MaximumDepth = 32;
    public DateTimeOffset Timestamp { get; } = DateTimeOffset.UtcNow;
    public int MaxDepth { get; }
    public bool Truncated { get; private set; }
    private readonly List<UnavailableEvidence> _unavailable = [];

    public InspectionContext(int maxDepth)
    {
        MaxDepth = maxDepth;
    }

    public void Unavailable(string evidence, string reason)
    {
        var item = new UnavailableEvidence(evidence, reason);
        if (!_unavailable.Contains(item) && _unavailable.Count < MaxItems)
            _unavailable.Add(item);
    }

    public void Truncate(string evidence)
    {
        Truncated = true;
        Unavailable(evidence, "The inspection limit was reached.");
    }

    public ProvenanceResolution Resolution() =>
        new(_unavailable.Count == 0 ? "complete" : "partial", _unavailable.ToArray(), Truncated);
}
