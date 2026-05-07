using System.Diagnostics;
using System.Reflection;
using System.Security;
using System.Text.Json;

namespace Zafiro.Avalonia.Mcp.Tool.Preview;

internal sealed class PreviewHostProjectBuilder
{
    private const string HostAssemblyName = "Zafiro.Avalonia.Mcp.PreviewHost";
    private readonly IProcessRunner processRunner;
    private readonly string hostRoot;
    private readonly IPreviewHostDependencyResolver dependencyResolver;

    public PreviewHostProjectBuilder(IProcessRunner processRunner)
        : this(processRunner, DefaultHostRoot(), new PreviewHostDependencyResolver())
    {
    }

    internal PreviewHostProjectBuilder(
        IProcessRunner processRunner,
        string hostRoot,
        IPreviewHostDependencyResolver dependencyResolver)
    {
        this.processRunner = processRunner;
        this.hostRoot = hostRoot;
        this.dependencyResolver = dependencyResolver;
    }

    public async Task<PreviewHostLaunch> Build(
        PreviewTarget target,
        int width,
        int height,
        CancellationToken cancellationToken)
    {
        var targetFramework = string.IsNullOrWhiteSpace(target.TargetFramework)
            ? "net10.0"
            : target.TargetFramework;
        var hostDirectory = Path.Combine(hostRoot, Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(hostDirectory);

        var projectPath = Path.Combine(hostDirectory, $"{HostAssemblyName}.csproj");
        var programPath = Path.Combine(hostDirectory, "Program.cs");
        var dependency = dependencyResolver.Resolve();

        await File.WriteAllTextAsync(projectPath, CreateProject(target, targetFramework, dependency), cancellationToken);
        await File.WriteAllTextAsync(programPath, PreviewHostSource.Code, cancellationToken);

        var result = await processRunner.Run(
            "dotnet",
            [
                "build",
                projectPath,
                "-c",
                "Release",
                "-f",
                targetFramework,
                "-v",
                "minimal",
                "--nologo",
            ],
            hostDirectory,
            cancellationToken);

        if (result.ExitCode != 0)
        {
            throw new PreviewValidationException("BUILD_FAILED", CleanError(result.StandardError, result.StandardOutput));
        }

        var hostAssemblyPath = Path.Combine(hostDirectory, "bin", "Release", targetFramework, $"{HostAssemblyName}.dll");
        if (!File.Exists(hostAssemblyPath))
        {
            throw new PreviewValidationException("INTERNAL", $"Preview host build did not produce '{hostAssemblyPath}'.");
        }

        return new PreviewHostLaunch(projectPath, CreateStartInfo(hostAssemblyPath, target, width, height));
    }

    private static string CreateProject(PreviewTarget target, string targetFramework, PreviewHostDependency dependency)
    {
        var references = CreateReferenceItems(target.AssemblyPath);
        var appHostReference = CreateAppHostReference(dependency);
        var xamlLoaderItems = CreateXamlLoaderItems(target.AssemblyPath);
        var runtimeContent = CreateRuntimeContent(target.AssemblyPath);

        return $$"""
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <OutputType>Exe</OutputType>
                <TargetFramework>{{Xml(targetFramework)}}</TargetFramework>
                <AssemblyName>{{HostAssemblyName}}</AssemblyName>
                <Nullable>enable</Nullable>
                <ImplicitUsings>enable</ImplicitUsings>
              </PropertyGroup>

              <ItemGroup>
            {{references}}
              </ItemGroup>

              <ItemGroup>
            {{appHostReference}}
              </ItemGroup>

            {{xamlLoaderItems}}
            {{runtimeContent}}
            </Project>
            """;
    }

    private static string CreateReferenceItems(string targetAssemblyPath)
    {
        var targetDirectory = Path.GetDirectoryName(targetAssemblyPath)
                              ?? throw new PreviewValidationException("INVALID_PARAM", "Target assembly has no parent directory.");

        return string.Join(
            Environment.NewLine,
            Directory.EnumerateFiles(targetDirectory, "*.dll")
                .Where(ShouldReferenceTargetOutput)
                .Order(StringComparer.OrdinalIgnoreCase)
                .Select(path =>
                    $$"""
                        <Reference Include="{{Xml(Path.GetFileNameWithoutExtension(path))}}">
                          <HintPath>{{Xml(path)}}</HintPath>
                          <Private>true</Private>
                        </Reference>
                    """));
    }

    private static string CreateAppHostReference(PreviewHostDependency dependency)
    {
        if (!string.IsNullOrWhiteSpace(dependency.AppHostProjectPath))
        {
            return $$"""
                    <ProjectReference Include="{{Xml(dependency.AppHostProjectPath)}}">
                      <Private>true</Private>
                    </ProjectReference>
                """;
        }

        if (string.IsNullOrWhiteSpace(dependency.AppHostPackageVersion))
        {
            throw new PreviewValidationException("INTERNAL", "Could not resolve a Zafiro.Avalonia.Mcp.AppHost source project or package version.");
        }

        return $$"""
                <PackageReference Include="Zafiro.Avalonia.Mcp.AppHost" Version="{{Xml(dependency.AppHostPackageVersion)}}" />
            """;
    }

    private static string CreateXamlLoaderItems(string targetAssemblyPath)
    {
        var targetDirectory = Path.GetDirectoryName(targetAssemblyPath)
                              ?? throw new PreviewValidationException("INVALID_PARAM", "Target assembly has no parent directory.");
        var loaderPath = Path.Combine(targetDirectory, "Avalonia.Markup.Xaml.Loader.dll");
        if (File.Exists(loaderPath))
        {
            return string.Empty;
        }

        var avaloniaVersion = ResolveAvaloniaPackageVersion(targetAssemblyPath);
        if (string.IsNullOrWhiteSpace(avaloniaVersion))
        {
            throw new PreviewValidationException(
                "INVALID_PARAM",
                "Could not resolve the Avalonia package version from the target app output. Build the app before launching the AXAML preview.");
        }

        return $$"""
              <PropertyGroup>
                <AvaloniaXamlLoaderTfm>net8.0</AvaloniaXamlLoaderTfm>
                <AvaloniaXamlLoaderTfm Condition="Exists('$(PkgAvalonia_Markup_Xaml_Loader)/lib/$(TargetFramework)/Avalonia.Markup.Xaml.Loader.dll')">$(TargetFramework)</AvaloniaXamlLoaderTfm>
                <AvaloniaXamlLoaderTfm Condition="!Exists('$(PkgAvalonia_Markup_Xaml_Loader)/lib/$(AvaloniaXamlLoaderTfm)/Avalonia.Markup.Xaml.Loader.dll') And Exists('$(PkgAvalonia_Markup_Xaml_Loader)/lib/netstandard2.0/Avalonia.Markup.Xaml.Loader.dll')">netstandard2.0</AvaloniaXamlLoaderTfm>
              </PropertyGroup>

              <ItemGroup>
                <PackageReference Include="Avalonia.Markup.Xaml.Loader" Version="{{Xml(avaloniaVersion)}}" GeneratePathProperty="true" PrivateAssets="all" ExcludeAssets="all" />
                <Reference Include="Avalonia.Markup.Xaml.Loader">
                  <HintPath>$(PkgAvalonia_Markup_Xaml_Loader)/lib/$(AvaloniaXamlLoaderTfm)/Avalonia.Markup.Xaml.Loader.dll</HintPath>
                  <Private>true</Private>
                </Reference>
              </ItemGroup>

            """;
    }

    private static string CreateRuntimeContent(string targetAssemblyPath)
    {
        var targetDirectory = Path.GetDirectoryName(targetAssemblyPath)
                              ?? throw new PreviewValidationException("INVALID_PARAM", "Target assembly has no parent directory.");
        var runtimesDirectory = Path.Combine(targetDirectory, "runtimes");
        if (!Directory.Exists(runtimesDirectory))
        {
            return string.Empty;
        }

        return $$"""
              <ItemGroup>
                <Content Include="{{Xml(runtimesDirectory)}}/**/*">
                  <Link>runtimes/%(RecursiveDir)%(Filename)%(Extension)</Link>
                  <CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>
                </Content>
              </ItemGroup>

            """;
    }

    private static bool ShouldReferenceTargetOutput(string path)
    {
        var fileName = Path.GetFileName(path);
        return !string.Equals(fileName, "Zafiro.Avalonia.Mcp.AppHost.dll", StringComparison.OrdinalIgnoreCase) &&
               !string.Equals(fileName, "Zafiro.Avalonia.Mcp.Protocol.dll", StringComparison.OrdinalIgnoreCase);
    }

    private static string? ResolveAvaloniaPackageVersion(string targetAssemblyPath)
    {
        var depsPath = Path.ChangeExtension(targetAssemblyPath, ".deps.json");
        if (File.Exists(depsPath))
        {
            using var document = JsonDocument.Parse(File.ReadAllText(depsPath));
            if (document.RootElement.TryGetProperty("libraries", out var libraries))
            {
                var avaloniaLibrary = libraries
                    .EnumerateObject()
                    .Select(property => property.Name)
                    .FirstOrDefault(name => name.StartsWith("Avalonia/", StringComparison.OrdinalIgnoreCase));

                if (avaloniaLibrary is not null)
                {
                    return avaloniaLibrary["Avalonia/".Length..];
                }
            }
        }

        var targetDirectory = Path.GetDirectoryName(targetAssemblyPath);
        var avaloniaPath = targetDirectory is null ? null : Path.Combine(targetDirectory, "Avalonia.dll");
        if (avaloniaPath is null || !File.Exists(avaloniaPath))
        {
            return null;
        }

        var version = FileVersionInfo.GetVersionInfo(avaloniaPath).ProductVersion
                      ?? FileVersionInfo.GetVersionInfo(avaloniaPath).FileVersion;
        return NormalizeVersion(version);
    }

    private static string? NormalizeVersion(string? version)
    {
        if (string.IsNullOrWhiteSpace(version))
        {
            return null;
        }

        return Version.TryParse(version, out var parsed) && parsed.Revision == 0
            ? $"{parsed.Major}.{parsed.Minor}.{parsed.Build}"
            : version;
    }

    private static ProcessStartInfo CreateStartInfo(string hostAssemblyPath, PreviewTarget target, int width, int height)
    {
        var startInfo = new ProcessStartInfo("dotnet")
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            WorkingDirectory = Path.GetDirectoryName(target.AssemblyPath) ?? Environment.CurrentDirectory,
        };

        startInfo.ArgumentList.Add(hostAssemblyPath);
        startInfo.ArgumentList.Add("--assembly");
        startInfo.ArgumentList.Add(target.AssemblyPath);
        startInfo.ArgumentList.Add("--axaml");
        startInfo.ArgumentList.Add(target.AxamlPath);
        startInfo.ArgumentList.Add("--width");
        startInfo.ArgumentList.Add(width.ToString());
        startInfo.ArgumentList.Add("--height");
        startInfo.ArgumentList.Add(height.ToString());

        if (!string.IsNullOrWhiteSpace(target.EntryType))
        {
            startInfo.ArgumentList.Add("--entry-type");
            startInfo.ArgumentList.Add(target.EntryType);
        }

        return startInfo;
    }

