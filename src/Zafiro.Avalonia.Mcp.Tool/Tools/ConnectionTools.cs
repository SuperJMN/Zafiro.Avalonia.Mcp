using System.ComponentModel;
using Zafiro.Avalonia.Mcp.Protocol;
using Zafiro.Avalonia.Mcp.Tool.Connection;
using ModelContextProtocol.Server;

namespace Zafiro.Avalonia.Mcp.Tool.Tools;

[McpServerToolType]
public sealed class ConnectionTools
{
    [McpServerTool(Name = "list_apps"), Description("List available Avalonia apps with MCP diagnostics enabled. Auto-connects if exactly one app is found. If multiple apps are discovered, use connect_to_app with the desired PID. The app must call .UseMcpDiagnostics() in its AppBuilder to be discoverable.")]
    public static async Task<string> ListApps(ConnectionPool pool)
    {
        var apps = pool.DiscoverApps();
        if (apps.Count == 0)
            return "No Avalonia apps found. Make sure your app calls .UseMcpDiagnostics() and is running.";

        var lines = apps.Select(a => $"PID {a.Pid}: {a.ProcessName} (started: {a.StartTime:u})");
        var result = string.Join("\n", lines);

        if (apps.Count == 1)
        {
            var conn = await pool.Connect(apps[0].Pid);
            await conn.SendAsync(ProtocolMethods.Ping);
            result += $"\n\nAuto-connected to {apps[0].ProcessName} (PID {apps[0].Pid}).";
        }
        else
        {
            result += "\n\nMultiple apps found. Use connect_to_app with the desired PID.";
        }

        return result;
    }

    [McpServerTool(Name = "connect_to_app"), Description("Connect to a specific running Avalonia app by process ID. Use PID 0 to auto-connect to the first available app. Must be called before using any inspection or interaction tools (unless list_apps already auto-connected).")]
    public static async Task<string> ConnectToApp(
        ConnectionPool pool,
        [Description("Process ID of the app. Use 0 to connect to the first available app.")] int pid = 0)
    {
        AppConnection conn;
        if (pid == 0)
            conn = await pool.ConnectFirst();
        else
            conn = await pool.Connect(pid);
        
        var result = await conn.SendAsync(ProtocolMethods.Ping);
        return $"Connected to {conn.ProcessName} (PID {conn.Pid}). Ping: {result}";
    }
}
