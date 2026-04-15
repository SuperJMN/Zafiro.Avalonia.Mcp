using System.Diagnostics;
using System.Text.Json;
using Zafiro.Avalonia.Mcp.Protocol;
using Zafiro.Avalonia.Mcp.Protocol.Models;

namespace Zafiro.Avalonia.Mcp.AppHost.Discovery;

public static class DiscoveryWriter
{
    private static string? _discoveryFilePath;

    public static string PipeName => $"zafiro-avalonia-mcp-{Environment.ProcessId}";

    public static string DiscoveryDirectory
    {
        get
        {
            var tempPath = Path.GetTempPath();
            return Path.Combine(tempPath, "zafiro-avalonia-mcp");
        }
    }

    public static void Write()
    {
        var dir = DiscoveryDirectory;
        Directory.CreateDirectory(dir);

        var info = new DiscoveryInfo
        {
            Pid = Environment.ProcessId,
            PipeName = PipeName,
            ProcessName = Process.GetCurrentProcess().ProcessName,
            StartTime = DateTimeOffset.UtcNow
        };

        _discoveryFilePath = Path.Combine(dir, $"{Environment.ProcessId}.json");
        File.WriteAllText(_discoveryFilePath, ProtocolSerializer.Serialize(info));
    }

    public static void Remove()
    {
        if (_discoveryFilePath is not null && File.Exists(_discoveryFilePath))
        {
            try { File.Delete(_discoveryFilePath); } catch { }
        }
    }
}
