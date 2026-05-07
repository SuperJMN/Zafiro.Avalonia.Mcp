using System.Diagnostics;
using Xunit;
using Zafiro.Avalonia.Mcp.Tool.Connection;
using Zafiro.Avalonia.Mcp.Tool.Preview;

namespace Zafiro.Avalonia.Mcp.Tests.Preview;

public sealed class PreviewProcessManagerTests
{
    [Fact]
    public async Task WaitForConnection_ReportsTimeout_WhenDiscoveryExpires()
    {
        using var currentProcess = Process.GetCurrentProcess();
        var manager = new PreviewProcessManager(
            discoveryTimeout: TimeSpan.FromMilliseconds(10),
            pollInterval: TimeSpan.FromMilliseconds(1));
        var preview = new PreviewProcess(
            currentProcess.Id,
            currentProcess,
            new PreviewTarget(
                AxamlPath: "/tmp/Preview.axaml",
                AssemblyPath: "/tmp/Preview.dll",
                XamlAssemblyPath: "/tmp/Preview.dll",
                ProjectPath: null,
                EntryType: null,
                TargetFramework: null,
                Configuration: "Debug"));

        var ex = await Assert.ThrowsAsync<PreviewValidationException>(() =>
            manager.WaitForConnection(preview, new ConnectionPool(), CancellationToken.None));

        Assert.Equal("TIMEOUT", ex.Code);
    }
}
