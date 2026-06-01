using System.ComponentModel;
using System.Text.Json;
using ModelContextProtocol.Server;
using Zafiro.Avalonia.Mcp.Protocol.Messages;
using Zafiro.Avalonia.Mcp.Tool.Connection;
using Zafiro.Avalonia.Mcp.Tool.Preview;

namespace Zafiro.Avalonia.Mcp.Tool.Tools;

[McpServerToolType]
public sealed class PreviewTools
{
    [McpServerTool(Name = "preview_axaml"), Description("""
        Launch a single AXAML file in an isolated preview window, connect MCP to that preview process, and make normal tools (get_snapshot, screenshot, get_datacontext, selectors) operate on the preview instead of the real app.
        Required: axamlPath plus exactly one of projectPath or assemblyPath. In multi-project apps, projectPath should usually be the executable Desktop host project, not the shared UI class library; assemblyPath should point at the built executable host assembly output. Returns: {pid,title,axamlPath,connected}.
        Example: {"axamlPath":"src/MyApp/Views/EditView.axaml","projectPath":"src/MyApp.Desktop/MyApp.Desktop.csproj","width":390,"height":844}
        """)]
    public static async Task<string> PreviewAxaml(
        ConnectionPool pool,
        PreviewProcessManager previews,
        PreviewTargetResolver resolver,
        [Description("Path to the AXAML document to load.")] string axamlPath,
        [Description("Path to the target Avalonia project. For multi-project apps, use the executable Desktop host project, not the shared UI class library. Required unless assemblyPath is set.")] string? projectPath = null,
        [Description("Path to an already-built target app assembly. For multi-project apps, use the executable host assembly output. Required unless projectPath is set.")] string? assemblyPath = null,
        [Description("Optional full or short type name for Program.BuildAvaloniaApp or an Application subclass.")] string? entryType = null,
        [Description("Optional target framework to build/evaluate, e.g. net10.0.")] string? targetFramework = null,
        [Description("Build configuration.")] string configuration = "Debug",
        [Description("Preview window width.")] int width = 1024,
        [Description("Preview window height.")] int height = 768,
        [Description("When projectPath is used, build before launching.")] bool build = true,
        CancellationToken cancellationToken = default)
    {
        PreviewProcess? preview = null;
        try
        {
            var target = await resolver.Resolve(new PreviewAxamlRequest(
                axamlPath,
                projectPath,
                assemblyPath,
                entryType,
                targetFramework,
                configuration,
                width,
                height,
                build), cancellationToken);

            preview = await previews.Launch(target, Math.Max(1, width), Math.Max(1, height), cancellationToken);
            try
            {
                await previews.WaitForConnection(preview, pool, cancellationToken);
            }
            catch
            {
                previews.Close(preview.Pid, pool);
                preview = null;
                throw;
            }

            return JsonSerializer.Serialize(new
            {
                pid = preview.Pid,
                title = $"AXAML Preview - {Path.GetFileName(target.AxamlPath)}",
                axamlPath = target.AxamlPath,
                connected = true,
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

    [McpServerTool(Name = "close_preview"), Description("""
        Close AXAML preview processes launched by preview_axaml and clear their MCP connection. Pass pid to close one preview; omit or pass 0 to close all previews owned by this MCP tool process.
        Returns: {closed:[pid]}.
        Example: {"pid":12345}
        """)]
    public static string ClosePreview(
        ConnectionPool pool,
        PreviewProcessManager previews,
        [Description("Preview process ID to close. Use 0 to close all previews launched by this tool process.")] int pid = 0)
    {
        var closed = previews.Close(pid, pool);
        return JsonSerializer.Serialize(new { closed });
    }
}

internal static class PreviewErrorSerializer
{
    public static string Serialize(string code, string message, string? suggested = null, object? details = null)
        => JsonSerializer.Serialize(new
        {
            error = new
            {
                code,
                message,
                suggested,
                details,
            },
        });
}
