using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using Microsoft.CodeAnalysis.CSharp.Scripting;
using Microsoft.CodeAnalysis.Scripting;

namespace Zafiro.Avalonia.Mcp.AppHost.Selectors;

/// <summary>
/// Evaluates C# boolean predicates against live DataContext objects using Roslyn scripting.
/// Compiled scripts are cached keyed by (type, expression hash). Enforces a 200ms timeout per evaluation.
/// </summary>
public sealed class RoslynDataContextPredicateEvaluator : IDataContextPredicateEvaluator
{
    private readonly ConcurrentDictionary<string, Script<bool>> _cache = new();
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
        // GetOrAdd compiles the script synchronously on first access; subsequent calls skip compilation
        var script = _cache.GetOrAdd(cacheKey, _ => CompileScript(expression, dcType));

        // Execute on a background thread so Task.WhenAny can time out the execution
        var runTask = Task.Run(async () =>
        {
            var state = await script.RunAsync(dataContext);
            return state.ReturnValue;
        });

        if (await Task.WhenAny(runTask, Task.Delay(_timeout)) != runTask)
        {
            Console.Error.WriteLine($"[RoslynEvaluator] Timeout evaluating '{expression}'");
            return false;
        }

        return await runTask;
    }

    private static Script<bool> CompileScript(string expression, Type dcType)
    {
        var script = CreateScript(expression, dcType);
        script.Compile(); // Pre-compile so RunAsync only runs execution (no compilation overhead)
        return script;
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
}
