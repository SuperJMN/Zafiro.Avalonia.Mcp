using System.Text.Json;
using Xunit;
using Zafiro.Avalonia.Mcp.Tool.Connection;
using Zafiro.Avalonia.Mcp.Tool.Launching;
using Zafiro.Avalonia.Mcp.Tool.Preview;
using Zafiro.Avalonia.Mcp.Tool.Tools;

namespace Zafiro.Avalonia.Mcp.Tests.Preview;

public sealed class PreviewToolsTests
{
    [Fact]
    public async Task PreviewAxaml_CleansLaunchedPreview_WhenConnectionFails()
    {
        var directory = Path.Combine(Path.GetTempPath(), "avalonia-mcp-preview-tool-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var axamlPath = Path.Combine(directory, "View.axaml");
        File.WriteAllText(axamlPath, """
            <UserControl xmlns="https://github.com/avaloniaui" />
            """);

        var pool = new ConnectionPool();
        var previews = new PreviewProcessManager(
            discoveryTimeout: TimeSpan.FromSeconds(5),
            pollInterval: TimeSpan.FromMilliseconds(10));

        try
        {
            var result = await PreviewTools.PreviewAxaml(
                pool,
                previews,
                new PreviewTargetResolver(new NoOpProcessRunner()),
                axamlPath,
                assemblyPath: typeof(PreviewToolsTests).Assembly.Location,
                entryType: "MissingEntryType");

            Assert.Contains("\"error\"", result);

            var closeResult = PreviewTools.ClosePreview(pool, previews);
            using var document = JsonDocument.Parse(closeResult);
            var closed = document.RootElement.GetProperty("closed");

            Assert.Equal(0, closed.GetArrayLength());
        }
        finally
        {
            previews.Dispose();
            pool.Dispose();
            try
            {
                Directory.Delete(directory, recursive: true);
            }
            catch
            {
            }
        }
    }

    [Fact]
    public void PreviewErrorSerializer_WritesMachineReadableDetails()
    {
        var result = PreviewErrorSerializer.Serialize(
            "INTERNAL",
            "Preview host exited.",
            details: new PreviewHostExitDetails(37, "stdout", "stderr"));

        using var document = JsonDocument.Parse(result);
        var error = document.RootElement.GetProperty("error");
        Assert.Equal("INTERNAL", error.GetProperty("code").GetString());
        var details = error.GetProperty("details");
        Assert.Equal(37, details.GetProperty("exitCode").GetInt32());
        Assert.Equal("stdout", details.GetProperty("standardOutput").GetString());
        Assert.Equal("stderr", details.GetProperty("standardError").GetString());
    }

    [Fact]
    public void PreviewErrorSerializer_WritesMachineReadableLaunchFailureDetails()
    {
        var result = PreviewErrorSerializer.Serialize(
            "APP_LAUNCH_FAILED",
            "Launch failed.",
            details: new ManagedAppExitDetails(
                LaunchId: 7,
                Pid: 123,
                ExitCode: 37,
                StandardOutput: "stdout",
                StandardError: "stderr",
                AssemblyPath: "/tmp/App.dll",
                Connected: false));

        using var document = JsonDocument.Parse(result);
        var details = document.RootElement.GetProperty("error").GetProperty("details");

        Assert.Equal(7, details.GetProperty("launchId").GetInt32());
        Assert.Equal(123, details.GetProperty("pid").GetInt32());
        Assert.Equal(37, details.GetProperty("exitCode").GetInt32());
        Assert.Equal("stdout", details.GetProperty("standardOutput").GetString());
        Assert.Equal("stderr", details.GetProperty("standardError").GetString());
        Assert.Equal("/tmp/App.dll", details.GetProperty("assemblyPath").GetString());
        Assert.False(details.GetProperty("connected").GetBoolean());
        Assert.False(details.TryGetProperty("StandardOutput", out _));
        Assert.False(details.TryGetProperty("StandardError", out _));
    }

    private sealed class NoOpProcessRunner : IProcessRunner
    {
        public Task<ProcessRunResult> Run(
            string fileName,
            IReadOnlyList<string> arguments,
            string? workingDirectory,
            CancellationToken cancellationToken)
            => Task.FromResult(ProcessRunResult.Success(string.Empty));
    }
}
