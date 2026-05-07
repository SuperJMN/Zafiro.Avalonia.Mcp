using System.Diagnostics;
using Xunit;
using Zafiro.Avalonia.Mcp.Tool.Connection;
using Zafiro.Avalonia.Mcp.Tool.Preview;

namespace Zafiro.Avalonia.Mcp.Tests.Preview;

public sealed class PreviewProcessManagerTests
{
    [Fact]
    public void BoundedTextBuffer_KeepsMostRecentCharacters()
    {
        var buffer = new BoundedTextBuffer(maxCharacters: 10);

        buffer.Append("0123456789ABCDE".AsSpan());

        Assert.Equal("56789ABCDE", buffer.ToString());
    }

    [Fact]
    public async Task WaitForConnection_ReportsCapturedOutput_WhenPreviewHostExitsBeforeDiscovery()
    {
        using var script = new TempDotnetScript("""
            Console.WriteLine("preview-output");
            Console.Error.WriteLine("preview-error");
            return 37;
            """);
        using var process = new Process
        {
            StartInfo = new ProcessStartInfo("dotnet")
            {
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            },
            EnableRaisingEvents = true,
        };
        process.StartInfo.ArgumentList.Add("run");
        process.StartInfo.ArgumentList.Add(script.Path);

        Assert.True(process.Start());
        var output = PreviewProcessOutput.Capture(process.StandardOutput, process.StandardError);
        var preview = new PreviewProcess(
            process.Id,
            process,
            CreateTarget(),
            output);
        var manager = new PreviewProcessManager(
            discoveryTimeout: TimeSpan.FromSeconds(5),
            pollInterval: TimeSpan.FromMilliseconds(1));

        var ex = await Assert.ThrowsAsync<PreviewValidationException>(() =>
            manager.WaitForConnection(preview, new ConnectionPool(), CancellationToken.None));

        var details = Assert.IsType<PreviewHostExitDetails>(ex.Details);
        Assert.Equal(37, details.ExitCode);
        Assert.Contains("preview-output", details.StandardOutput);
        Assert.Contains("preview-error", details.StandardError);
    }

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
            CreateTarget(),
            PreviewProcessOutput.Empty);

        var ex = await Assert.ThrowsAsync<PreviewValidationException>(() =>
            manager.WaitForConnection(preview, new ConnectionPool(), CancellationToken.None));

        Assert.Equal("TIMEOUT", ex.Code);
    }

    private static PreviewTarget CreateTarget() =>
        new(
            AxamlPath: "/tmp/Preview.axaml",
            AssemblyPath: "/tmp/Preview.dll",
            XamlAssemblyPath: "/tmp/Preview.dll",
            ProjectPath: null,
            EntryType: null,
            TargetFramework: null,
            Configuration: "Debug");

    private sealed class TempDotnetScript : IDisposable
    {
        private readonly string root;

        public TempDotnetScript(string source)
        {
            root = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "avalonia-mcp-preview-process-tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            Path = System.IO.Path.Combine(root, "ExitWithOutput.cs");
            File.WriteAllText(Path, source);
        }

        public string Path { get; }

        public void Dispose()
        {
            try
            {
                Directory.Delete(root, recursive: true);
            }
            catch
            {
            }
        }
    }
}
