using System.Reflection;
using Avalonia;

namespace Zafiro.Avalonia.Mcp.Tool.Preview;

internal static class PreviewAppBuilderFactory
{
    public static AppBuilder Create(Assembly assembly, string? entryType)
    {
        if (!string.IsNullOrWhiteSpace(entryType))
        {
            var type = FindType(assembly, entryType);
            return CreateFromType(type);
        }

        var builderTypes = assembly.GetTypes()
            .Where(HasBuildAvaloniaApp)
            .ToArray();

        if (builderTypes.Length == 1)
        {
            return InvokeBuildAvaloniaApp(builderTypes[0]);
        }

        if (builderTypes.Length > 1)
        {
            throw new InvalidOperationException(
                "Multiple BuildAvaloniaApp entry types were found. Pass entryType. Candidates: " +
                string.Join(", ", builderTypes.Select(x => x.FullName)));
        }

        var applicationTypes = assembly.GetTypes()
            .Where(type => type is { IsAbstract: false } && typeof(Application).IsAssignableFrom(type))
            .ToArray();

        return applicationTypes.Length switch
        {
            1 => ConfigureApplication(applicationTypes[0]).UsePlatformDetect(),
            0 => throw new InvalidOperationException("No BuildAvaloniaApp method or Application subclass was found. Pass entryType."),
            _ => throw new InvalidOperationException(
                "Multiple Application subclasses were found. Pass entryType. Candidates: " +
                string.Join(", ", applicationTypes.Select(x => x.FullName)))
        };
    }

    private static AppBuilder CreateFromType(Type type)
    {
        if (HasBuildAvaloniaApp(type))
        {
            return InvokeBuildAvaloniaApp(type);
        }

        if (typeof(Application).IsAssignableFrom(type))
        {
            return ConfigureApplication(type).UsePlatformDetect();
        }

        throw new InvalidOperationException(
            $"Entry type '{type.FullName}' must expose a static parameterless BuildAvaloniaApp method or derive from Avalonia.Application.");
    }

    private static Type FindType(Assembly assembly, string entryType)
    {
        var exact = assembly.GetType(entryType, throwOnError: false, ignoreCase: false);
        if (exact is not null)
        {
            return exact;
        }

        var matches = assembly.GetTypes()
            .Where(type => string.Equals(type.Name, entryType, StringComparison.Ordinal) ||
                           string.Equals(type.FullName, entryType, StringComparison.Ordinal))
            .ToArray();

        return matches.Length switch
        {
            1 => matches[0],
            0 => throw new InvalidOperationException($"Entry type '{entryType}' was not found in '{assembly.GetName().Name}'."),
            _ => throw new InvalidOperationException($"Entry type '{entryType}' is ambiguous. Use the full type name.")
        };
    }

    private static bool HasBuildAvaloniaApp(Type type)
    {
        var method = GetBuildAvaloniaApp(type);
        return method is not null &&
               method.GetParameters().Length == 0 &&
               typeof(AppBuilder).IsAssignableFrom(method.ReturnType);
    }

    private static AppBuilder InvokeBuildAvaloniaApp(Type type)
    {
        var method = GetBuildAvaloniaApp(type)
                     ?? throw new InvalidOperationException($"Entry type '{type.FullName}' does not expose BuildAvaloniaApp.");

        return method.Invoke(null, null) as AppBuilder
               ?? throw new InvalidOperationException($"Entry type '{type.FullName}' returned null from BuildAvaloniaApp.");
    }

    private static MethodInfo? GetBuildAvaloniaApp(Type type)
        => type.GetMethod(
            "BuildAvaloniaApp",
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static,
            binder: null,
            types: [],
            modifiers: null);

    private static AppBuilder ConfigureApplication(Type applicationType)
    {
        var configure = typeof(AppBuilder)
            .GetMethods(BindingFlags.Public | BindingFlags.Static)
            .Single(method => method.Name == nameof(AppBuilder.Configure) &&
                              method.IsGenericMethodDefinition &&
                              method.GetParameters().Length == 0);

        return (AppBuilder)configure.MakeGenericMethod(applicationType).Invoke(null, null)!;
    }
}
