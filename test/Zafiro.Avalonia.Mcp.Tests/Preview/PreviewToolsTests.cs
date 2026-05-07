using System.Text.Json;
using Xunit;
using Zafiro.Avalonia.Mcp.Tool.Connection;
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
