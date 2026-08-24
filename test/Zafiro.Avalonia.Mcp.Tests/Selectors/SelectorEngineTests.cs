using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;
using Avalonia.VisualTree;
using System.Reflection;
using System.Runtime.Loader;
using Xunit;
using Zafiro.Avalonia.Mcp.AppHost.Handlers;
using Zafiro.Avalonia.Mcp.AppHost.Selectors;

namespace Zafiro.Avalonia.Mcp.Tests.Selectors;

[Collection("Avalonia")]
public class SelectorEngineTests
{
    private readonly SelectorEngine _engine = new();

    private static T Run<T>(Func<T> f) => Dispatcher.UIThread.Invoke(f);
    private static void Run(Action action) => Dispatcher.UIThread.Invoke(action);

    [Fact]
    public void Default_CanBeRead_WhenRoslynAssembliesAreUnavailable()
    {
        var appHostPath = typeof(SelectorEngine).Assembly.Location;
        var context = new CodeAnalysisBlockingLoadContext(appHostPath);

        try
        {
            var assembly = context.LoadFromAssemblyPath(appHostPath);
            var type = assembly.GetType(typeof(SelectorEngine).FullName!, throwOnError: true)!;
            var property = type.GetProperty(nameof(SelectorEngine.Default), BindingFlags.Public | BindingFlags.Static)!;

            var value = property.GetValue(null);

            Assert.NotNull(value);
        }
        finally
        {
            context.Unload();
        }
    }

    [Fact]
    public void Resolves_ByType_FromScope()
    {
        var (root, save) = BuildTree();
        var matches = Run(() => _engine.Resolve("Button", root));
        Assert.Equal(2, matches.Count); // save + disabled
        Assert.Contains(save, matches);
    }

    [Fact]
    public void Resolves_ByName()
    {
        var (root, _) = BuildTree();
        var matches = Run(() => _engine.Resolve("[Name=SaveBtn]", root));
        Assert.Single(matches);
        Assert.Equal("SaveBtn", ((Control)matches[0]).Name);
    }

    [Fact]
    public void Resolves_HashName_Shorthand()
    {
        var (root, _) = BuildTree();
        var matches = Run(() => _engine.Resolve("#SaveBtn", root));
        Assert.Single(matches);
    }

    [Fact]
    public void Resolves_HasText()
    {
        var (root, _) = BuildTree();
        var matches = Run(() => _engine.Resolve("Button:has-text(\"Save\")", root));
        Assert.Single(matches);
    }

    [Fact]
    public void Resolves_Disabled()
    {
        var (root, _) = BuildTree();
        var matches = Run(() => _engine.Resolve("Button:disabled", root));
        Assert.Single(matches);
        Assert.Equal("DisabledBtn", ((Control)matches[0]).Name);
    }

    [Fact]
    public void Resolves_Enabled()
    {
        var (root, _) = BuildTree();
        var matches = Run(() => _engine.Resolve("Button:enabled", root));
        // SaveBtn is enabled, DisabledBtn is not
        Assert.Single(matches);
        Assert.Equal("SaveBtn", ((Control)matches[0]).Name);
    }

    [Fact]
    public void Resolves_Descendant_Implicit()
    {
        var (root, _) = BuildTree();
        var matches = Run(() => _engine.Resolve("StackPanel TextBox", root));
        Assert.Single(matches);
        Assert.IsType<TextBox>(matches[0]);
    }

    [Fact]
    public void Resolves_Descendant_Explicit()
    {
        var (root, _) = BuildTree();
        var matches = Run(() => _engine.Resolve("StackPanel >> Button", root));
        Assert.Equal(2, matches.Count); // Both buttons
    }

    [Fact]
    public void Resolves_Nth()
    {
        var (root, _) = BuildTree();
        var matches = Run(() => _engine.Resolve("Button:nth(1)", root));
        Assert.Single(matches);
        Assert.Equal("DisabledBtn", ((Control)matches[0]).Name);
    }

