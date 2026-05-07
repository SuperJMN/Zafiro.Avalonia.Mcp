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
        using var app = new TempDotnetConsoleApp("""
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
        process.StartInfo.ArgumentList.Add(app.AssemblyPath);

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

    private sealed class TempDotnetConsoleApp : IDisposable
    {
        private readonly string root;

        public TempDotnetConsoleApp(string source)
        {
            root = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "avalonia-mcp-preview-process-tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            var projectPath = System.IO.Path.Combine(root, "ExitWithOutput.csproj");
            var programPath = System.IO.Path.Combine(root, "Program.cs");
            File.WriteAllText(projectPath, """
                <Project Sdk="Microsoft.NET.Sdk">
                  <PropertyGroup>
                    <OutputType>Exe</OutputType>
                    <TargetFramework>net10.0</TargetFramework>
                    <Nullable>enable</Nullable>
                    <ImplicitUsings>enable</ImplicitUsings>
                  </PropertyGroup>
                </Project>
                """);
            File.WriteAllText(programPath, source);
            Build(projectPath);
            AssemblyPath = System.IO.Path.Combine(root, "bin", "Debug", "net10.0", "ExitWithOutput.dll");
        }

        public string AssemblyPath { get; }

        private static void Build(string projectPath)
        {
            using var process = new Process
            {
                StartInfo = new ProcessStartInfo("dotnet")
                {
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                },
            };
            process.StartInfo.ArgumentList.Add("build");
            process.StartInfo.ArgumentList.Add(projectPath);
            process.StartInfo.ArgumentList.Add("-v");
            process.StartInfo.ArgumentList.Add("quiet");

            if (!process.Start())
            {
                throw new InvalidOperationException("Failed to start test fixture build.");
            }

            if (!process.WaitForExit(TimeSpan.FromSeconds(30)))
            {
                process.Kill(entireProcessTree: true);
                throw new TimeoutException("Timed out building test fixture process.");
            }

            if (process.ExitCode != 0)
            {
                throw new InvalidOperationException(
                    $"Failed to build test fixture process. stdout: {process.StandardOutput.ReadToEnd()} stderr: {process.StandardError.ReadToEnd()}");
            }
        }

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
