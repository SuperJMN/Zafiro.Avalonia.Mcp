using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Scripting;
using Microsoft.CodeAnalysis.Scripting;

namespace Zafiro.Avalonia.Mcp.AppHost.Selectors;

/// <summary>
/// Evaluates C# boolean predicates against live DataContext objects using Roslyn scripting.
/// Compiled scripts are cached keyed by (type, expression hash). Enforces a 200ms timeout per evaluation.
/// </summary>
public sealed class RoslynDataContextPredicateEvaluator : IDataContextPredicateEvaluator
{
    private readonly ConcurrentDictionary<string, CachedCompilation> _cache = new();
    private readonly TimeSpan _timeout;

    public RoslynDataContextPredicateEvaluator(TimeSpan? timeout = null)
    {
        _timeout = timeout ?? TimeSpan.FromMilliseconds(200);
    }

    public bool Evaluate(string expression, object dataContext)
    {
        try
        {
            return EvaluateAsync(expression, dataContext).GetAwaiter().GetResult();
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[RoslynEvaluator] Error evaluating '{expression}': {ex.Message}");
            return false;
        }
    }

    private async Task<bool> EvaluateAsync(string expression, object dataContext)
    {
        var dcType = dataContext.GetType();
        var cacheKey = BuildCacheKey(dcType, expression);
        // GetOrAdd caches both valid scripts and compilation failures, so an incompatible
        // DataContext type is only compiled once for each expression.
        var compilation = _cache.GetOrAdd(cacheKey, _ => CompileScript(expression, dcType));
        if (compilation.Script is null)
            return false;

        // Execute on a background thread so Task.WhenAny can time out the execution
        var runTask = Task.Run(async () =>
        {
            var state = await compilation.Script.RunAsync(dataContext);
            return state.ReturnValue;
        });

        if (await Task.WhenAny(runTask, Task.Delay(_timeout)) != runTask)
        {
            Console.Error.WriteLine($"[RoslynEvaluator] Timeout evaluating '{expression}'");
            return false;
        }

        return await runTask;
    }

    private static CachedCompilation CompileScript(string expression, Type dcType)
    {
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

    private static string BuildCacheKey(Type dcType, string expression)
    {
        var typeName = dcType.FullName ?? dcType.Name;
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(expression));
        return $"{typeName}|{Convert.ToHexString(hash)}";
    }

    private sealed record CachedCompilation(Script<bool>? Script);
}
