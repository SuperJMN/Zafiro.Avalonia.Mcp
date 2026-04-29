using System.Collections.Concurrent;
using System.Diagnostics;
using Zafiro.Avalonia.Mcp.Protocol;
using Zafiro.Avalonia.Mcp.Protocol.Models;

namespace Zafiro.Avalonia.Mcp.Tool.Connection;

public sealed class ConnectionPool : IDisposable
{
    private readonly ConcurrentDictionary<int, AppConnection> _connections = new();
    private volatile AppConnection? _activeConnection;

    public string DiscoveryDirectory
    {
        get
        {
            var tempPath = Path.GetTempPath();
            return Path.Combine(tempPath, "zafiro-avalonia-mcp");
        }
    }

    public IReadOnlyList<DiscoveryInfo> DiscoverApps()
    {
        var dir = DiscoveryDirectory;
        if (!Directory.Exists(dir)) return [];

        var apps = new List<DiscoveryInfo>();
        foreach (var file in Directory.GetFiles(dir, "*.json"))
        {
            try
            {
                var json = File.ReadAllText(file);
                var info = ProtocolSerializer.Deserialize<DiscoveryInfo>(json);
                if (info is not null && IsProcessRunning(info.Pid) && IsEndpointAvailable(info))
                    apps.Add(info);
                else if (info is not null)
                    try { File.Delete(file); } catch { }
            }
            catch { }
        }

        return apps;
    }

    private static bool IsEndpointAvailable(DiscoveryInfo info)
    {
        // TCP endpoints can't be probed cheaply without actually connecting; trust the process check.
        if (string.Equals(info.Transport, "tcp", StringComparison.OrdinalIgnoreCase))
            return true;

        var pipeName = info.PipeName;
        return !string.IsNullOrEmpty(pipeName) && IsPipeAvailable(pipeName);
    }

    private static bool IsPipeAvailable(string pipeName)
    {
        // On Linux, .NET named pipes are Unix domain sockets at /tmp/CoreFxPipe_<name>
        var socketPath = Path.Combine(Path.GetTempPath(), $"CoreFxPipe_{pipeName}");
        return File.Exists(socketPath);
    }

    private static bool IsProcessRunning(int pid)
    {
        try { Process.GetProcessById(pid); return true; }
        catch { return false; }
    }

    public async Task<AppConnection> Connect(int pid)
    {
        if (_connections.TryGetValue(pid, out var existing) && existing.IsConnected)
        {
            _activeConnection = existing;
            return existing;
        }

        var apps = DiscoverApps();
        var app = apps.FirstOrDefault(a => a.Pid == pid)
                  ?? throw new InvalidOperationException($"No app found with PID {pid}");

        var connection = new AppConnection(app);
        await connection.ConnectAsync();
        _connections[pid] = connection;
        _activeConnection = connection;
        return connection;
    }

    public async Task<AppConnection> ConnectFirst()
    {
        var apps = DiscoverApps();
        if (apps.Count == 0)
            throw new InvalidOperationException("No Avalonia apps with MCP diagnostics found. Make sure the app is running with .UseMcpDiagnostics().");
        
        return await Connect(apps[0].Pid);
    }

    /// <summary>
    /// Manually register and activate a connection from a hand-crafted <see cref="DiscoveryInfo"/>.
    /// Used by the <c>connect_adb</c> tool when the user has already wired <c>adb forward</c> and the
    /// app does not show up in the local discovery directory.
    /// </summary>
    public async Task<AppConnection> ConnectExternal(DiscoveryInfo info)
    {
        if (_connections.TryGetValue(info.Pid, out var existing) && existing.IsConnected)
        {
            _activeConnection = existing;
            return existing;
        }

        var connection = new AppConnection(info);
        await connection.ConnectAsync();
        _connections[info.Pid] = connection;
        _activeConnection = connection;
        return connection;
    }

    public AppConnection GetActive()
    {
        var conn = _activeConnection;
        if (conn is null)
            throw new InvalidOperationException(
                "No active connection. Use list_apps to find available apps and connect_to_app to connect.");
        if (!conn.IsConnected)
            throw new InvalidOperationException(
                "Connection lost. The app may have exited. Use list_apps and connect_to_app to reconnect.");
        return conn;
    }

    public void Dispose()
    {
        foreach (var conn in _connections.Values)
            conn.Dispose();
        _connections.Clear();
    }
}
