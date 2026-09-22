using Avalonia;
using Avalonia.Headless;
using Avalonia.Threading;
using Xunit;

namespace Zafiro.Avalonia.Mcp.Tests;

public class AvaloniaTestFixture : IAsyncLifetime
{
    private static readonly TimeSpan LifecycleTimeout = TimeSpan.FromSeconds(5);
    private readonly HeadlessUnitTestSession _session;
    private readonly CancellationTokenSource _shutdown = new();
    private Task _runTask = Task.CompletedTask;
    private bool _disposed;

    public AvaloniaTestFixture() : this(typeof(TestApp)) { }

    internal AvaloniaTestFixture(Type applicationType)
    {
        _session = HeadlessUnitTestSession.StartNew(applicationType);
    }

    public async Task InitializeAsync()
    {
        var ready = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        // Keep the session's UI thread pumping for the collection, not just the initializing xUnit worker.
        _runTask = _session.Dispatch(() =>
        {
            var dispatcher = Dispatcher.UIThread;
            dispatcher.Post(() => ready.TrySetResult());
            dispatcher.MainLoop(_shutdown.Token);
        }, CancellationToken.None);

        try
        {
            var completed = await Task.WhenAny(ready.Task, _runTask).WaitAsync(LifecycleTimeout);
            await completed;
            if (completed == _runTask)
                throw new InvalidOperationException("The Avalonia dispatcher stopped during startup.");
        }
        catch
        {
            // Session disposal also observes application setup failures outside the dispatched callback.
            await DisposeAsync();
            throw;
        }
    }

    public async Task DisposeAsync()
    {
        if (_disposed)
            return;

        _disposed = true;
        _shutdown.Cancel();
        try
        {
            await _session.DisposeAsync().AsTask().WaitAsync(LifecycleTimeout);
            await _runTask.WaitAsync(LifecycleTimeout);
        }
        finally
        {
            _shutdown.Dispose();
        }
    }
}

public class TestApp : Application { }

[CollectionDefinition("Avalonia")]
public class AvaloniaCollection : ICollectionFixture<AvaloniaTestFixture> { }