    private static string DefaultHostRoot()
        => Path.Combine(Path.GetTempPath(), "zafiro-avalonia-mcp", "preview-hosts");

    private static string CleanError(string standardError, string standardOutput)
    {
        var message = string.IsNullOrWhiteSpace(standardError) ? standardOutput : standardError;
        return string.IsNullOrWhiteSpace(message) ? "Command failed." : message.Trim();
    }

    private static string Xml(string value)
        => SecurityElement.Escape(value) ?? string.Empty;
}

internal sealed record PreviewHostLaunch(string ProjectPath, ProcessStartInfo StartInfo);

internal sealed record PreviewHostDependency(string? AppHostProjectPath, string? AppHostPackageVersion);

internal interface IPreviewHostDependencyResolver
{
    PreviewHostDependency Resolve();
}

internal sealed class PreviewHostDependencyResolver : IPreviewHostDependencyResolver
{
    public PreviewHostDependency Resolve()
    {
        var sourceProject = FindSourceAppHostProject();
        return sourceProject is not null
            ? new PreviewHostDependency(sourceProject, null)
            : new PreviewHostDependency(null, ResolvePackageVersion());
    }

    private static string? FindSourceAppHostProject()
    {
        foreach (var root in SourceSearchRoots())
        {
            var directory = new DirectoryInfo(root);
            while (directory is not null)
            {
                var candidate = Path.Combine(
                    directory.FullName,
                    "src",
                    "Zafiro.Avalonia.Mcp.AppHost",
                    "Zafiro.Avalonia.Mcp.AppHost.csproj");

                if (File.Exists(candidate))
                {
                    return candidate;
                }

                directory = directory.Parent;
            }
        }

        return null;
    }

    private static IEnumerable<string> SourceSearchRoots()
    {
        yield return Environment.CurrentDirectory;
        yield return AppContext.BaseDirectory;

        var assemblyDirectory = Path.GetDirectoryName(typeof(PreviewHostDependencyResolver).Assembly.Location);
        if (!string.IsNullOrWhiteSpace(assemblyDirectory))
        {
            yield return assemblyDirectory;
        }
    }

    private static string? ResolvePackageVersion()
    {
        var version = typeof(PreviewHostDependencyResolver)
            .Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
            ?.InformationalVersion;

        return string.IsNullOrWhiteSpace(version)
            ? typeof(PreviewHostDependencyResolver).Assembly.GetName().Version?.ToString(3)
            : version.Split('+')[0];
    }
}
