using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text;
using Zafiro.Avalonia.Mcp.Protocol;
using Zafiro.Avalonia.Mcp.Protocol.Messages;
using Zafiro.Avalonia.Mcp.Tool.Connection;

namespace Zafiro.Avalonia.Mcp.Tool.Preview;

public sealed class PreviewProcessManager : IDisposable
{
    private readonly PreviewHostProjectBuilder hostBuilder;
    private readonly TimeSpan discoveryTimeout;
    private readonly TimeSpan pollInterval;
    private readonly ConcurrentDictionary<int, PreviewProcess> processes = new();
    private const string GenericPreviewHostExitSuggestion = "Inspect the preview host standardError/standardOutput details to distinguish AXAML load failures, app startup failures, and environment failures.";
    private const string MissingAssemblyPreviewHostExitSuggestion = "The preview host is missing an assembly. In multi-project Avalonia apps, pass projectPath for the executable Desktop host project, not the shared UI class library, or pass assemblyPath pointing at the built executable host assembly output. Inspect standardError/standardOutput for the exact missing assembly.";
    private static readonly TimeSpan ReadinessProbeTimeout = TimeSpan.FromSeconds(2);
    private static readonly TimeSpan ExitDrainTimeout = TimeSpan.FromMilliseconds(500);

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
            throw new PreviewValidationException(DiagnosticErrorCodes.Internal, "Failed to start the AXAML preview host process.");
        }

        var output = PreviewProcessOutput.Capture(process.StandardOutput, process.StandardError);

        var previewProcess = new PreviewProcess(process.Id, process, target, output, launch.ProjectPath);
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
                pool.RegisterConnectionFailureDetails(
                    previewProcess.Pid,
                    cancellationToken => GetConnectionFailureDetails(previewProcess.Pid, cancellationToken));

                await ThrowIfExited(
                    previewProcess,
                    "Preview host exited before publishing MCP discovery.",
                    connected: false,
                    cancellationToken);

                var app = pool.DiscoverApps().FirstOrDefault(x => x.Pid == previewProcess.Pid);
                if (app is not null)
                {
                    try
                    {
                        var connection = await pool.Connect(previewProcess.Pid);
                        await connection.SendAsync(ProtocolMethods.Ping, null, timeout.Token);
                        if (await IsPreviewReady(connection, previewProcess, timeout.Token))
                        {
                            return connection;
                        }
                    }
                    catch (OperationCanceledException)
                    {
                        throw;
                    }
                    catch
                    {
                        await ThrowIfExited(
                            previewProcess,
                            "Preview host exited before the preview window became ready.",
                            connected: true,
                            cancellationToken);
                        pool.Disconnect(previewProcess.Pid);
                    }
                }

                await Task.Delay(pollInterval, timeout.Token);
            }
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested && timeout.IsCancellationRequested)
        {
            throw new PreviewValidationException(
                DiagnosticErrorCodes.Timeout,
                "Timed out waiting for the AXAML preview host to publish MCP discovery and answer get_snapshot.");
        }
    }

    internal async Task<string?> GetConnectionFailureDetails(int pid, CancellationToken cancellationToken)
    {
        if (!processes.TryGetValue(pid, out var preview))
        {
            return null;
        }

        var details = await TryGetExitDetails(preview, connected: true, cancellationToken);
        if (details is null)
        {
            return null;
        }

        return FormatConnectionFailure(details);
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

    private async Task<bool> IsPreviewReady(
        AppConnection connection,
        PreviewProcess previewProcess,
        CancellationToken cancellationToken)
    {
        try
        {
            var snapshot = await connection.SendAsync(
                ProtocolMethods.GetSnapshot,
                null,
                ReadinessProbeTimeout,
                cancellationToken);

            return SnapshotHasWindow(snapshot);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new TimeoutException("Timed out waiting for get_snapshot during preview readiness probing.");
        }
        catch
        {
            await ThrowIfExited(
                previewProcess,
                "Preview host exited before the preview window became ready.",
                connected: true,
                cancellationToken);
            throw;
        }
    }

    private static bool SnapshotHasWindow(System.Text.Json.JsonElement? snapshot)
    {
        if (snapshot is null)
        {
            return false;
        }

        return !snapshot.Value.TryGetProperty("error", out _);
    }

    private static async Task ThrowIfExited(
        PreviewProcess previewProcess,
        string message,
        bool connected,
        CancellationToken cancellationToken)
    {
        var details = await TryGetExitDetails(previewProcess, connected, cancellationToken);
        if (details is null)
        {
            return;
        }

        throw new PreviewValidationException(
            DiagnosticErrorCodes.PreviewHostExited,
            $"{message} Exit code: {details.ExitCode}.",
            GetPreviewHostExitSuggestion(details),
            details);
    }

    private static string GetPreviewHostExitSuggestion(PreviewHostExitDetails details)
    {
        var output = details.StandardError + "\n" + details.StandardOutput;
        if (output.Contains("Could not load file or assembly", StringComparison.OrdinalIgnoreCase) ||
            output.Contains("System.IO.FileNotFoundException", StringComparison.OrdinalIgnoreCase))
        {
            return MissingAssemblyPreviewHostExitSuggestion;
        }

        return GenericPreviewHostExitSuggestion;
    }

    private static async Task<PreviewHostExitDetails?> TryGetExitDetails(
        PreviewProcess previewProcess,
        bool connected,
        CancellationToken cancellationToken)
    {
        if (!previewProcess.Process.HasExited)
        {
            try
            {
                await Task.Delay(ExitDrainTimeout, cancellationToken);
            }
            catch (OperationCanceledException)
            {
            }

            if (!previewProcess.Process.HasExited)
            {
                return null;
            }
        }

        await previewProcess.Output.WaitForDrain(ExitDrainTimeout, cancellationToken);
        var output = previewProcess.Output.Snapshot();
        return new PreviewHostExitDetails(
            previewProcess.Process.ExitCode,
            output.StandardOutput,
            output.StandardError,
            previewProcess.HostProjectPath,
            connected);
    }

    private static string FormatConnectionFailure(PreviewHostExitDetails details)
    {
        var builder = new StringBuilder()
            .Append("Preview host process exited. Exit code: ")
            .Append(details.ExitCode)
            .Append('.');

        if (!string.IsNullOrWhiteSpace(details.PreviewHostProjectPath))
        {
            builder.AppendLine()
                .Append("Preview host project: ")
                .Append(details.PreviewHostProjectPath);
        }

        if (!string.IsNullOrWhiteSpace(details.StandardError))
        {
            builder.AppendLine()
                .Append("stderr:")
                .AppendLine()
                .Append(details.StandardError.Trim());
        }

        if (!string.IsNullOrWhiteSpace(details.StandardOutput))
        {
            builder.AppendLine()
                .Append("stdout:")
                .AppendLine()
                .Append(details.StandardOutput.Trim());
        }

        return builder.ToString();
    }
}

internal sealed record PreviewProcess(
    int Pid,
    Process Process,
    PreviewTarget Target,
    PreviewProcessOutput Output,
    string HostProjectPath);

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
