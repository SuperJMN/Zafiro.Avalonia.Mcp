using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;
using Zafiro.Avalonia.Mcp.Protocol;
using Zafiro.Avalonia.Mcp.Protocol.Messages;
using Zafiro.Avalonia.Mcp.Tool.Connection;
using Zafiro.Avalonia.Mcp.Tool.Preview;

namespace Zafiro.Avalonia.Mcp.Tool.Launching;

public sealed class ManagedAppProcessManager : IDisposable
{
    private static readonly TimeSpan ExitDrainTimeout = TimeSpan.FromMilliseconds(500);
    private readonly IProcessRunner processRunner;
    private readonly TimeSpan discoveryTimeout;
    private readonly TimeSpan pollInterval;
    private readonly IGraphicalEnvironmentProvider graphicalEnvironment;
    private readonly ConcurrentDictionary<int, ManagedAppProcess> processes = new();
    private int nextLaunchId;

    public ManagedAppProcessManager(IProcessRunner processRunner)
        : this(
            processRunner,
            TimeSpan.FromSeconds(30),
            TimeSpan.FromMilliseconds(150),
            DefaultGraphicalEnvironmentProvider.Instance)
    {
    }

    internal ManagedAppProcessManager(
        IProcessRunner processRunner,
        TimeSpan discoveryTimeout,
        TimeSpan pollInterval,
        IGraphicalEnvironmentProvider graphicalEnvironment)
    {
        this.processRunner = processRunner;
        this.discoveryTimeout = discoveryTimeout;
        this.pollInterval = pollInterval;
        this.graphicalEnvironment = graphicalEnvironment;
    }

    internal async Task<ManagedAppLaunchResult> Launch(
        ManagedAppLaunchRequest request,
        ConnectionPool pool,
        CancellationToken cancellationToken)
    {
        var target = await ResolveTarget(request, cancellationToken);
        var startInfo = CreateStartInfo(target, request.AppArgs);
        graphicalEnvironment.Apply(startInfo.Environment);
        graphicalEnvironment.EnsureAvailable(startInfo.Environment);
        startInfo.Environment["ZAFIRO_AVALONIA_MCP_LAUNCH"] = "1";

        var process = new Process
        {
            StartInfo = startInfo,
            EnableRaisingEvents = true,
        };

        if (!process.Start())
        {
            throw new PreviewValidationException(DiagnosticErrorCodes.AppLaunchFailed, "Failed to start the Avalonia app process.");
        }

        var output = PreviewProcessOutput.Capture(process.StandardOutput, process.StandardError);
        var launchId = Interlocked.Increment(ref nextLaunchId);
        var managed = new ManagedAppProcess(launchId, process, target, output);
        processes[launchId] = managed;

        try
        {
            await WaitForConnection(managed, pool, cancellationToken);
            return new ManagedAppLaunchResult(
                launchId,
                process.Id,
                Connected: true,
                Backend: "local-gui",
                Display: ReadDisplay(startInfo.Environment),
                target.AssemblyPath);
        }
        catch
        {
            Close(launchId, pool);
            throw;
        }
    }

    internal ManagedAppCloseResult Close(int launchId, ConnectionPool pool)
    {
        var targets = launchId == 0
            ? processes.Keys.ToArray()
            : [launchId];

        var closed = new List<int>();
        var pids = new List<int>();

        foreach (var target in targets)
        {
            if (!processes.TryRemove(target, out var app))
            {
                continue;
            }

            var pid = app.Process.Id;
            pool.Disconnect(pid);
            DeleteDiscoveryFile(pool, pid);
            CloseProcess(app.Process);
            closed.Add(target);
            pids.Add(pid);
        }

        return new ManagedAppCloseResult(closed, pids);
    }

    public void Dispose()
    {
        foreach (var launchId in processes.Keys.ToArray())
        {
            if (processes.TryRemove(launchId, out var app))
            {
                CloseProcess(app.Process);
            }
        }
    }

    private async Task WaitForConnection(
        ManagedAppProcess app,
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
                    app.Process.Id,
                    cancellationToken => GetConnectionFailureDetails(app, connected: true, cancellationToken));

