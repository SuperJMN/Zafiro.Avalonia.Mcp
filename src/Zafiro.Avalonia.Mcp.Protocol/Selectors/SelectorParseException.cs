namespace Zafiro.Avalonia.Mcp.Protocol.Selectors;

public sealed class SelectorParseException : Exception
{
    public int Position { get; }

    public SelectorParseException(string message, int position)
        : base($"Selector parse error at position {position}: {message}")
    {
        Position = position;
    }
}
