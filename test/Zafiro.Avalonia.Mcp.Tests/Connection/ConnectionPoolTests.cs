using System.Diagnostics;
using System.IO.Pipes;
using System.Text.Json;
using Xunit;
using Zafiro.Avalonia.Mcp.Protocol;
using Zafiro.Avalonia.Mcp.Protocol.Models;
using Zafiro.Avalonia.Mcp.Tool.Connection;

namespace Zafiro.Avalonia.Mcp.Tests.Connection;

public sealed class ConnectionPoolTests
{
    [Fact]
    public async Task ConnectExternal_ReopensConnection_AfterRequestTimeout()
    {
        var pipeName = $"zafiro-avalonia-mcp-timeout-{Guid.NewGuid():N}";
        await using var server = new TimeoutThenPingServer(pipeName);
        using var pool = new ConnectionPool();
        var info = new DiscoveryInfo
        {
            Pid = Random.Shared.Next(100_000, 999_999),
            PipeName = pipeName,
            ProcessName = "TimeoutThenPing",
            StartTime = DateTimeOffset.UtcNow,
            Transport = "pipe",
            Endpoint = pipeName
        };

        await server.WaitUntilReady();
        var first = await pool.ConnectExternal(info);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            first.SendAsync("slow", null, TimeSpan.FromMilliseconds(50)));

        var second = await pool.ConnectExternal(info);
        var response = await second.SendAsync("ping", null, TimeSpan.FromSeconds(2));

        Assert.False(ReferenceEquals(first, second));
        Assert.Equal("ok", response?.GetProperty("status").GetString());
    }

    [Fact]
    public void DiscoverApps_KeepsPipeDiscovery_OnWindows()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        using var process = Process.GetCurrentProcess();
        using var pool = new ConnectionPool();
        var pipeName = $"zafiro-avalonia-mcp-test-{Guid.NewGuid():N}";
        using var server = new NamedPipeServerStream(
            pipeName,
            PipeDirection.InOut,
            NamedPipeServerStream.MaxAllowedServerInstances,
            PipeTransmissionMode.Byte,
            PipeOptions.Asynchronous);
        var discoveryPath = Path.Combine(pool.DiscoveryDirectory, $"{process.Id}-{Guid.NewGuid():N}.json");
        Directory.CreateDirectory(pool.DiscoveryDirectory);

        var info = new DiscoveryInfo
        {
            Pid = process.Id,
            PipeName = pipeName,
            ProcessName = process.ProcessName,
            StartTime = DateTimeOffset.UtcNow,
            Transport = "pipe",
            Endpoint = pipeName
        };

        File.WriteAllText(discoveryPath, ProtocolSerializer.Serialize(info));

        try
        {
            var apps = pool.DiscoverApps();

            Assert.False(File.Exists(Path.Combine(Path.GetTempPath(), $"CoreFxPipe_{pipeName}")));
            Assert.Contains(apps, app => app.Pid == process.Id && app.PipeName == pipeName);
            Assert.True(File.Exists(discoveryPath));
        }
        finally
        {
            try
            {
                File.Delete(discoveryPath);
            }
            catch
            {
            }
        }
    }

    private sealed class TimeoutThenPingServer : IAsyncDisposable
    {
        private readonly string pipeName;
        private readonly CancellationTokenSource cts = new();
        private readonly Task serverTask;
        private readonly TaskCompletionSource ready = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int connectionCount;

        public TimeoutThenPingServer(string pipeName)
        {
            this.pipeName = pipeName;
            serverTask = Task.Run(Listen);
        }

        public async Task WaitUntilReady()
        {
            await ready.Task.WaitAsync(TimeSpan.FromSeconds(5));
        }

        private async Task Listen()
        {
            while (!cts.IsCancellationRequested)
            {
                var pipe = new NamedPipeServerStream(
                    pipeName,
                    PipeDirection.InOut,
                    NamedPipeServerStream.MaxAllowedServerInstances,
                    PipeTransmissionMode.Byte,
                    PipeOptions.Asynchronous);

                ready.TrySetResult();
                try
                {
                    await pipe.WaitForConnectionAsync(cts.Token);
                    var connectionNumber = Interlocked.Increment(ref connectionCount);
                    _ = Task.Run(() => Handle(pipe, connectionNumber));
                }
                catch (OperationCanceledException)
                {
                    await pipe.DisposeAsync();
                    break;
                }
                catch
                {
                    await pipe.DisposeAsync();
                }
            }
        }

        private async Task Handle(NamedPipeServerStream pipe, int connectionNumber)
        {
            await using (pipe)
            {
                using var reader = new StreamReader(pipe);
                await using var writer = new StreamWriter(pipe) { AutoFlush = true };
                var line = await reader.ReadLineAsync(cts.Token);
                if (connectionNumber == 1)
                {
                    await Task.Delay(Timeout.InfiniteTimeSpan, cts.Token);
                    return;
                }

                using var document = JsonDocument.Parse(line!);
                var id = document.RootElement.GetProperty("id").GetString();
                await writer.WriteLineAsync(JsonSerializer.Serialize(new
                {
                    id,
                    result = new { status = "ok" },
                }));
            }
        }

        public async ValueTask DisposeAsync()
        {
            cts.Cancel();
            try
            {
                await serverTask.WaitAsync(TimeSpan.FromSeconds(5));
            }
            catch
            {
            }
            finally
            {
                cts.Dispose();
            }
        }
    }
}
