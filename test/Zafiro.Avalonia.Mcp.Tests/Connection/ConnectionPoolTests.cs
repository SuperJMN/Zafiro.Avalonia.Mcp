using System.Diagnostics;
using System.IO.Pipes;
using Xunit;
using Zafiro.Avalonia.Mcp.Protocol;
using Zafiro.Avalonia.Mcp.Protocol.Models;
using Zafiro.Avalonia.Mcp.Tool.Connection;

namespace Zafiro.Avalonia.Mcp.Tests.Connection;

public sealed class ConnectionPoolTests
{
    [Fact]
    public void DiscoverApps_KeepsPipeDiscovery_OnWindows()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        using var process = Process.GetCurrentProcess();
        using var pool = new ConnectionPool();
        var pipeName = $"zafiro-avalonia-mcp-test-{Guid.NewGuid():N}";
        using var server = new NamedPipeServerStream(
            pipeName,
            PipeDirection.InOut,
            NamedPipeServerStream.MaxAllowedServerInstances,
            PipeTransmissionMode.Byte,
            PipeOptions.Asynchronous);
        var discoveryPath = Path.Combine(pool.DiscoveryDirectory, $"{process.Id}-{Guid.NewGuid():N}.json");
        Directory.CreateDirectory(pool.DiscoveryDirectory);

        var info = new DiscoveryInfo
        {
            Pid = process.Id,
            PipeName = pipeName,
            ProcessName = process.ProcessName,
            StartTime = DateTimeOffset.UtcNow,
            Transport = "pipe",
            Endpoint = pipeName
        };

        File.WriteAllText(discoveryPath, ProtocolSerializer.Serialize(info));

        try
        {
            var apps = pool.DiscoverApps();

            Assert.False(File.Exists(Path.Combine(Path.GetTempPath(), $"CoreFxPipe_{pipeName}")));
            Assert.Contains(apps, app => app.Pid == process.Id && app.PipeName == pipeName);
            Assert.True(File.Exists(discoveryPath));
        }
        finally
        {
            try
            {
                File.Delete(discoveryPath);
            }
            catch
            {
            }
        }
    }
}
