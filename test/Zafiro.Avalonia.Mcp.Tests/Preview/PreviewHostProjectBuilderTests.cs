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
        var previousDisplay = Environment.GetEnvironmentVariable("DISPLAY");

        PreviewHostLaunch launch;
        try
        {
            Environment.SetEnvironmentVariable("DISPLAY", previousDisplay ?? ":1");
            launch = await builder.Build(target, width: 320, height: 240, CancellationToken.None);
        }
        finally
        {
            Environment.SetEnvironmentVariable("DISPLAY", previousDisplay);
        }

        var projectText = File.ReadAllText(launch.ProjectPath);
        var programText = File.ReadAllText(Path.Combine(Path.GetDirectoryName(launch.ProjectPath)!, "Program.cs"));
        var nativeResolverText = File.ReadAllText(Path.Combine(Path.GetDirectoryName(launch.ProjectPath)!, "PreviewNativeAssetResolver.cs"));

        Assert.Contains($"<HintPath>{temp.TargetAssemblyPath}</HintPath>", projectText);
        Assert.Contains($"<ProjectReference Include=\"{temp.AppHostProjectPath}\"", projectText);
        Assert.Contains("Avalonia.Desktop", projectText);
        Assert.Contains("Avalonia.Markup.Xaml.Loader", projectText);
        Assert.Contains("Version=\"12.0.2\"", projectText);
        Assert.Contains("ExcludeAssets=\"all\"", projectText);
        Assert.Equal(PreviewHostSource.Code, programText);
        Assert.Equal(PreviewNativeAssetResolverSource.Code, nativeResolverText);
        Assert.Contains(runner.Calls, call => call.FileName == "dotnet" && call.Arguments.Contains("build"));
        Assert.Equal("dotnet", launch.StartInfo.FileName);
        Assert.Equal("1", launch.StartInfo.Environment["ZAFIRO_AVALONIA_MCP_PREVIEW"]);
        Assert.Contains(launch.StartInfo.ArgumentList, argument => argument.EndsWith("Zafiro.Avalonia.Mcp.PreviewHost.dll", StringComparison.Ordinal));
        Assert.Contains(launch.StartInfo.ArgumentList, argument => argument == "--assembly");
        Assert.Contains(launch.StartInfo.ArgumentList, argument => argument == temp.TargetAssemblyPath);
        Assert.Contains(launch.StartInfo.ArgumentList, argument => argument == "--xaml-assembly");
    }

    [Fact]
    public async Task Build_DoesNotAddDesktopPackage_WhenTargetOutputAlreadyContainsAvaloniaDesktop()
    {
        using var temp = new TempHostProject(includeAvaloniaDesktop: true);
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
        var previousDisplay = Environment.GetEnvironmentVariable("DISPLAY");

        PreviewHostLaunch launch;
        try
        {
            Environment.SetEnvironmentVariable("DISPLAY", previousDisplay ?? ":1");
            launch = await builder.Build(target, width: 320, height: 240, CancellationToken.None);
        }
        finally
        {
            Environment.SetEnvironmentVariable("DISPLAY", previousDisplay);
        }

        var projectText = File.ReadAllText(launch.ProjectPath);
        Assert.Contains($"<HintPath>{temp.AvaloniaDesktopPath}</HintPath>", projectText);
        Assert.DoesNotContain("<PackageReference Include=\"Avalonia.Desktop\"", projectText);
    }

    [Fact]
    public async Task Build_CreatesHeadlessHost_WhenBackendIsHeadless()
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
            Configuration: "Debug",
            Backend: PreviewBackend.Headless);

        var launch = await builder.Build(target, width: 320, height: 240, CancellationToken.None);

        var projectText = File.ReadAllText(launch.ProjectPath);

        Assert.Contains("<PackageReference Include=\"Avalonia.Headless\"", projectText);
        Assert.DoesNotContain("<PackageReference Include=\"Avalonia.Desktop\"", projectText);
        Assert.Contains(launch.StartInfo.ArgumentList, argument => argument == "--backend");
        Assert.Contains(launch.StartInfo.ArgumentList, argument => argument == "headless");
        Assert.Equal("headless", launch.Backend);
    }

    [Fact]
    public async Task Build_CompilesHostProject_ForApplicationOnlyTargetWithoutAvaloniaDesktop()
    {
        using var temp = new ApplicationOnlyPreviewTarget();
        await temp.BuildTarget();

        var builder = new PreviewHostProjectBuilder(
            new DotnetProcessRunner(),
            temp.HostRoot,
            new FixedPreviewHostDependencyResolver(new PreviewHostDependency(FindRepositoryFile("src/Zafiro.Avalonia.Mcp.AppHost/Zafiro.Avalonia.Mcp.AppHost.csproj"), null)));
        var target = new PreviewTarget(
            temp.AxamlPath,
            temp.TargetAssemblyPath,
            temp.TargetAssemblyPath,
            temp.TargetProjectPath,
            EntryType: null,
            TargetFramework: "net10.0",
            Configuration: "Debug");
        var previousDisplay = Environment.GetEnvironmentVariable("DISPLAY");

        PreviewHostLaunch launch;
        try
        {
            Environment.SetEnvironmentVariable("DISPLAY", previousDisplay ?? ":1");
            launch = await builder.Build(target, width: 320, height: 240, CancellationToken.None);
        }
        finally
        {
            Environment.SetEnvironmentVariable("DISPLAY", previousDisplay);
        }

        var projectText = File.ReadAllText(launch.ProjectPath);
        Assert.Contains("Avalonia.Desktop", projectText);
    }

    private static string FindRepositoryFile(string relativePath)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(directory.FullName, relativePath);
            if (File.Exists(candidate))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        throw new FileNotFoundException($"Could not find '{relativePath}' from '{AppContext.BaseDirectory}'.");
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
        public TempHostProject(bool includeAvaloniaDesktop = false)
        {
            Root = Path.Combine(Path.GetTempPath(), "avalonia-mcp-preview-host-builder-tests", Guid.NewGuid().ToString("N"));
            HostRoot = Path.Combine(Root, "hosts");
            var targetOutput = Path.Combine(Root, "target", "bin", "Debug", "net10.0");
            Directory.CreateDirectory(targetOutput);
            Directory.CreateDirectory(HostRoot);

            TargetProjectPath = Path.Combine(Root, "target", "TargetApp.csproj");
            TargetAssemblyPath = Path.Combine(targetOutput, "TargetApp.dll");
            AvaloniaDesktopPath = Path.Combine(targetOutput, "Avalonia.Desktop.dll");
            var depsPath = Path.Combine(targetOutput, "TargetApp.deps.json");
            AxamlPath = Path.Combine(Root, "target", "Views", "View.axaml");
            AppHostProjectPath = Path.Combine(Root, "src", "Zafiro.Avalonia.Mcp.AppHost", "Zafiro.Avalonia.Mcp.AppHost.csproj");

            Directory.CreateDirectory(Path.GetDirectoryName(TargetProjectPath)!);
            Directory.CreateDirectory(Path.GetDirectoryName(AxamlPath)!);
            Directory.CreateDirectory(Path.GetDirectoryName(AppHostProjectPath)!);

            File.WriteAllText(TargetProjectPath, "<Project Sdk=\"Microsoft.NET.Sdk\" />");
            File.WriteAllText(TargetAssemblyPath, string.Empty);
            File.WriteAllText(Path.Combine(targetOutput, "Avalonia.dll"), string.Empty);
            if (includeAvaloniaDesktop)
            {
                File.WriteAllText(AvaloniaDesktopPath, string.Empty);
            }

            File.WriteAllText(depsPath, """{"libraries":{"Avalonia/12.0.2":{}}}""");
            File.WriteAllText(AxamlPath, "<UserControl xmlns=\"https://github.com/avaloniaui\" />");
            File.WriteAllText(AppHostProjectPath, "<Project Sdk=\"Microsoft.NET.Sdk\" />");
        }

        public string Root { get; }
        public string HostRoot { get; }
        public string TargetProjectPath { get; }
        public string TargetAssemblyPath { get; }
        public string AvaloniaDesktopPath { get; }
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

    private sealed class ApplicationOnlyPreviewTarget : IDisposable
    {
        public ApplicationOnlyPreviewTarget()
        {
            Root = Path.Combine(Path.GetTempPath(), "avalonia-mcp-application-only-preview", Guid.NewGuid().ToString("N"));
            HostRoot = Path.Combine(Root, "hosts");
            var appDirectory = Path.Combine(Root, "PreviewFixture.ApplicationOnly");
            Directory.CreateDirectory(appDirectory);
            Directory.CreateDirectory(Path.Combine(appDirectory, "Views"));
            Directory.CreateDirectory(HostRoot);

            TargetProjectPath = Path.Combine(appDirectory, "PreviewFixture.ApplicationOnly.csproj");
            AxamlPath = Path.Combine(appDirectory, "Views", "PreviewView.axaml");
            TargetAssemblyPath = Path.Combine(appDirectory, "bin", "Debug", "net10.0", "PreviewFixture.ApplicationOnly.dll");

            File.WriteAllText(TargetProjectPath, """
                <Project Sdk="Microsoft.NET.Sdk">
                  <PropertyGroup>
                    <TargetFramework>net10.0</TargetFramework>
                    <Nullable>enable</Nullable>
                    <ImplicitUsings>enable</ImplicitUsings>
                    <CopyLocalLockFileAssemblies>true</CopyLocalLockFileAssemblies>
                    <GenerateDependencyFile>true</GenerateDependencyFile>
                  </PropertyGroup>
                  <ItemGroup>
                    <PackageReference Include="Avalonia" Version="12.0.2" />
                    <PackageReference Include="Avalonia.Markup.Xaml.Loader" Version="12.0.2" />
                  </ItemGroup>
                </Project>
                """);
            File.WriteAllText(Path.Combine(appDirectory, "App.cs"), """
                using Avalonia;

                namespace PreviewFixture.ApplicationOnly;

                public sealed class App : Application;
                """);
            File.WriteAllText(AxamlPath, """
                <UserControl xmlns="https://github.com/avaloniaui">
                  <TextBlock Text="Application-only preview" />
                </UserControl>
                """);
        }

        public string Root { get; }
        public string HostRoot { get; }
        public string TargetProjectPath { get; }
        public string TargetAssemblyPath { get; }
        public string AxamlPath { get; }

        public async Task BuildTarget()
        {
            var runner = new DotnetProcessRunner();
            var result = await runner.Run(
                "dotnet",
                [
                    "build",
                    TargetProjectPath,
                    "-c",
                    "Debug",
                    "-f",
                    "net10.0",
                    "-v",
                    "minimal",
                    "--nologo",
                ],
                Root,
                CancellationToken.None);

            Assert.True(result.ExitCode == 0, $"Target build failed. stdout: {result.StandardOutput} stderr: {result.StandardError}");
            Assert.True(File.Exists(TargetAssemblyPath), $"Expected target assembly at '{TargetAssemblyPath}'.");
        }

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
