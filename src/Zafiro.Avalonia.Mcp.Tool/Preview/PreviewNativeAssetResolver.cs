using System.Runtime.InteropServices;

namespace Zafiro.Avalonia.Mcp.Tool.Preview;

internal static class PreviewNativeAssetResolver
{
    public static string? FindNativeLibraryInAppBase(string libraryName, string baseDirectory, string currentRuntimeIdentifier)
    {
        var runtimesDirectory = Path.Combine(baseDirectory, "runtimes");
        if (!Directory.Exists(runtimesDirectory))
        {
            return null;
        }

        var availableRids = Directory
            .EnumerateDirectories(runtimesDirectory)
            .Select(Path.GetFileName)
            .OfType<string>();

        foreach (var rid in CandidateRuntimeIdentifiers(currentRuntimeIdentifier, availableRids))
        {
            var nativeDirectory = Path.Combine(runtimesDirectory, rid, "native");
            if (!Directory.Exists(nativeDirectory))
            {
                continue;
            }

            foreach (var name in CandidateLibraryNames(libraryName, rid))
            {
                var path = Path.Combine(nativeDirectory, name);
                if (File.Exists(path))
                {
                    return path;
                }
            }
        }

        return null;
    }

    public static string? FindNativeLibraryInAppBase(string libraryName)
        => FindNativeLibraryInAppBase(libraryName, AppContext.BaseDirectory, RuntimeInformation.RuntimeIdentifier);

    internal static IReadOnlyList<string> CandidateRuntimeIdentifiers(string currentRuntimeIdentifier, IEnumerable<string> availableRids)
    {
        var currentRid = string.IsNullOrWhiteSpace(currentRuntimeIdentifier)
            ? RuntimeInformation.RuntimeIdentifier
            : currentRuntimeIdentifier.Trim();

        return availableRids
            .Where(rid => !string.IsNullOrWhiteSpace(rid))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(rid => CandidateRank(currentRid, rid), Comparer<int>.Default)
            .ThenBy(rid => rid, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static IEnumerable<string> CandidateLibraryNames(string libraryName, string rid)
    {
        yield return libraryName;

        var platformName = NativeLibraryName(libraryName, rid);
        if (!string.Equals(platformName, libraryName, StringComparison.Ordinal))
        {
            yield return platformName;
        }
    }

    private static int CandidateRank(string currentRid, string candidateRid)
    {
        if (string.Equals(currentRid, candidateRid, StringComparison.OrdinalIgnoreCase))
        {
            return 0;
        }

        var currentFamily = OsFamily(currentRid);
        var candidateFamily = OsFamily(candidateRid);
        if (!string.Equals(currentFamily, candidateFamily, StringComparison.OrdinalIgnoreCase))
        {
            return 3;
        }

        return string.Equals(Architecture(currentRid), Architecture(candidateRid), StringComparison.OrdinalIgnoreCase)
            ? 1
            : 2;
    }

    private static string NativeLibraryName(string libraryName, string rid)
    {
        var family = OsFamily(rid);
        if (string.Equals(family, "win", StringComparison.OrdinalIgnoreCase))
        {
            return libraryName.EndsWith(".dll", StringComparison.OrdinalIgnoreCase) ? libraryName : $"{libraryName}.dll";
        }

        if (string.Equals(family, "osx", StringComparison.OrdinalIgnoreCase))
        {
            var name = libraryName.StartsWith("lib", StringComparison.Ordinal) ? libraryName : $"lib{libraryName}";
            return name.EndsWith(".dylib", StringComparison.OrdinalIgnoreCase) ? name : $"{name}.dylib";
        }

        var linuxName = libraryName.StartsWith("lib", StringComparison.Ordinal) ? libraryName : $"lib{libraryName}";
        return linuxName.EndsWith(".so", StringComparison.OrdinalIgnoreCase) ? linuxName : $"{linuxName}.so";
    }

    private static string OsFamily(string rid)
    {
        if (rid.StartsWith("win", StringComparison.OrdinalIgnoreCase))
        {
            return "win";
        }

        if (rid.StartsWith("osx", StringComparison.OrdinalIgnoreCase))
        {
            return "osx";
        }

        if (rid.StartsWith("linux", StringComparison.OrdinalIgnoreCase))
        {
            return "linux";
        }

        var separator = rid.IndexOf('-');
        return separator < 0 ? rid : rid[..separator];
    }

    private static string Architecture(string rid)
    {
        var separator = rid.LastIndexOf('-');
        return separator < 0 ? string.Empty : rid[(separator + 1)..];
    }
}
