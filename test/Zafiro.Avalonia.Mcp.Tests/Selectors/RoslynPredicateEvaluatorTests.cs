using System.Diagnostics;
using Xunit;
using Zafiro.Avalonia.Mcp.AppHost.Selectors;

namespace Zafiro.Avalonia.Mcp.Tests.Selectors;

// Must be public so Roslyn scripting can access its properties when used as globalsType
public sealed record TestVm(int Id, bool IsActive, string Name);

public class RoslynPredicateEvaluatorTests
{
    private readonly RoslynDataContextPredicateEvaluator _evaluator = new();

    [Fact]
    public void Evaluate_SimpleEquality_ReturnsTrue()
    {
        var vm = new TestVm(42, true, "Alice");
        Assert.True(_evaluator.Evaluate("Id == 42", vm));
    }

    [Fact]
    public void Evaluate_SimpleEquality_ReturnsFalse()
    {
        var vm = new TestVm(1, true, "Alice");
        Assert.False(_evaluator.Evaluate("Id == 42", vm));
    }

    [Fact]
    public void Evaluate_CompoundPredicate_ReturnsTrue()
    {
        var vm = new TestVm(42, true, "Alice");
        Assert.True(_evaluator.Evaluate("Id == 42 && IsActive", vm));
    }

    [Fact]
    public void Evaluate_CompoundPredicate_ReturnsFalse_WhenPartFails()
    {
        var vm = new TestVm(42, false, "Alice");
        Assert.False(_evaluator.Evaluate("Id == 42 && IsActive", vm));
    }

    [Fact]
    public void Evaluate_StringEquality_ReturnsTrue()
    {
        var vm = new TestVm(1, true, "Alice");
        Assert.True(_evaluator.Evaluate("Name == \"Alice\"", vm));
    }

    [Fact]
    public void Evaluate_InvalidSyntax_ReturnsFalse()
    {
        var vm = new TestVm(1, true, "Alice");
        Assert.False(_evaluator.Evaluate("this is not valid C# !!!", vm));
    }

    [Fact]
    public void Evaluate_CachesCompilation_SameResultOnSecondCall()
    {
        var vm = new TestVm(5, true, "Test");
        Assert.True(_evaluator.Evaluate("Id == 5", vm));
        Assert.True(_evaluator.Evaluate("Id == 5", vm));
    }

    [Fact]
    public void Evaluate_Timeout_ReturnsFalse()
    {
        // Use a short 50ms timeout so the test completes quickly
        var evaluator = new RoslynDataContextPredicateEvaluator(timeout: TimeSpan.FromMilliseconds(50));
        var vm = new TestVm(1, true, "Alice");
        var sw = Stopwatch.StartNew();
        // Lambda that sleeps 300ms — exceeds the 50ms timeout, but background thread finishes within ~300ms
        var result = evaluator.Evaluate(
            "((System.Func<bool>)(() => { System.Threading.Thread.Sleep(300); return true; }))()", vm);
        sw.Stop();
        Assert.False(result);
        Assert.True(sw.ElapsedMilliseconds < 500, $"Should time out quickly, took {sw.ElapsedMilliseconds}ms");
    }

    [Fact]
    public void Evaluate_NullPropertyAccess_DoesNotThrow()
    {
        var vm = new TestVm(1, true, "Alice");
        // Accessing a non-existent property produces a compile error → false
        var result = _evaluator.Evaluate("NonExistentProperty == 99", vm);
        Assert.False(result);
    }
}
