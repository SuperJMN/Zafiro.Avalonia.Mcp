using System.Text.Json;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Xml.Linq;

namespace Zafiro.Avalonia.Mcp.Tool.Preview;

public sealed class PreviewTargetResolver
{
    private readonly IProcessRunner processRunner;

    public PreviewTargetResolver(IProcessRunner processRunner)
    {
        this.processRunner = processRunner;
    }

    internal async Task<PreviewTarget> Resolve(PreviewAxamlRequest request, CancellationToken cancellationToken)
    {
        var axamlPath = ResolveExistingFile(request.AxamlPath, "AXAML file does not exist");
        var hasProject = !string.IsNullOrWhiteSpace(request.ProjectPath);
        var hasAssembly = !string.IsNullOrWhiteSpace(request.AssemblyPath);

        if (hasProject == hasAssembly)
        {
            throw new PreviewValidationException(
                "INVALID_PARAM",
                hasProject
                    ? "Pass either projectPath or assemblyPath, not both."
                    : "Either projectPath or assemblyPath is required.");
        }

        var configuration = string.IsNullOrWhiteSpace(request.Configuration)
            ? "Debug"
            : request.Configuration.Trim();

        if (hasAssembly)
        {
            var assemblyPath = ResolveExistingFile(request.AssemblyPath!, "Assembly file does not exist");
            var xamlAssemblyPath = ResolveXamlAssemblyPath(axamlPath, assemblyPath);
            return new PreviewTarget(axamlPath, assemblyPath, xamlAssemblyPath, null, request.EntryType, request.TargetFramework, configuration);
        }

        var projectPath = ResolveExistingFile(request.ProjectPath!, "Project file does not exist");
        var targetFramework = string.IsNullOrWhiteSpace(request.TargetFramework)
            ? await ResolveDefaultTargetFramework(projectPath, configuration, cancellationToken)
            : request.TargetFramework.Trim();

        if (request.Build)
        {
            await BuildProject(projectPath, configuration, targetFramework, cancellationToken);
        }

        var targetPath = await EvaluateTargetPath(projectPath, configuration, targetFramework, cancellationToken);
        if (!Path.IsPathFullyQualified(targetPath))
        {
            targetPath = Path.GetFullPath(targetPath, Path.GetDirectoryName(projectPath)!);
        }

        if (!File.Exists(targetPath))
        {
            throw new PreviewValidationException(
                "INVALID_PARAM",
                $"Target assembly was not found at evaluated TargetPath '{targetPath}'. Build the project or pass build=true.");
        }

        var resolvedXamlAssemblyPath = ResolveXamlAssemblyPath(axamlPath, targetPath);
        return new PreviewTarget(axamlPath, targetPath, resolvedXamlAssemblyPath, projectPath, request.EntryType, targetFramework, configuration);
    }

    internal static string ResolveXamlAssemblyPath(string axamlPath, string targetAssemblyPath)
    {
        var xClass = TryReadXClass(axamlPath);
        if (string.IsNullOrWhiteSpace(xClass))
        {
            return targetAssemblyPath;
        }

        var targetDirectory = Path.GetDirectoryName(targetAssemblyPath);
        if (string.IsNullOrWhiteSpace(targetDirectory))
        {
            return targetAssemblyPath;
        }

        var candidateAssemblies = Directory
            .EnumerateFiles(targetDirectory, "*.dll")
            .Prepend(targetAssemblyPath)
            .Distinct(StringComparer.OrdinalIgnoreCase);

        return candidateAssemblies.FirstOrDefault(path => AssemblyContainsType(path, xClass)) ?? targetAssemblyPath;
    }

    private static string? TryReadXClass(string axamlPath)
    {
        try
        {
            var document = XDocument.Load(axamlPath, LoadOptions.None);
            return document.Root?.Attribute(XName.Get("Class", "http://schemas.microsoft.com/winfx/2006/xaml"))?.Value.Trim();
        }
        catch
        {
            return null;
        }
    }

    private static bool AssemblyContainsType(string assemblyPath, string fullTypeName)
    {
        try
        {
            using var stream = File.OpenRead(assemblyPath);
            using var reader = new PEReader(stream);
            if (!reader.HasMetadata)
            {
                return false;
            }

            var metadata = reader.GetMetadataReader();
            foreach (var handle in metadata.TypeDefinitions)
            {
                var type = metadata.GetTypeDefinition(handle);
                var name = metadata.GetString(type.Name);
                var ns = metadata.GetString(type.Namespace);
                var candidate = string.IsNullOrWhiteSpace(ns) ? name : $"{ns}.{name}";
                if (string.Equals(candidate, fullTypeName, StringComparison.Ordinal))
                {
                    return true;
                }
            }
        }
        catch
        {
        }

        return false;
    }

