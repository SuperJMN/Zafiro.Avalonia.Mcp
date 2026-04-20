using System.ComponentModel;
using Zafiro.Avalonia.Mcp.Protocol;
using Zafiro.Avalonia.Mcp.Tool.Connection;
using ModelContextProtocol.Server;

namespace Zafiro.Avalonia.Mcp.Tool.Tools;

[McpServerToolType]
public sealed class ConnectionTools
{
    [McpServerTool(Name = "list_apps"), Description("""
        List Avalonia apps that have UseMcpDiagnostics() enabled. AUTO-CONNECTS if exactly one app is found (saving a connect_to_app round-trip). Call this FIRST before any inspection/interaction tool.
        Returns: array of {pid, processName, title, connected}.
        Example: [{"pid":12345,"processName":"MyApp","title":"Main Window","connected":true}]
        """)]
    public static async Task<string> ListApps(ConnectionPool pool)
    {
        var apps = pool.DiscoverApps();
        if (apps.Count == 0)
            return "No Avalonia apps found. Make sure your app calls .UseMcpDiagnostics() and is running.";

        var lines = apps.Select(a => $"PID {a.Pid}: {a.ProcessName} (started: {a.StartTime:u})");
        var result = string.Join("\n", lines);

        if (apps.Count == 1)
        {
            try
            {
                var conn = await pool.Connect(apps[0].Pid);
                await conn.SendAsync(ProtocolMethods.Ping);
                result += $"\n\nAuto-connected to {apps[0].ProcessName} (PID {apps[0].Pid}).";
            }
            catch (Exception ex)
            {
                result += $"\n\nFound app but could not connect: {ex.Message}\nUse connect_to_app to retry.";
            }
        }
        else
        {
            result += "\n\nMultiple apps found. Use connect_to_app with the desired PID.";
        }

        return result;
    }

    [McpServerTool(Name = "connect_to_app"), Description("""
        Connect explicitly to an Avalonia app by PID. Use pid=0 for the first available app. Required only when list_apps did not auto-connect (i.e. multiple apps discovered).
        Returns: {pid, processName, title}.
        Example: {"pid":12345,"processName":"MyApp","title":"Main Window"}
        """)]
    public static async Task<string> ConnectToApp(
        ConnectionPool pool,
        [Description("Process ID of the app. Use 0 to connect to the first available app.")] int pid = 0)
    {
        try
        {
            AppConnection conn;
            if (pid == 0)
                conn = await pool.ConnectFirst();
            else
                conn = await pool.Connect(pid);

            var result = await conn.SendAsync(ProtocolMethods.Ping);
            return $"Connected to {conn.ProcessName} (PID {conn.Pid}). Ping: {result}";
        }
        catch (Exception ex)
        {
            return $"Error connecting: {ex.Message}";
        }
    }
}