                await ThrowIfExited(app, connected: false, cancellationToken);

                var discovered = pool.DiscoverApps().FirstOrDefault(x => x.Pid == app.Process.Id);
                if (discovered is not null)
                {
                    try
                    {
                        var connection = await pool.Connect(app.Process.Id);
                        await connection.SendAsync(ProtocolMethods.Ping, null, timeout.Token);
                        return;
                    }
                    catch (OperationCanceledException)
                    {
                        throw;
                    }
                    catch
                    {
                        await ThrowIfExited(app, connected: true, cancellationToken);
                        pool.Disconnect(app.Process.Id);
                    }
                }

                await Task.Delay(pollInterval, timeout.Token);
            }
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested && timeout.IsCancellationRequested)
        {
            throw new PreviewValidationException(
                DiagnosticErrorCodes.Timeout,
                "Timed out waiting for the launched Avalonia app to publish MCP discovery and answer ping.",
                "Confirm the app calls UseMcpDiagnostics() during startup and can start in the local graphical session.");
        }
    }

    private static async Task ThrowIfExited(
        ManagedAppProcess app,
        bool connected,
        CancellationToken cancellationToken)
    {
        var details = await TryGetExitDetails(app, connected, cancellationToken);
        if (details is null)
        {
            return;
        }

        throw new PreviewValidationException(
            DiagnosticErrorCodes.AppLaunchFailed,
            $"Launched Avalonia app exited before MCP was ready. Exit code: {details.ExitCode}.",
            "Inspect standardError/standardOutput. Confirm the app can start from this machine's local graphical session and calls UseMcpDiagnostics().",
            details);
    }

    private static async Task<string?> GetConnectionFailureDetails(
        ManagedAppProcess app,
        bool connected,
        CancellationToken cancellationToken)
    {
        var details = await TryGetExitDetails(app, connected, cancellationToken);
        if (details is null)
        {
            return null;
        }

        return $"Launched app exited. Exit code: {details.ExitCode}.\nStandard error:\n{details.StandardError}\nStandard output:\n{details.StandardOutput}";
    }

    private static async Task<ManagedAppExitDetails?> TryGetExitDetails(
        ManagedAppProcess app,
        bool connected,
        CancellationToken cancellationToken)
    {
        if (!app.Process.HasExited)
        {
            try
            {
                await Task.Delay(ExitDrainTimeout, cancellationToken);
            }
            catch (OperationCanceledException)
            {
            }

            if (!app.Process.HasExited)
            {
                return null;
            }
        }

        await app.Output.WaitForDrain(ExitDrainTimeout, cancellationToken);
        var output = app.Output.Snapshot();
        return new ManagedAppExitDetails(
            app.LaunchId,
            app.Process.Id,
            app.Process.ExitCode,
            output.StandardOutput,
            output.StandardError,
            app.Target.AssemblyPath,
            connected);
    }

    private async Task<ManagedAppTarget> ResolveTarget(
        ManagedAppLaunchRequest request,
        CancellationToken cancellationToken)
    {
        var hasProject = !string.IsNullOrWhiteSpace(request.ProjectPath);
        var hasAssembly = !string.IsNullOrWhiteSpace(request.AssemblyPath);

        if (hasProject == hasAssembly)
        {
            throw new PreviewValidationException(
                DiagnosticErrorCodes.InvalidParam,
                hasProject
                    ? "Pass either projectPath or assemblyPath, not both."
                    : "Either projectPath or assemblyPath is required.");
        }

        var configuration = string.IsNullOrWhiteSpace(request.Configuration)
            ? "Debug"
            : request.Configuration.Trim();

        if (hasAssembly)
        {
            var assemblyPath = ResolveExistingFile(request.AssemblyPath!, "Assembly file does not exist");
            return new ManagedAppTarget(assemblyPath, null, configuration, request.TargetFramework);
        }

        var projectPath = ResolveExistingFile(request.ProjectPath!, "Project file does not exist");
        var targetFramework = string.IsNullOrWhiteSpace(request.TargetFramework)
            ? await ResolveDefaultTargetFramework(projectPath, configuration, cancellationToken)
            : request.TargetFramework.Trim();

        if (request.Build)
        {
            await BuildProject(projectPath, configuration, targetFramework, cancellationToken);
        }

        var targetPath = await EvaluateTargetPath(projectPath, configuration, targetFramework, cancellationToken);
        if (!Path.IsPathFullyQualified(targetPath))
        {
            targetPath = Path.GetFullPath(targetPath, Path.GetDirectoryName(projectPath)!);
        }

        if (!File.Exists(targetPath))
        {
            throw new PreviewValidationException(
                DiagnosticErrorCodes.InvalidParam,
                $"Target assembly was not found at evaluated TargetPath '{targetPath}'. Build the project or pass build=true.");
        }

        return new ManagedAppTarget(targetPath, projectPath, configuration, targetFramework);
    }

    private async Task<string?> ResolveDefaultTargetFramework(
        string projectPath,
        string configuration,
        CancellationToken cancellationToken)
    {
        var result = await RunDotnet([
            "msbuild",
            projectPath,
            "-nologo",
            "-getProperty:TargetFrameworks",
            "-getProperty:TargetFramework",
            $"-p:Configuration={configuration}",
        ], Path.GetDirectoryName(projectPath), cancellationToken);

        var properties = ParseProperties(result.StandardOutput);
        var targetFramework = properties.GetValueOrDefault("TargetFramework");
        var targetFrameworks = properties.GetValueOrDefault("TargetFrameworks");

        if (!string.IsNullOrWhiteSpace(targetFramework))
        {
            return targetFramework.Trim();
        }

        return targetFrameworks?
            .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .FirstOrDefault();
    }

    private async Task BuildProject(
        string projectPath,
        string configuration,
        string? targetFramework,
        CancellationToken cancellationToken)
    {
        var arguments = new List<string>
        {
            "build",
            projectPath,
            "-c",
            configuration,
            "-v",
            "minimal",
        };

        if (!string.IsNullOrWhiteSpace(targetFramework))
        {
            arguments.Add("-f");
            arguments.Add(targetFramework);
        }

        var result = await RunDotnet(arguments, Path.GetDirectoryName(projectPath), cancellationToken, DiagnosticErrorCodes.BuildFailed);
        if (result.ExitCode != 0)
        {
            throw new PreviewValidationException(DiagnosticErrorCodes.BuildFailed, CleanError(result.StandardError, result.StandardOutput));
        }
    }

    private async Task<string> EvaluateTargetPath(
        string projectPath,
        string configuration,
        string? targetFramework,
        CancellationToken cancellationToken)
    {
        var arguments = new List<string>
        {
            "msbuild",
            projectPath,
            "-nologo",
            "-getProperty:TargetPath",
            $"-p:Configuration={configuration}",
        };

        if (!string.IsNullOrWhiteSpace(targetFramework))
        {
            arguments.Add($"-p:TargetFramework={targetFramework}");
        }

        var result = await RunDotnet(arguments, Path.GetDirectoryName(projectPath), cancellationToken);
        var targetPath = ParseSingleProperty(result.StandardOutput, "TargetPath");

        if (string.IsNullOrWhiteSpace(targetPath))
        {
            throw new PreviewValidationException(DiagnosticErrorCodes.InvalidParam, "MSBuild did not return a TargetPath for the project.");
        }

        return targetPath.Trim();
    }

    private async Task<ProcessRunResult> RunDotnet(
        IReadOnlyList<string> arguments,
        string? workingDirectory,
        CancellationToken cancellationToken,
        string errorCode = DiagnosticErrorCodes.InvalidParam)
    {
        var result = await processRunner.Run("dotnet", arguments, workingDirectory, cancellationToken);
        if (result.ExitCode != 0)
        {
            throw new PreviewValidationException(errorCode, CleanError(result.StandardError, result.StandardOutput));
        }

        return result;
    }

    private static ProcessStartInfo CreateStartInfo(ManagedAppTarget target, IReadOnlyList<string>? appArgs)
    {
        var startInfo = new ProcessStartInfo("dotnet")
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            WorkingDirectory = Path.GetDirectoryName(target.AssemblyPath) ?? Environment.CurrentDirectory,
        };

        startInfo.ArgumentList.Add(target.AssemblyPath);

        if (appArgs is not null)
        {
            foreach (var argument in appArgs.Where(argument => argument is not null))
            {
                startInfo.ArgumentList.Add(argument);
            }
        }

        return startInfo;
    }

    private static string? ReadDisplay(IDictionary<string, string?> environment)
    {
        if (environment.TryGetValue("WAYLAND_DISPLAY", out var wayland) && !string.IsNullOrWhiteSpace(wayland))
        {
            return wayland;
        }

        return environment.TryGetValue("DISPLAY", out var display) && !string.IsNullOrWhiteSpace(display)
            ? display
            : null;
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

    private static string ResolveExistingFile(string path, string message)
    {
        var fullPath = Path.GetFullPath(path);
        if (!File.Exists(fullPath))
        {
            throw new PreviewValidationException(DiagnosticErrorCodes.InvalidParam, $"{message}: '{fullPath}'.");
        }

        return fullPath;
    }

    private static Dictionary<string, string?> ParseProperties(string output)
    {
        var trimmed = output.Trim();
        if (trimmed.StartsWith('{'))
        {
            using var document = JsonDocument.Parse(trimmed);
            if (document.RootElement.TryGetProperty("Properties", out var properties))
            {
                return properties.EnumerateObject()
                    .ToDictionary(
                        property => property.Name,
                        property => property.Value.ValueKind == JsonValueKind.String
                            ? property.Value.GetString()
                            : property.Value.ToString(),
                        StringComparer.Ordinal);
            }
        }

        return [];
    }

    private static string ParseSingleProperty(string output, string propertyName)
    {
        var properties = ParseProperties(output);
        return properties.TryGetValue(propertyName, out var value)
            ? value ?? string.Empty
            : output.Trim();
    }

    private static string CleanError(string standardError, string standardOutput)
    {
        var message = string.IsNullOrWhiteSpace(standardError) ? standardOutput : standardError;
        return string.IsNullOrWhiteSpace(message) ? "Command failed." : message.Trim();
    }
}

