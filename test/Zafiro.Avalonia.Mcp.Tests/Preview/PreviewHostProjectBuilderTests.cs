using Xunit;
using Zafiro.Avalonia.Mcp.Tool.Preview;

namespace Zafiro.Avalonia.Mcp.Tests.Preview;

public sealed class PreviewHostProjectBuilderTests
{
    [Fact]
    public void DependencyResolver_FindsSourceAppHostProject_FromCurrentDirectory()
    {
        using var temp = new TempHostProject();
        var previousDirectory = Environment.CurrentDirectory;

        try
        {
            Environment.CurrentDirectory = temp.Root;

            var dependency = new PreviewHostDependencyResolver().Resolve();

            Assert.Equal(temp.AppHostProjectPath, dependency.AppHostProjectPath);
            Assert.Null(dependency.AppHostPackageVersion);
        }
        finally
        {
            Environment.CurrentDirectory = previousDirectory;
        }
    }

    [Fact]
    public async Task Build_CreatesHostProject_ReferencingTargetOutputAndAppHostSource()
    {
        using var temp = new TempHostProject();
        var runner = new FakeProcessRunner();
        var builder = new PreviewHostProjectBuilder(
            runner,
            temp.HostRoot,
            new FixedPreviewHostDependencyResolver(new PreviewHostDependency(temp.AppHostProjectPath, null)));

        var target = new PreviewTarget(
            temp.AxamlPath,
            temp.TargetAssemblyPath,
            temp.TargetAssemblyPath,
            temp.TargetProjectPath,
            EntryType: null,
            TargetFramework: "net10.0",
            Configuration: "Debug");

        var launch = await builder.Build(target, width: 320, height: 240, CancellationToken.None);
        var projectText = File.ReadAllText(launch.ProjectPath);

        Assert.Contains($"<HintPath>{temp.TargetAssemblyPath}</HintPath>", projectText);
        Assert.Contains($"<ProjectReference Include=\"{temp.AppHostProjectPath}\"", projectText);
        Assert.DoesNotContain("Avalonia.Desktop", projectText);
        Assert.Contains("Avalonia.Markup.Xaml.Loader", projectText);
        Assert.Contains("Version=\"12.0.2\"", projectText);
        Assert.Contains("ExcludeAssets=\"all\"", projectText);
        Assert.Contains(runner.Calls, call => call.FileName == "dotnet" && call.Arguments.Contains("build"));
        Assert.Equal("dotnet", launch.StartInfo.FileName);
        Assert.Contains(launch.StartInfo.ArgumentList, argument => argument.EndsWith("Zafiro.Avalonia.Mcp.PreviewHost.dll", StringComparison.Ordinal));
        Assert.Contains(launch.StartInfo.ArgumentList, argument => argument == "--assembly");
        Assert.Contains(launch.StartInfo.ArgumentList, argument => argument == temp.TargetAssemblyPath);
        Assert.Contains(launch.StartInfo.ArgumentList, argument => argument == "--xaml-assembly");
    }

    private sealed class FixedPreviewHostDependencyResolver : IPreviewHostDependencyResolver
    {
        private readonly PreviewHostDependency dependency;

        public FixedPreviewHostDependencyResolver(PreviewHostDependency dependency)
        {
            this.dependency = dependency;
        }

        public PreviewHostDependency Resolve() => dependency;
    }

    private sealed class FakeProcessRunner : IProcessRunner
    {
        private readonly List<ProcessCall> calls = [];

        public IReadOnlyList<ProcessCall> Calls => calls;

        public Task<ProcessRunResult> Run(
            string fileName,
            IReadOnlyList<string> arguments,
            string? workingDirectory,
            CancellationToken cancellationToken)
        {
            calls.Add(new ProcessCall(fileName, arguments, workingDirectory));
            if (workingDirectory is not null && arguments.Contains("build"))
            {
                var outputDirectory = Path.Combine(workingDirectory, "bin", "Release", "net10.0");
                Directory.CreateDirectory(outputDirectory);
                File.WriteAllText(Path.Combine(outputDirectory, "Zafiro.Avalonia.Mcp.PreviewHost.dll"), string.Empty);
            }

            return Task.FromResult(ProcessRunResult.Success(string.Empty));
        }
    }

    private sealed record ProcessCall(string FileName, IReadOnlyList<string> Arguments, string? WorkingDirectory);

    private sealed class TempHostProject : IDisposable
    {
        public TempHostProject()
        {
            Root = Path.Combine(Path.GetTempPath(), "avalonia-mcp-preview-host-builder-tests", Guid.NewGuid().ToString("N"));
            HostRoot = Path.Combine(Root, "hosts");
            var targetOutput = Path.Combine(Root, "target", "bin", "Debug", "net10.0");
            Directory.CreateDirectory(targetOutput);
            Directory.CreateDirectory(HostRoot);

            TargetProjectPath = Path.Combine(Root, "target", "TargetApp.csproj");
            TargetAssemblyPath = Path.Combine(targetOutput, "TargetApp.dll");
            var depsPath = Path.Combine(targetOutput, "TargetApp.deps.json");
            AxamlPath = Path.Combine(Root, "target", "Views", "View.axaml");
            AppHostProjectPath = Path.Combine(Root, "src", "Zafiro.Avalonia.Mcp.AppHost", "Zafiro.Avalonia.Mcp.AppHost.csproj");

            Directory.CreateDirectory(Path.GetDirectoryName(TargetProjectPath)!);
            Directory.CreateDirectory(Path.GetDirectoryName(AxamlPath)!);
            Directory.CreateDirectory(Path.GetDirectoryName(AppHostProjectPath)!);

            File.WriteAllText(TargetProjectPath, "<Project Sdk=\"Microsoft.NET.Sdk\" />");
            File.WriteAllText(TargetAssemblyPath, string.Empty);
            File.WriteAllText(Path.Combine(targetOutput, "Avalonia.dll"), string.Empty);
            File.WriteAllText(depsPath, """{"libraries":{"Avalonia/12.0.2":{}}}""");
            File.WriteAllText(AxamlPath, "<UserControl xmlns=\"https://github.com/avaloniaui\" />");
            File.WriteAllText(AppHostProjectPath, "<Project Sdk=\"Microsoft.NET.Sdk\" />");
        }

        public string Root { get; }
        public string HostRoot { get; }
        public string TargetProjectPath { get; }
        public string TargetAssemblyPath { get; }
        public string AxamlPath { get; }
        public string AppHostProjectPath { get; }

        public void Dispose()
        {
            try
            {
                Directory.Delete(Root, recursive: true);
            }
            catch
            {
            }
        }
    }
}
