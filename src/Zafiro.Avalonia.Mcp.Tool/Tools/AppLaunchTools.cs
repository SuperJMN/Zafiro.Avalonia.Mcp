using System.ComponentModel;
using System.Text.Json;
using ModelContextProtocol.Server;
using Zafiro.Avalonia.Mcp.Protocol.Messages;
using Zafiro.Avalonia.Mcp.Tool.Connection;
using Zafiro.Avalonia.Mcp.Tool.Launching;
using Zafiro.Avalonia.Mcp.Tool.Preview;

namespace Zafiro.Avalonia.Mcp.Tool.Tools;

[McpServerToolType]
public sealed class AppLaunchTools
{
    [McpServerTool(Name = "launch_app"), Description("""
        Launch a real Avalonia app from this MCP process, recover the local graphical desktop session when running over SSH, wait for UseMcpDiagnostics discovery, and connect to it automatically.
        Required: exactly one of projectPath or assemblyPath. The app must call UseMcpDiagnostics() during startup. Returns: {launchId,pid,connected,backend,display,assemblyPath}.
        Example: {"projectPath":"src/MyApp.Desktop/MyApp.Desktop.csproj","configuration":"Debug","build":true}
        """)]
    public static async Task<string> LaunchApp(
        ConnectionPool pool,
        ManagedAppProcessManager apps,
        [Description("Path to the target Avalonia project. Required unless assemblyPath is set.")] string? projectPath = null,
        [Description("Path to an already-built target app assembly. Required unless projectPath is set.")] string? assemblyPath = null,
        [Description("Build configuration.")] string configuration = "Debug",
        [Description("Optional target framework to build/evaluate, e.g. net10.0.")] string? targetFramework = null,
        [Description("When projectPath is used, build before launching.")] bool build = true,
        [Description("Optional arguments passed to the launched app, one item per process argument.")] string[]? appArgs = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await apps.Launch(
                new ManagedAppLaunchRequest(
                    projectPath,
                    assemblyPath,
                    configuration,
                    targetFramework,
                    build,
                    appArgs),
                pool,
                cancellationToken);

            return JsonSerializer.Serialize(new
            {
                launchId = result.LaunchId,
                pid = result.Pid,
                connected = result.Connected,
                backend = result.Backend,
                display = result.Display,
                assemblyPath = result.AssemblyPath,
            });
        }
        catch (PreviewValidationException ex)
        {
            return PreviewErrorSerializer.Serialize(ex.Code, ex.Message, ex.Suggested, ex.Details);
        }
        catch (Exception ex)
        {
            return PreviewErrorSerializer.Serialize(DiagnosticErrorCodes.Internal, ex.Message);
        }
    }

    [McpServerTool(Name = "close_app"), Description("""
        Close Avalonia app processes launched by launch_app and clear their MCP connection. Pass launchId to close one app; omit or pass 0 to close all apps owned by this MCP tool process.
        Returns: {closed:[launchId],pids:[pid]}.
        Example: {"launchId":1}
        """)]
    public static string CloseApp(
        ConnectionPool pool,
        ManagedAppProcessManager apps,
        [Description("Launch ID returned by launch_app. Use 0 to close all apps launched by this tool process.")] int launchId = 0)
    {
        var result = apps.Close(launchId, pool);
        return JsonSerializer.Serialize(new
        {
            closed = result.Closed,
            pids = result.Pids,
        });
    }
}
