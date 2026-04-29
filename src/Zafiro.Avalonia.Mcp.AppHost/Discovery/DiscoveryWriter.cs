using System.Diagnostics;
using Zafiro.Avalonia.Mcp.AppHost.Transport;
using Zafiro.Avalonia.Mcp.Protocol;
using Zafiro.Avalonia.Mcp.Protocol.Models;

namespace Zafiro.Avalonia.Mcp.AppHost.Discovery;

public static class DiscoveryWriter
{
    private static string? _discoveryFilePath;

    public static string? LastWrittenPath => _discoveryFilePath;

    public static string PipeName => $"zafiro-avalonia-mcp-{Environment.ProcessId}";

    /// <summary>
    /// Discovery JSON directory. On desktop, lives under <see cref="Path.GetTempPath"/>.
    /// On Android, lives under the package's external cache dir so it can be read via
    /// <c>adb shell cat</c> without root or <c>run-as</c>.
    /// </summary>
    public static string DiscoveryDirectory
    {
        get
        {
            var basePath = AndroidPaths.ExternalCacheDir ?? Path.GetTempPath();
            return Path.Combine(basePath, "zafiro-avalonia-mcp");
        }
    }

    public static void Write(IDiagnosticTransport transport)
    {
        var dir = DiscoveryDirectory;
        Directory.CreateDirectory(dir);

        var info = new DiscoveryInfo
        {
            Pid = Environment.ProcessId,
            PipeName = transport.PipeName ?? PipeName,
            ProcessName = Process.GetCurrentProcess().ProcessName,
            StartTime = DateTimeOffset.UtcNow,
            Transport = transport.Kind,
            Endpoint = transport.Endpoint,
            PackageId = AndroidPaths.PackageId
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
