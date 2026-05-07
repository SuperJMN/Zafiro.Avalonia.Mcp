using Xunit;
using Zafiro.Avalonia.Mcp.Tool.Preview;

namespace Zafiro.Avalonia.Mcp.Tests.Preview;

public sealed class PreviewTargetResolverTests
{
    [Fact]
    public async Task Resolve_RejectsMissingAxaml()
    {
        var resolver = new PreviewTargetResolver(new FakeProcessRunner());
        var projectPath = Path.Combine(Path.GetTempPath(), "PreviewApp.csproj");

        var ex = await Assert.ThrowsAsync<PreviewValidationException>(() =>
            resolver.Resolve(new PreviewAxamlRequest(
                AxamlPath: Path.Combine(Path.GetTempPath(), "Missing.axaml"),
                ProjectPath: projectPath), CancellationToken.None));

        Assert.Equal("INVALID_PARAM", ex.Code);
        Assert.Contains("AXAML file does not exist", ex.Message);
    }

    [Fact]
    public async Task Resolve_RejectsMissingProjectAndAssembly()
    {
        using var temp = new TempPreviewProject();
        var resolver = new PreviewTargetResolver(new FakeProcessRunner());

        var ex = await Assert.ThrowsAsync<PreviewValidationException>(() =>
            resolver.Resolve(new PreviewAxamlRequest(AxamlPath: temp.AxamlPath), CancellationToken.None));

        Assert.Equal("INVALID_PARAM", ex.Code);
        Assert.Contains("Either projectPath or assemblyPath", ex.Message);
    }

    [Fact]
    public async Task Resolve_RejectsProjectAndAssemblyTogether()
    {
        using var temp = new TempPreviewProject();
        var assemblyPath = Path.Combine(temp.Directory, "PreviewApp.dll");
        File.WriteAllText(assemblyPath, string.Empty);
        var resolver = new PreviewTargetResolver(new FakeProcessRunner());

        var ex = await Assert.ThrowsAsync<PreviewValidationException>(() =>
            resolver.Resolve(new PreviewAxamlRequest(
                AxamlPath: temp.AxamlPath,
                ProjectPath: temp.ProjectPath,
                AssemblyPath: assemblyPath), CancellationToken.None));

        Assert.Equal("INVALID_PARAM", ex.Code);
        Assert.Contains("not both", ex.Message);
    }

    [Fact]
    public async Task Resolve_ProjectMode_UsesEvaluatedTargetPath()
    {
        using var temp = new TempPreviewProject();
        var targetDirectory = Path.Combine(temp.Directory, "custom-output");
        Directory.CreateDirectory(targetDirectory);
        var targetAssemblyPath = Path.Combine(targetDirectory, "PreviewApp.dll");
        File.WriteAllText(targetAssemblyPath, string.Empty);

        var runner = new FakeProcessRunner((fileName, arguments, workingDirectory) =>
        {
            if (arguments.Contains("-getProperty:TargetFrameworks"))
            {
                return ProcessRunResult.Success("""
                    {
                      "Properties": {
                        "TargetFramework": "net10.0",
                        "TargetFrameworks": ""
                      }
                    }
                    """);
            }

            if (arguments.Contains("-getProperty:TargetPath"))
            {
                return ProcessRunResult.Success(targetAssemblyPath);
            }

            if (arguments.FirstOrDefault() == "build")
            {
                return ProcessRunResult.Success(string.Empty);
            }

            return new ProcessRunResult(1, string.Empty, "Unexpected command");
        });

        var resolver = new PreviewTargetResolver(runner);

        var target = await resolver.Resolve(new PreviewAxamlRequest(
            AxamlPath: temp.AxamlPath,
            ProjectPath: temp.ProjectPath), CancellationToken.None);

        Assert.Equal(targetAssemblyPath, target.AssemblyPath);
        Assert.Contains(runner.Calls, call => call.Arguments.FirstOrDefault() == "build");
    }

