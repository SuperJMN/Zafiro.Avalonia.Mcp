using Avalonia.Controls;
using AvaloniaMcp.AppHost.Handlers;
using Xunit;

namespace AvaloniaMcp.Tests.Handlers;

[Collection("Avalonia")]
public class NodeRegistryTests
{
    public NodeRegistryTests(AvaloniaTestFixture _)
    {
        NodeRegistry.Clear();
    }

    [Fact]
    public void Register_AssignsUniqueIds()
    {
        var a = new Control();
        var b = new Control();
        var c = new Control();

        var idA = NodeRegistry.Register(a);
        var idB = NodeRegistry.Register(b);
        var idC = NodeRegistry.Register(c);

        Assert.NotEqual(idA, idB);
        Assert.NotEqual(idB, idC);
        Assert.NotEqual(idA, idC);
    }

    [Fact]
    public void Resolve_ReturnsRegisteredVisual()
    {
        var control = new Control();
        var id = NodeRegistry.Register(control);

        var resolved = NodeRegistry.Resolve(id);

        Assert.Same(control, resolved);
    }

    [Fact]
    public void Resolve_ReturnsNull_ForUnknownId()
    {
        var result = NodeRegistry.Resolve(99999);

        Assert.Null(result);
    }

    [Fact]
    public void GetOrRegister_ReturnsSameId_ForSameVisual()
    {
        var control = new Control();
        var id1 = NodeRegistry.Register(control);
        var id2 = NodeRegistry.GetOrRegister(control);

        Assert.Equal(id1, id2);
    }

    [Fact]
    public void GetOrRegister_RegistersNew_ForNewVisual()
    {
        var control = new Control();
        var id = NodeRegistry.GetOrRegister(control);

        var resolved = NodeRegistry.Resolve(id);
        Assert.Same(control, resolved);
    }

    [Fact]
    public void Clear_RemovesAllNodes()
    {
        var a = new Control();
        var b = new Control();
        var idA = NodeRegistry.Register(a);
        var idB = NodeRegistry.Register(b);

        NodeRegistry.Clear();

        Assert.Null(NodeRegistry.Resolve(idA));
        Assert.Null(NodeRegistry.Resolve(idB));
    }
}
