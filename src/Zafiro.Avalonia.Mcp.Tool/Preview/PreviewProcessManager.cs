using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text;
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

        var output = PreviewProcessOutput.Capture(process.StandardOutput, process.StandardError);

        var previewProcess = new PreviewProcess(process.Id, process, target, output);
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
                    await previewProcess.Output.WaitForDrain(TimeSpan.FromMilliseconds(500), cancellationToken);
                    var output = previewProcess.Output.Snapshot();
                    throw new PreviewValidationException(
                        "INTERNAL",
                        $"Preview host exited before publishing MCP discovery. Exit code: {previewProcess.Process.ExitCode}.",
                        details: new PreviewHostExitDetails(
                            previewProcess.Process.ExitCode,
                            output.StandardOutput,
                            output.StandardError));
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

internal sealed record PreviewProcess(int Pid, Process Process, PreviewTarget Target, PreviewProcessOutput Output);

internal sealed class PreviewProcessOutput
{
    private const int DefaultMaxCharacters = 8192;
    private readonly Task standardOutputTask;
    private readonly Task standardErrorTask;
    private readonly BoundedTextBuffer standardOutput;
    private readonly BoundedTextBuffer standardError;

    private PreviewProcessOutput(
        Task standardOutputTask,
        Task standardErrorTask,
        BoundedTextBuffer standardOutput,
        BoundedTextBuffer standardError)
    {
        this.standardOutputTask = standardOutputTask;
        this.standardErrorTask = standardErrorTask;
        this.standardOutput = standardOutput;
        this.standardError = standardError;
    }

    public static PreviewProcessOutput Empty { get; } = new(
        Task.CompletedTask,
        Task.CompletedTask,
        new BoundedTextBuffer(DefaultMaxCharacters),
        new BoundedTextBuffer(DefaultMaxCharacters));

    public static PreviewProcessOutput Capture(
        StreamReader standardOutputReader,
        StreamReader standardErrorReader,
        int maxCharacters = DefaultMaxCharacters)
    {
        var standardOutput = new BoundedTextBuffer(maxCharacters);
        var standardError = new BoundedTextBuffer(maxCharacters);
        return new PreviewProcessOutput(
            Drain(standardOutputReader, standardOutput),
            Drain(standardErrorReader, standardError),
            standardOutput,
            standardError);
    }

    public async Task WaitForDrain(TimeSpan timeout, CancellationToken cancellationToken)
    {
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        linked.CancelAfter(timeout);

        try
        {
            await Task.WhenAll(standardOutputTask, standardErrorTask).WaitAsync(linked.Token);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
        }
    }

    public PreviewProcessOutputSnapshot Snapshot() => new(standardOutput.ToString(), standardError.ToString());

    private static async Task Drain(StreamReader reader, BoundedTextBuffer buffer)
    {
        var chars = new char[1024];
        try
        {
            int read;
            while ((read = await reader.ReadAsync(chars, 0, chars.Length)) > 0)
            {
                buffer.Append(chars.AsSpan(0, read));
            }
        }
        catch
        {
        }
    }
}

internal sealed record PreviewProcessOutputSnapshot(string StandardOutput, string StandardError);

internal sealed class BoundedTextBuffer
{
    private readonly int maxCharacters;
    private readonly StringBuilder builder = new();

    public BoundedTextBuffer(int maxCharacters)
    {
        this.maxCharacters = Math.Max(1, maxCharacters);
    }

    public void Append(ReadOnlySpan<char> value)
    {
        lock (builder)
        {
            if (value.Length >= maxCharacters)
            {
                builder.Clear();
                builder.Append(value[^maxCharacters..]);
                return;
            }

            var overflow = builder.Length + value.Length - maxCharacters;
            if (overflow > 0)
            {
                builder.Remove(0, overflow);
            }

            builder.Append(value);
        }
    }

    public override string ToString()
    {
        lock (builder)
        {
            return builder.ToString();
        }
    }
}
