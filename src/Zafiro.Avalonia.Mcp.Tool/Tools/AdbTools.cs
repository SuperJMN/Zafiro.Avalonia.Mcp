using System.ComponentModel;
using ModelContextProtocol.Server;
using Zafiro.Avalonia.Mcp.Protocol;
using Zafiro.Avalonia.Mcp.Protocol.Models;
using Zafiro.Avalonia.Mcp.Tool.Connection;

namespace Zafiro.Avalonia.Mcp.Tool.Tools;

[McpServerToolType]
public sealed class AdbTools
{
    [McpServerTool(Name = "connect_adb"), Description("""
        Connect to an Avalonia.Android app whose diagnostic TCP endpoint has been forwarded to the
        local PC via `adb forward tcp:<hostPort> tcp:<devicePort>`. Bypasses local discovery — use this
        when the app runs on an Android device/emulator instead of the host machine.

        Prerequisites:
        1. App calls `UseMcpDiagnostics()` (transport auto-resolves to TCP on Android).
        2. Read the device-side endpoint with `adb shell cat /sdcard/Android/data/<pkg>/cache/zafiro-avalonia-mcp/<pid>.json`.
        3. `adb forward tcp:<hostPort> tcp:<devicePortFromJson>`.
        4. Call this tool with `port=<hostPort>`.

        Returns: confirmation string with PID and ping result.
        Example: "Connected to com.example.app via tcp:127.0.0.1:9999. Ping: ..."
        """)]
    public static async Task<string> ConnectAdb(
        ConnectionPool pool,
        [Description("Local TCP port that adb forward maps to the device-side diagnostic port (e.g. 9999).")] int port,
        [Description("Hostname for the TCP connection. Defaults to 127.0.0.1; only change for non-loopback adb setups.")] string host = "127.0.0.1",
        [Description("Optional label that identifies the device/app; used as the synthetic process name. Defaults to 'android-<port>'.")] string? label = null,
        [Description("Optional synthetic PID. Defaults to a stable hash of host:port. Use to keep multiple devices distinguishable.")] int pid = 0)
    {
        if (port <= 0 || port > 65535)
            return $"Invalid port {port}. Must be 1..65535.";

        var resolvedPid = pid != 0 ? pid : SyntheticPid(host, port);
        var resolvedLabel = string.IsNullOrWhiteSpace(label) ? $"android-{port}" : label;

        var info = new DiscoveryInfo
        {
            Pid = resolvedPid,
            PipeName = $"adb-{host}-{port}",
            ProcessName = resolvedLabel,
            StartTime = DateTimeOffset.UtcNow,
            Transport = "tcp",
            Endpoint = $"tcp:{host}:{port}"
        };

        try
        {
            var conn = await pool.ConnectExternal(info);
            var result = await conn.SendAsync(ProtocolMethods.Ping);
            return $"Connected to {resolvedLabel} via tcp:{host}:{port} (PID {resolvedPid}). Ping: {result}";
        }
        catch (Exception ex)
        {
            return $"Error connecting to tcp:{host}:{port}: {ex.Message}\n" +
                   "Check that 'adb forward tcp:" + port + " tcp:<devicePort>' is active and the app is running with UseMcpDiagnostics().";
        }
    }

    private static int SyntheticPid(string host, int port)
    {
        // Stable, positive, non-zero pseudo-PID per host:port so the ConnectionPool can dedupe.
        var hash = HashCode.Combine(host, port);
        var positive = hash & int.MaxValue;
        return positive == 0 ? 1 : positive;
    }
}
