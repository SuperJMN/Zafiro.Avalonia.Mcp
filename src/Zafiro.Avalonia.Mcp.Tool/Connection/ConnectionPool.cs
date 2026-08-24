using System.Collections.Concurrent;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Zafiro.Avalonia.Mcp.Protocol;
using Zafiro.Avalonia.Mcp.Protocol.Models;

namespace Zafiro.Avalonia.Mcp.Tool.Connection;

public sealed class ConnectionPool : IDisposable
{
    private readonly ConcurrentDictionary<int, AppConnection> _connections = new();
    private readonly ConcurrentDictionary<int, Func<CancellationToken, Task<string?>>> _connectionFailureDetails = new();
    private readonly Dictionary<int, long> _connectionGenerations = new();
    private readonly object _stateLock = new();
    private readonly string? _discoveryDirectory;
    private volatile AppConnection? _activeConnection;
    private bool _disposed;

    public ConnectionPool()
    {
    }

    internal ConnectionPool(string discoveryDirectory)
    {
        _discoveryDirectory = discoveryDirectory;
    }

    public string DiscoveryDirectory
    {
        get
        {
            return _discoveryDirectory ?? Path.Combine(Path.GetTempPath(), "zafiro-avalonia-mcp");
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
        if (OperatingSystem.IsWindows())
            return IsWindowsPipeAvailable(pipeName);

        // On Unix, .NET named pipes are Unix domain sockets at /tmp/CoreFxPipe_<name>.
        var socketPath = Path.Combine(Path.GetTempPath(), $"CoreFxPipe_{pipeName}");
        return File.Exists(socketPath);
    }

    private static bool IsWindowsPipeAvailable(string pipeName)
    {
        // Windows named pipes live under \\.\pipe and are not filesystem entries.
        // WaitNamedPipe probes the pipe namespace without consuming a connection.
        return WaitNamedPipe($@"\\.\pipe\{pipeName}", 0);
    }

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool WaitNamedPipe(string namedPipeName, int timeout);

    private static bool IsProcessRunning(int pid)
    {
        try { Process.GetProcessById(pid); return true; }
        catch { return false; }
    }

    public async Task<AppConnection> Connect(int pid)
    {
        if (TryActivateReusableConnection(pid) is { } existing)
        {
            return existing;
        }

        var apps = DiscoverApps();
        var app = apps.FirstOrDefault(a => a.Pid == pid)
                  ?? throw new InvalidOperationException($"No app found with PID {pid}");

        return await OpenConnection(app, forceReplace: false);
    }

    public async Task<AppConnection> ConnectFirst()
    {
        var apps = DiscoverApps();
        if (apps.Count == 0)
            throw new InvalidOperationException("No Avalonia apps with MCP diagnostics found. Make sure the app is running with .UseMcpDiagnostics().");

        return await OpenConnection(apps[0], forceReplace: false);
    }

    public async Task<AppConnection> Reconnect(int pid)
    {
        InvalidateCachedConnectionBeforeDiscovery(pid);
        var apps = DiscoverApps();
        var app = apps.FirstOrDefault(a => a.Pid == pid)
                  ?? throw new InvalidOperationException($"No app found with PID {pid}");

        return await OpenConnection(app, forceReplace: true);
    }

    public async Task<AppConnection> ReconnectFirst()
    {
        InvalidateCachedConnectionBeforeDiscovery(pid: null);
        var apps = DiscoverApps();
        if (apps.Count == 0)
            throw new InvalidOperationException("No Avalonia apps with MCP diagnostics found. Make sure the app is running with .UseMcpDiagnostics().");

        return await OpenConnection(apps[0], forceReplace: true);
    }

    /// <summary>
    /// Manually register and activate a connection from a hand-crafted <see cref="DiscoveryInfo"/>.
    /// Used by the <c>connect_adb</c> tool when the user has already wired <c>adb forward</c> and the
    /// app does not show up in the local discovery directory.
    /// </summary>
    public async Task<AppConnection> ConnectExternal(DiscoveryInfo info)
    {
        return await OpenConnection(info, forceReplace: false);
    }

    public async Task<AppConnection> ReconnectExternal(DiscoveryInfo info)
    {
        return await OpenConnection(info, forceReplace: true);
    }

    private AppConnection? TryActivateReusableConnection(int pid)
    {
        AppConnection? stale = null;
        lock (_stateLock)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (!_connections.TryGetValue(pid, out var existing))
            {
                return null;
            }

            if (existing.IsConnected)
            {
                _activeConnection = existing;
                return existing;
            }

            if (_connections.TryRemove(pid, out stale) && ReferenceEquals(_activeConnection, stale))
            {
                _activeConnection = null;
            }
        }

        stale?.Dispose();
        return null;
    }

