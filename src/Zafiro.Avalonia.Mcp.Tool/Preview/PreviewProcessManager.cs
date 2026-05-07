using System.Collections.Concurrent;
using System.Diagnostics;
using Zafiro.Avalonia.Mcp.Protocol;
using Zafiro.Avalonia.Mcp.Tool.Connection;

namespace Zafiro.Avalonia.Mcp.Tool.Preview;

public sealed class PreviewProcessManager : IDisposable
{
    private readonly PreviewHostProjectBuilder hostBuilder;
    private readonly TimeSpan discoveryTimeout;
    private readonly TimeSpan pollInterval;
    private readonly ConcurrentDictionary<int, PreviewProcess> processes = new();

    public PreviewProcessManager()
        : this(new PreviewHostProjectBuilder(new DotnetProcessRunner()), TimeSpan.FromSeconds(30), TimeSpan.FromMilliseconds(150))
    {
    }

    internal PreviewProcessManager(TimeSpan discoveryTimeout, TimeSpan pollInterval)
        : this(new PreviewHostProjectBuilder(new DotnetProcessRunner()), discoveryTimeout, pollInterval)
    {
    }

    internal PreviewProcessManager(
        PreviewHostProjectBuilder hostBuilder,
        TimeSpan discoveryTimeout,
        TimeSpan pollInterval)
    {
        this.hostBuilder = hostBuilder;
        this.discoveryTimeout = discoveryTimeout;
        this.pollInterval = pollInterval;
    }

    internal async Task<PreviewProcess> Launch(
        PreviewTarget target,
        int width,
        int height,
        CancellationToken cancellationToken)
    {
        var launch = await hostBuilder.Build(target, width, height, cancellationToken);

        var process = new Process
        {
            StartInfo = launch.StartInfo,
            EnableRaisingEvents = true,
        };

        if (!process.Start())
        {
            throw new PreviewValidationException("INTERNAL", "Failed to start the AXAML preview host process.");
        }

        _ = Drain(process.StandardOutput);
        _ = Drain(process.StandardError);

        var previewProcess = new PreviewProcess(process.Id, process, target);
        processes[process.Id] = previewProcess;

        return previewProcess;
    }

    internal async Task<AppConnection> WaitForConnection(
        PreviewProcess previewProcess,
        ConnectionPool pool,
        CancellationToken cancellationToken)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(discoveryTimeout);

        try
        {
            while (true)
            {
                if (previewProcess.Process.HasExited)
                {
                    throw new PreviewValidationException(
                        "INTERNAL",
                        $"Preview host exited before publishing MCP discovery. Exit code: {previewProcess.Process.ExitCode}.");
                }

                var app = pool.DiscoverApps().FirstOrDefault(x => x.Pid == previewProcess.Pid);
                if (app is not null)
                {
                    try
                    {
                        var connection = await pool.Connect(previewProcess.Pid);
                        await connection.SendAsync(ProtocolMethods.Ping, null, timeout.Token);
                        return connection;
                    }
                    catch (OperationCanceledException)
                    {
                        throw;
                    }
                    catch
                    {
                    }
                }

                await Task.Delay(pollInterval, timeout.Token);
            }
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested && timeout.IsCancellationRequested)
        {
            throw new PreviewValidationException("TIMEOUT", "Timed out waiting for the AXAML preview host MCP discovery.");
        }
    }

    internal IReadOnlyList<int> Close(int pid, ConnectionPool pool)
    {
        var targets = pid == 0
            ? processes.Keys.ToArray()
            : [pid];

        var closed = new List<int>();
        foreach (var targetPid in targets)
        {
            if (!processes.TryRemove(targetPid, out var preview))
            {
                continue;
            }

            pool.Disconnect(targetPid);
            DeleteDiscoveryFile(pool, targetPid);
            CloseProcess(preview.Process);
            closed.Add(targetPid);
        }

        return closed;
    }

    public void Dispose()
    {
        foreach (var preview in processes.Values)
        {
            CloseProcess(preview.Process);
        }

        processes.Clear();
    }

    private static async Task Drain(StreamReader reader)
    {
        try
        {
            while (await reader.ReadLineAsync() is not null)
            {
            }
        }
        catch
        {
        }
    }

    private static void CloseProcess(Process process)
    {
        try
        {
            if (process.HasExited)
            {
                return;
            }

            process.CloseMainWindow();
            if (!process.WaitForExit(2000))
            {
                process.Kill(entireProcessTree: true);
                process.WaitForExit(2000);
            }
        }
        catch
        {
        }
        finally
        {
            process.Dispose();
        }
    }

    private static void DeleteDiscoveryFile(ConnectionPool pool, int pid)
    {
        try
        {
            var path = Path.Combine(pool.DiscoveryDirectory, $"{pid}.json");
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch
        {
        }
    }
}

internal sealed record PreviewProcess(int Pid, Process Process, PreviewTarget Target);