    [Fact]
    public async Task Resolve_WhenAxamlClassLivesInReferencedOutputAssembly_UsesThatAssemblyAsXamlLocalAssembly()
    {
        using var temp = new TempAxamlFile(
            """
            <UserControl xmlns="https://github.com/avaloniaui"
                         xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
                         x:Class="Zafiro.Avalonia.Mcp.Tests.Preview.PreviewDesignData" />
            """);
        var resolver = new PreviewTargetResolver(new FakeProcessRunner());
        var targetAssemblyPath = typeof(PreviewTargetResolver).Assembly.Location;

        var target = await resolver.Resolve(
            new PreviewAxamlRequest(temp.Path, AssemblyPath: targetAssemblyPath),
            CancellationToken.None);

        Assert.Equal(typeof(PreviewDesignData).Assembly.Location, target.XamlAssemblyPath);
    }

    [Fact]
    public async Task Resolve_WhenAxamlHasNoClass_UsesTargetAssemblyAsXamlLocalAssembly()
    {
        using var temp = new TempAxamlFile("""<UserControl xmlns="https://github.com/avaloniaui" />""");
        var resolver = new PreviewTargetResolver(new FakeProcessRunner());
        var targetAssemblyPath = typeof(PreviewTargetResolver).Assembly.Location;

        var target = await resolver.Resolve(
            new PreviewAxamlRequest(temp.Path, AssemblyPath: targetAssemblyPath),
            CancellationToken.None);

        Assert.Equal(targetAssemblyPath, target.XamlAssemblyPath);
    }

    private sealed class FakeProcessRunner : IProcessRunner
    {
        private readonly Func<string, IReadOnlyList<string>, string?, ProcessRunResult> handler;
        private readonly List<ProcessCall> calls = [];

        public FakeProcessRunner(Func<string, IReadOnlyList<string>, string?, ProcessRunResult>? handler = null)
        {
            this.handler = handler ?? ((_, _, _) => ProcessRunResult.Success(string.Empty));
        }

        public IReadOnlyList<ProcessCall> Calls => calls;

        public Task<ProcessRunResult> Run(
            string fileName,
            IReadOnlyList<string> arguments,
            string? workingDirectory,
            CancellationToken cancellationToken)
        {
            calls.Add(new ProcessCall(fileName, arguments, workingDirectory));
            return Task.FromResult(handler(fileName, arguments, workingDirectory));
        }
    }

    private sealed record ProcessCall(string FileName, IReadOnlyList<string> Arguments, string? WorkingDirectory);

    private sealed class TempPreviewProject : IDisposable
    {
        public TempPreviewProject()
        {
            Directory = Path.Combine(Path.GetTempPath(), "avalonia-mcp-preview-tests", Guid.NewGuid().ToString("N"));
            System.IO.Directory.CreateDirectory(Directory);
            System.IO.Directory.CreateDirectory(Path.Combine(Directory, "Views"));

            ProjectPath = Path.Combine(Directory, "PreviewApp.csproj");
            AxamlPath = Path.Combine(Directory, "Views", "SampleView.axaml");

            File.WriteAllText(ProjectPath, """
                <Project Sdk="Microsoft.NET.Sdk">
                  <PropertyGroup>
                    <TargetFramework>net10.0</TargetFramework>
                  </PropertyGroup>
                </Project>
                """);
            File.WriteAllText(AxamlPath, """
                <UserControl xmlns="https://github.com/avaloniaui" />
                """);
        }

        public string Directory { get; }
        public string ProjectPath { get; }
        public string AxamlPath { get; }

        public void Dispose()
        {
            try
            {
                System.IO.Directory.Delete(Directory, recursive: true);
            }
            catch
            {
            }
        }
    }

    private sealed class TempAxamlFile : IDisposable
    {
        private readonly string root;

        public TempAxamlFile(string content)
        {
            root = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "avalonia-mcp-preview-target-tests", Guid.NewGuid().ToString("N"));
            System.IO.Directory.CreateDirectory(root);
            Path = System.IO.Path.Combine(root, "View.axaml");
            File.WriteAllText(Path, content);
        }

        public string Path { get; }

        public void Dispose()
        {
            try
            {
                System.IO.Directory.Delete(root, recursive: true);
            }
            catch
            {
            }
        }
    }
}