internal interface IGraphicalEnvironmentProvider
{
    void Apply(IDictionary<string, string?> environment);
    void EnsureAvailable(IDictionary<string, string?> environment);
}

internal sealed class DefaultGraphicalEnvironmentProvider : IGraphicalEnvironmentProvider
{
    public static readonly DefaultGraphicalEnvironmentProvider Instance = new();

    public void Apply(IDictionary<string, string?> environment) => PreviewGraphicalEnvironment.Apply(environment);

    public void EnsureAvailable(IDictionary<string, string?> environment) => PreviewGraphicalEnvironment.EnsureAvailable(environment);
}

internal sealed record ManagedAppLaunchRequest(
    string? ProjectPath,
    string? AssemblyPath,
    string Configuration,
    string? TargetFramework,
    bool Build,
    IReadOnlyList<string>? AppArgs);

internal sealed record ManagedAppTarget(
    string AssemblyPath,
    string? ProjectPath,
    string Configuration,
    string? TargetFramework);

internal sealed record ManagedAppLaunchResult(
    int LaunchId,
    int Pid,
    bool Connected,
    string Backend,
    string? Display,
    string AssemblyPath);

internal sealed record ManagedAppCloseResult(IReadOnlyList<int> Closed, IReadOnlyList<int> Pids);

internal sealed record ManagedAppProcess(
    int LaunchId,
    Process Process,
    ManagedAppTarget Target,
    PreviewProcessOutput Output);

internal sealed record ManagedAppExitDetails(
    [property: JsonPropertyName("launchId")] int LaunchId,
    [property: JsonPropertyName("pid")] int Pid,
    [property: JsonPropertyName("exitCode")] int ExitCode,
    [property: JsonPropertyName("standardOutput")] string StandardOutput,
    [property: JsonPropertyName("standardError")] string StandardError,
    [property: JsonPropertyName("assemblyPath")] string AssemblyPath,
    [property: JsonPropertyName("connected")] bool Connected);
