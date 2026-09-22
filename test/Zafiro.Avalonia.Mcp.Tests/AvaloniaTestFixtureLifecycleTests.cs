using Avalonia;
using Avalonia.Threading;
using Xunit;

namespace Zafiro.Avalonia.Mcp.Tests;

[Collection("Avalonia lifecycle")]
public class AvaloniaTestFixtureLifecycleTests
{
    [Fact]
    public async Task DisposeAsync_StopsDispatcher_AndAllowsAnotherFixtureToStart()
    {
        Dispatcher? previous = null;
        for (var i = 0; i < 2; i++)
        {
            var fixture = new AvaloniaTestFixture();
            var stopped = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            try
            {
                await fixture.InitializeAsync();
                var dispatcher = Dispatcher.UIThread;
                Assert.NotSame(previous, dispatcher);
                await dispatcher.InvokeAsync(() =>
                {
                    Assert.True(dispatcher.CheckAccess());
                    dispatcher.ShutdownFinished += (_, _) => stopped.TrySetResult();
                }).GetTask().WaitAsync(TimeSpan.FromSeconds(5));
                previous = dispatcher;
            }
            finally
            {
                await fixture.DisposeAsync();
            }

            Assert.True(stopped.Task.IsCompletedSuccessfully);
            await fixture.DisposeAsync();
        }
    }

    [Fact]
    public async Task InitializeAsync_PropagatesApplicationStartupFailure_AndCleansUp()
    {
        var fixture = new AvaloniaTestFixture(typeof(FailingApp));
        try
        {
            var exception = await Assert.ThrowsAsync<InvalidOperationException>(fixture.InitializeAsync);
            Assert.Equal("Expected startup failure.", exception.Message);
        }
        finally
        {
            await fixture.DisposeAsync();
        }

        var nextFixture = new AvaloniaTestFixture();
        try
        {
            await nextFixture.InitializeAsync();
            Assert.True(await Dispatcher.UIThread.InvokeAsync(() => Dispatcher.UIThread.CheckAccess())
                .GetTask().WaitAsync(TimeSpan.FromSeconds(5)));
        }
        finally
        {
            await nextFixture.DisposeAsync();
        }
    }

    public class FailingApp : Application
    {
        public override void Initialize() => throw new InvalidOperationException("Expected startup failure.");
    }
}

// These tests replace Avalonia's process-wide application and dispatcher.
[CollectionDefinition("Avalonia lifecycle", DisableParallelization = true)]
public class AvaloniaLifecycleCollection { }