    private void InvalidateCachedConnectionBeforeDiscovery(int? pid)
    {
        AppConnection? connection = null;
        lock (_stateLock)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            var targetPid = pid ?? _activeConnection?.Pid;
            if (targetPid is null)
            {
                return;
            }

            _connectionGenerations[targetPid.Value] = GetConnectionGeneration(targetPid.Value) + 1;
            _connections.TryRemove(targetPid.Value, out connection);
            if (_activeConnection?.Pid == targetPid.Value)
            {
                _activeConnection = null;
            }
        }

        connection?.Dispose();
    }

    private async Task<AppConnection> OpenConnection(DiscoveryInfo app, bool forceReplace)
    {
        AppConnection? discarded = null;
        long generation;
        lock (_stateLock)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (!forceReplace &&
                _connections.TryGetValue(app.Pid, out var existing) &&
                existing.IsConnected)
            {
                _activeConnection = existing;
                return existing;
            }

            if (_connections.TryRemove(app.Pid, out discarded) &&
                ReferenceEquals(_activeConnection, discarded))
            {
                _activeConnection = null;
            }

            generation = GetConnectionGeneration(app.Pid) + 1;
            _connectionGenerations[app.Pid] = generation;
        }

        discarded?.Dispose();

        return await CreateConnection(app, generation);
    }

    private async Task<AppConnection> CreateConnection(DiscoveryInfo app, long generation)
    {
        var connection = new AppConnection(app, GetConnectionFailureDetailsProvider(app.Pid));
        try
        {
            await connection.ConnectAsync();
        }
        catch
        {
            connection.Dispose();
            throw;
        }

        var superseded = false;
        lock (_stateLock)
        {
            if (_disposed || GetConnectionGeneration(app.Pid) != generation)
            {
                superseded = true;
            }
            else
            {
                _connections[app.Pid] = connection;
                _activeConnection = connection;
            }
        }

        if (superseded)
        {
            connection.Dispose();
            throw new ObjectDisposedException(nameof(ConnectionPool), "The connection request was superseded.");
        }

        return connection;
    }

    private long GetConnectionGeneration(int pid) =>
        _connectionGenerations.TryGetValue(pid, out var generation) ? generation : 0;

    internal void Invalidate(AppConnection connection)
    {
        lock (_stateLock)
        {
            var entry = new KeyValuePair<int, AppConnection>(connection.Pid, connection);
            if (!((ICollection<KeyValuePair<int, AppConnection>>)_connections).Remove(entry))
            {
                return;
            }

            _connectionGenerations[connection.Pid] = GetConnectionGeneration(connection.Pid) + 1;
            if (ReferenceEquals(_activeConnection, connection))
            {
                _activeConnection = null;
            }
        }

        connection.Dispose();
    }

    internal void RegisterConnectionFailureDetails(int pid, Func<CancellationToken, Task<string?>> detailsProvider)
    {
        lock (_stateLock)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            _connectionFailureDetails[pid] = detailsProvider;
        }
    }

    private Func<CancellationToken, Task<string?>>? GetConnectionFailureDetailsProvider(int pid) =>
        _connectionFailureDetails.TryGetValue(pid, out var provider) ? provider : null;

    public AppConnection GetActive()
    {
        lock (_stateLock)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            var conn = _activeConnection;
            if (conn is null)
                throw new InvalidOperationException(
                    "No active connection. Use list_apps to find available apps and connect_to_app to connect.");
            if (!conn.IsConnected)
                throw new InvalidOperationException(
                    "Connection lost. The app may have exited. Use list_apps and connect_to_app to reconnect.");
            return conn;
        }
    }

    public void Disconnect(int pid)
    {
        AppConnection? connection;
        lock (_stateLock)
        {
            _connectionGenerations[pid] = GetConnectionGeneration(pid) + 1;
            _connections.TryRemove(pid, out connection);
            _connectionFailureDetails.TryRemove(pid, out _);
            if (connection is not null && ReferenceEquals(_activeConnection, connection))
            {
                _activeConnection = null;
            }
            else if (_activeConnection?.Pid == pid)
            {
                _activeConnection = null;
            }
        }

        connection?.Dispose();
    }

    public void Dispose()
    {
        AppConnection[] connections;
        lock (_stateLock)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            connections = _connections.Values.ToArray();
            _connections.Clear();
            _connectionFailureDetails.Clear();
            _activeConnection = null;
        }

        foreach (var conn in connections)
        {
            conn.Dispose();
        }
    }
}
