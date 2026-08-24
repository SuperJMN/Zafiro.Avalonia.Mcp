using System.Collections.Concurrent;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Scripting;
using Microsoft.CodeAnalysis.Scripting;

namespace Zafiro.Avalonia.Mcp.AppHost.Selectors;

/// <summary>
/// Evaluates C# boolean predicates against live DataContext objects using Roslyn scripting.
/// Compiled scripts are cached keyed by runtime type and expression. Enforces a 200ms timeout per evaluation.
/// </summary>
public sealed class RoslynDataContextPredicateEvaluator : IDataContextPredicateEvaluator, IAsyncDataContextPredicateEvaluator
{
    private readonly ConcurrentDictionary<PredicateCacheKey, Lazy<Task<CachedCompilation>>> _cache = new();
    private readonly Action? _compilationAttempted;
    private readonly TimeSpan _timeout;

    public RoslynDataContextPredicateEvaluator(TimeSpan? timeout = null)
        : this(null, timeout)
    {
    }

    internal RoslynDataContextPredicateEvaluator(Action? compilationAttempted, TimeSpan? timeout = null)
    {
        _compilationAttempted = compilationAttempted;
        _timeout = timeout ?? TimeSpan.FromMilliseconds(200);
    }

    public bool Evaluate(string expression, object dataContext)
    {
        try
        {
            return EvaluateAsyncCore(expression, dataContext).GetAwaiter().GetResult();
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[RoslynEvaluator] Error evaluating '{expression}': {ex.Message}");
            return false;
        }
    }

    Task<bool> IAsyncDataContextPredicateEvaluator.EvaluateAsync(string expression, object dataContext) =>
        EvaluateAsyncCore(expression, dataContext);

    internal async Task<bool> EvaluateAsyncCore(string expression, object dataContext)
    {
        try
        {
            var dcType = dataContext.GetType();
            var cacheKey = new PredicateCacheKey(dcType, expression);
            // GetOrAdd may invoke its value factory more than once, so cache a Lazy to ensure
            // compilation runs only once per DataContext type and expression.
            var compilation = await _cache.GetOrAdd(cacheKey, _ => new Lazy<Task<CachedCompilation>>(
                () => Task.Run(() => CompileScript(expression, dcType)),
                LazyThreadSafetyMode.ExecutionAndPublication)).Value.ConfigureAwait(false);
            if (compilation.Script is null)
                return false;

            var runTask = Task.Run(async () =>
            {
                var state = await compilation.Script.RunAsync(dataContext).ConfigureAwait(false);
                return state.ReturnValue;
            });

            if (await Task.WhenAny(runTask, Task.Delay(_timeout)).ConfigureAwait(false) != runTask)
            {
                Console.Error.WriteLine($"[RoslynEvaluator] Timeout evaluating '{expression}'");
                return false;
            }

            return await runTask.ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[RoslynEvaluator] Error evaluating '{expression}': {ex.Message}");
            return false;
        }
    }

    private CachedCompilation CompileScript(string expression, Type dcType)
    {
        _compilationAttempted?.Invoke();

        try
        {
            var script = CreateScript(expression, dcType);
            var diagnostics = script.Compile();
            if (diagnostics.Any(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error))
                return new CachedCompilation(null);

            return new CachedCompilation(script);
        }
        catch (CompilationErrorException)
        {
            return new CachedCompilation(null);
        }
    }

    private static Script<bool> CreateScript(string expression, Type dcType)
    {
        var options = ScriptOptions.Default
            .WithReferences(
                typeof(object).Assembly,
                typeof(Enumerable).Assembly,
                dcType.Assembly)
            .WithImports(
                "System",
                "System.Linq",
                "System.Collections.Generic");

        if (!string.IsNullOrEmpty(dcType.Namespace))
            options = options.AddImports(dcType.Namespace);

        return CSharpScript.Create<bool>(
            $"return ({expression});",
            options,
            globalsType: dcType);
    }

    private sealed record CachedCompilation(Script<bool>? Script);
    private readonly record struct PredicateCacheKey(Type DataContextType, string Expression);
}