    [Fact]
    public void Resolves_NodeId()
    {
        var (root, _) = BuildTree();
        var btn = Run(() => root.GetVisualDescendants().OfType<Button>().First());
        var nodeId = Run(() => NodeRegistry.GetOrRegister(btn));
        var matches = Run(() => _engine.Resolve($"#{nodeId}", root));
        Assert.Single(matches);
        Assert.Same(btn, matches[0]);
    }

    [Fact]
    public void Resolves_Alternatives_DedupesByReference()
    {
        var (root, _) = BuildTree();
        // Both alternatives match SaveBtn; should appear once.
        var matches = Run(() => _engine.Resolve("Button[Name=SaveBtn], #SaveBtn", root));
        Assert.Single(matches);
    }

    [Fact]
    public void Resolves_DataContextEquality()
    {
        var (root, _) = Run(() =>
        {
            var item1 = new Button { Name = "B1", DataContext = new VmRow(1, "Alice") };
            var item2 = new Button { Name = "B2", DataContext = new VmRow(2, "Bob") };
            var sp = new StackPanel { Children = { item1, item2 } };
            return ((Visual)sp, item1);
        });

        var matches = Run(() => _engine.Resolve("Button[dc.Id=2]", root));
        Assert.Single(matches);
        Assert.Equal("B2", ((Control)matches[0]).Name);
    }

    [Fact]
    public void Resolves_Predicate_WithEvaluator()
    {
        var engine = new SelectorEngine(new StubPredicateEvaluator());
        var (root, _) = Run(() =>
        {
            var item1 = new Button { Name = "B1", DataContext = new VmRow(1, "Alice") };
            var item2 = new Button { Name = "B2", DataContext = new VmRow(42, "Bob") };
            var sp = new StackPanel { Children = { item1, item2 } };
            return ((Visual)sp, item1);
        });

        var matches = Run(() => engine.Resolve("Button[dc:'Id == 42']", root));
        Assert.Single(matches);
        Assert.Equal("B2", ((Control)matches[0]).Name);
    }

    [Fact]
    public async Task Resolves_DataContextPredicateAcrossManyIncompatibleTypes_AndFindsCompatibleMatch()
    {
        var compilationAttempts = 0;
        var compilationStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var evaluator = new RoslynDataContextPredicateEvaluator(
            () =>
            {
                Interlocked.Increment(ref compilationAttempts);
                compilationStarted.TrySetResult();
            });
        var dispatcher = new QueuedUiDispatcher();
        var engine = new SelectorEngine(evaluator, dispatcher);
        var panel = new StackPanel();
        foreach (var index in Enumerable.Range(0, 250))
        {
            panel.Children.Add(new Button { DataContext = new OtherVm($"item-{index}") });
        }

        var target = new Button { Name = "Match", DataContext = new TestVm(42, true, "Alice") };
        panel.Children.Add(target);
        var root = (Visual)panel;

        var resolveTask = engine.ResolveDataContextAsync("Button", "Id == 42", root);
        dispatcher.ExecuteNext();
        await compilationStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
        var matches = await resolveTask.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.Single(matches);
        Assert.Same(target, matches[0]);
        Assert.Equal(2, compilationAttempts);
    }

    [Fact]
    public async Task ResolveDataContextAsync_AllowsDispatcherToProgressWhilePredicateIsPending()
    {
        var evaluator = new PendingAsyncPredicateEvaluator();
        var dispatcher = new QueuedUiDispatcher();
        var engine = new SelectorEngine(evaluator, dispatcher);
        var root = (Visual)new StackPanel
        {
            Children = { new Button { DataContext = new TestVm(42, true, "Alice") } }
        };

        var resolveTask = engine.ResolveDataContextAsync("Button", "Id == 42", root);
        try
        {
            dispatcher.ExecuteNext();
            await evaluator.Started.Task.WaitAsync(TimeSpan.FromSeconds(5));

            var dispatcherProgressed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var markerTask = dispatcher.InvokeAsync(() =>
            {
                dispatcherProgressed.SetResult();
                return true;
            });
            dispatcher.ExecuteNext();
            await dispatcherProgressed.Task.WaitAsync(TimeSpan.FromSeconds(5));
            await markerTask;

            evaluator.Release.TrySetResult(true);
            var matches = await resolveTask.WaitAsync(TimeSpan.FromSeconds(5));

            Assert.Single(matches);
        }
        finally
        {
            evaluator.Release.TrySetResult(true);
        }
    }

