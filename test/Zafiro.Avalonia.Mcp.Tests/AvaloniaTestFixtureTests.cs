using Avalonia.Threading;
using Xunit;

namespace Zafiro.Avalonia.Mcp.Tests;

[Collection("Avalonia")]
public class AvaloniaTestFixtureTests
{
    [Fact]
    public async Task Dispatcher_ProcessesInvocations_FromDifferentWorkerThreads()
    {
        var first = await InvokeFromWorker();
        var second = await InvokeFromWorker();

        Assert.NotSame(first.Worker, second.Worker);
        Assert.Same(first.UiThread, second.UiThread);
    }

    [Fact]
    public async Task Dispatcher_PropagatesCallbackExceptions_AndContinuesProcessing()
    {
        var expected = new InvalidOperationException("Expected callback failure.");

        var actual = Assert.Throws<InvalidOperationException>(() =>
            Dispatcher.UIThread.Invoke(() => throw expected, DispatcherPriority.Send,
                CancellationToken.None, TimeSpan.FromSeconds(5)));

        Assert.Same(expected, actual);
        await InvokeFromWorker();
    }

    private static Task<(Thread Worker, Thread UiThread)> InvokeFromWorker()
    {
        return Task.Factory.StartNew(() =>
        {
            var worker = Thread.CurrentThread;
            var dispatcher = Dispatcher.UIThread;
            Assert.False(dispatcher.CheckAccess());

            var uiThread = dispatcher.Invoke(() =>
            {
                Assert.True(dispatcher.CheckAccess());
                return Thread.CurrentThread;
            }, DispatcherPriority.Send, CancellationToken.None, TimeSpan.FromSeconds(5));

            Assert.NotSame(worker, uiThread);
            return (worker, uiThread);
        }, CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default);
    }
}
