using System.Collections.Concurrent;
using System.Diagnostics;
using Zafiro.Avalonia.Mcp.Protocol;
using Zafiro.Avalonia.Mcp.Protocol.Models;

namespace Zafiro.Avalonia.Mcp.Server.Connection;

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
                if (info is not null && IsProcessRunning(info.Pid))
                    apps.Add(info);
                else if (info is not null)
                    try { File.Delete(file); } catch { }
            }
            catch { }
        }

        return apps;
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