    private async Task<string?> ResolveDefaultTargetFramework(
        string projectPath,
        string configuration,
        CancellationToken cancellationToken)
    {
        var result = await RunDotnet([
            "msbuild",
            projectPath,
            "-nologo",
            "-getProperty:TargetFrameworks",
            "-getProperty:TargetFramework",
            $"-p:Configuration={configuration}",
        ], Path.GetDirectoryName(projectPath), cancellationToken);

        var properties = ParseProperties(result.StandardOutput);
        var targetFramework = properties.GetValueOrDefault("TargetFramework");
        var targetFrameworks = properties.GetValueOrDefault("TargetFrameworks");

        if (!string.IsNullOrWhiteSpace(targetFramework))
        {
            return targetFramework.Trim();
        }

        return targetFrameworks?
            .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .FirstOrDefault();
    }

    private async Task BuildProject(
        string projectPath,
        string configuration,
        string? targetFramework,
        CancellationToken cancellationToken)
    {
        var arguments = new List<string>
        {
            "build",
            projectPath,
            "-c",
            configuration,
            "-v",
            "minimal",
        };

        if (!string.IsNullOrWhiteSpace(targetFramework))
        {
            arguments.Add("-f");
            arguments.Add(targetFramework);
        }

        var result = await RunDotnet(arguments, Path.GetDirectoryName(projectPath), cancellationToken, "BUILD_FAILED");
        if (result.ExitCode != 0)
        {
            throw new PreviewValidationException("BUILD_FAILED", CleanError(result.StandardError, result.StandardOutput));
        }
    }

    private async Task<string> EvaluateTargetPath(
        string projectPath,
        string configuration,
        string? targetFramework,
        CancellationToken cancellationToken)
    {
        var arguments = new List<string>
        {
            "msbuild",
            projectPath,
            "-nologo",
            "-getProperty:TargetPath",
            $"-p:Configuration={configuration}",
        };

        if (!string.IsNullOrWhiteSpace(targetFramework))
        {
            arguments.Add($"-p:TargetFramework={targetFramework}");
        }

        var result = await RunDotnet(arguments, Path.GetDirectoryName(projectPath), cancellationToken);
        var targetPath = ParseSingleProperty(result.StandardOutput, "TargetPath");

        if (string.IsNullOrWhiteSpace(targetPath))
        {
            throw new PreviewValidationException("INVALID_PARAM", "MSBuild did not return a TargetPath for the project.");
        }

        return targetPath.Trim();
    }

    private async Task<ProcessRunResult> RunDotnet(
        IReadOnlyList<string> arguments,
        string? workingDirectory,
        CancellationToken cancellationToken,
        string errorCode = "INVALID_PARAM")
    {
        var result = await processRunner.Run("dotnet", arguments, workingDirectory, cancellationToken);
        if (result.ExitCode != 0)
        {
            throw new PreviewValidationException(errorCode, CleanError(result.StandardError, result.StandardOutput));
        }

        return result;
    }

    private static string ResolveExistingFile(string path, string message)
    {
        var fullPath = Path.GetFullPath(path);
        if (!File.Exists(fullPath))
        {
            throw new PreviewValidationException("INVALID_PARAM", $"{message}: '{fullPath}'.");
        }

        return fullPath;
    }

    private static Dictionary<string, string?> ParseProperties(string output)
    {
        var trimmed = output.Trim();
        if (trimmed.StartsWith('{'))
        {
            using var document = JsonDocument.Parse(trimmed);
            if (document.RootElement.TryGetProperty("Properties", out var properties))
            {
                return properties.EnumerateObject()
                    .ToDictionary(
                        property => property.Name,
                        property => property.Value.ValueKind == JsonValueKind.String
                            ? property.Value.GetString()
                            : property.Value.ToString(),
                        StringComparer.Ordinal);
            }
        }

        return [];
    }

    private static string ParseSingleProperty(string output, string propertyName)
    {
        var properties = ParseProperties(output);
        if (properties.TryGetValue(propertyName, out var value))
        {
            return value ?? string.Empty;
        }

        return output.Trim();
    }

    private static string CleanError(string standardError, string standardOutput)
    {
        var message = string.IsNullOrWhiteSpace(standardError) ? standardOutput : standardError;
        return string.IsNullOrWhiteSpace(message) ? "Command failed." : message.Trim();
    }
}