    [Fact]
    public void Predicate_WithoutEvaluator_NoMatch()
    {
        var (root, _) = Run(() =>
        {
            var item = new Button { DataContext = new VmRow(1, "Alice") };
            var sp = new StackPanel { Children = { item } };
            return ((Visual)sp, item);
        });

        var matches = Run(() => _engine.Resolve("Button[dc:'anything']", root));
        Assert.Empty(matches);
    }

    [Fact]
    public void Resolves_Visible_FiltersOutHidden()
    {
        var (root, _) = Run(() =>
        {
            var hidden = new Button { Name = "Hidden", IsVisible = false };
            var shown = new Button { Name = "Shown" };
            var sp = new StackPanel { Children = { hidden, shown } };
            return ((Visual)sp, shown);
        });

        var matches = Run(() => _engine.Resolve("Button:visible", root));
        Assert.Single(matches);
        Assert.Equal("Shown", ((Control)matches[0]).Name);
    }

    [Fact]
    public void NoMatch_Empty()
    {
        var (root, _) = BuildTree();
        var matches = Run(() => _engine.Resolve("ListBoxItem", root));
        Assert.Empty(matches);
    }

    private static (Visual root, Button save) BuildTree() => Run(() =>
    {
        var save = new Button { Name = "SaveBtn", Content = "Save" };
        var disabled = new Button { Name = "DisabledBtn", Content = "Cancel", IsEnabled = false };
        var input = new TextBox { Name = "Input", Text = "" };
        var sp = new StackPanel { Children = { save, disabled, input } };
        return ((Visual)sp, save);
    });

    private sealed record VmRow(int Id, string Name);

    private sealed class StubPredicateEvaluator : IDataContextPredicateEvaluator
    {
        public bool Evaluate(string expression, object dataContext)
        {
            // Minimal: parses "Id == N"
            var trimmed = expression.Replace(" ", "");
            if (trimmed.StartsWith("Id==") && int.TryParse(trimmed[4..], out var n))
            {
                var prop = dataContext.GetType().GetProperty("Id");
                if (prop is null) return false;
                var value = prop.GetValue(dataContext);
                return value is int i && i == n;
            }
            return false;
        }
    }

    private sealed class PendingAsyncPredicateEvaluator : IDataContextPredicateEvaluator, IAsyncDataContextPredicateEvaluator
    {
        public TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource<bool> Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public bool Evaluate(string expression, object dataContext) => throw new NotSupportedException();

        public async Task<bool> EvaluateAsync(string expression, object dataContext)
        {
            Started.TrySetResult();
            await Release.Task.ConfigureAwait(false);
            return true;
        }
    }

    private sealed class QueuedUiDispatcher : IUiDispatcher
    {
        private readonly Queue<Action> _actions = new();

        public Task<T> InvokeAsync<T>(Func<T> action)
        {
            var completion = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
            _actions.Enqueue(() =>
            {
                try { completion.SetResult(action()); }
                catch (Exception ex) { completion.SetException(ex); }
            });
            return completion.Task;
        }

        public void ExecuteNext()
        {
            Assert.NotEmpty(_actions);
            _actions.Dequeue().Invoke();
        }
    }

    private sealed class CodeAnalysisBlockingLoadContext : AssemblyLoadContext
    {
        private readonly AssemblyDependencyResolver _resolver;

        public CodeAnalysisBlockingLoadContext(string appHostAssemblyPath) : base(isCollectible: true)
        {
            _resolver = new AssemblyDependencyResolver(appHostAssemblyPath);
        }

        protected override Assembly? Load(AssemblyName assemblyName)
        {
            if (assemblyName.Name is { } name &&
                name.StartsWith("Microsoft.CodeAnalysis", StringComparison.Ordinal))
            {
                throw new FileNotFoundException($"Blocked for test: {name}");
            }

            var path = _resolver.ResolveAssemblyToPath(assemblyName);
            return path is null ? null : LoadFromAssemblyPath(path);
        }
    }
}
