namespace Zafiro.Avalonia.Mcp.Tool.Preview;

internal static class PreviewGraphicalEnvironment
{
    internal static readonly string[] VariableNames =
    [
        "DISPLAY",
        "WAYLAND_DISPLAY",
        "XAUTHORITY",
        "XDG_RUNTIME_DIR",
        "XDG_SESSION_TYPE",
        "XDG_CURRENT_DESKTOP",
        "DESKTOP_SESSION",
        "DBUS_SESSION_BUS_ADDRESS",
        "GDK_BACKEND",
    ];

    public static void Apply(IDictionary<string, string?> environment)
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        Apply(environment, Environment.ProcessId, ProcProcessEnvironmentReader.Instance);
    }

    internal static void Apply(
        IDictionary<string, string?> environment,
        int currentPid,
        IProcessEnvironmentReader reader)
    {
        var recovered = FindAncestorGraphicalEnvironment(currentPid, reader);
        if (recovered is null)
        {
            return;
        }

        foreach (var variableName in VariableNames)
        {
            if (HasValue(environment, variableName))
            {
                continue;
            }

            if (recovered.TryGetValue(variableName, out var value) && !string.IsNullOrWhiteSpace(value))
            {
                environment[variableName] = value;
            }
        }
    }

    private static IReadOnlyDictionary<string, string>? FindAncestorGraphicalEnvironment(
        int currentPid,
        IProcessEnvironmentReader reader)
    {
        var visited = new HashSet<int>();
        var pid = currentPid;

        for (var depth = 0; depth < 32; depth++)
        {
            var parentPid = reader.GetParentPid(pid);
            if (parentPid is null or <= 1 || !visited.Add(parentPid.Value))
            {
                return null;
            }

            var environment = reader.ReadEnvironment(parentPid.Value);
            if (HasDisplay(environment))
            {
                return environment;
            }

            pid = parentPid.Value;
        }

        return null;
    }

    private static bool HasDisplay(IReadOnlyDictionary<string, string> environment) =>
        HasValue(environment, "DISPLAY") || HasValue(environment, "WAYLAND_DISPLAY");

    private static bool HasValue(IReadOnlyDictionary<string, string> environment, string name) =>
        environment.TryGetValue(name, out var value) && !string.IsNullOrWhiteSpace(value);

    private static bool HasValue(IDictionary<string, string?> environment, string name) =>
        environment.TryGetValue(name, out var value) && !string.IsNullOrWhiteSpace(value);
}

internal interface IProcessEnvironmentReader
{
    int? GetParentPid(int pid);
    IReadOnlyDictionary<string, string> ReadEnvironment(int pid);
}

internal sealed class ProcProcessEnvironmentReader : IProcessEnvironmentReader
{
    public static readonly ProcProcessEnvironmentReader Instance = new();

    public int? GetParentPid(int pid)
    {
        try
        {
            var stat = File.ReadAllText($"/proc/{pid}/stat");
            var commandEnd = stat.LastIndexOf(')');
            if (commandEnd < 0 || commandEnd + 2 >= stat.Length)
            {
                return null;
            }

            var fields = stat[(commandEnd + 2)..]
                .Split(' ', StringSplitOptions.RemoveEmptyEntries);

            return fields.Length >= 2 && int.TryParse(fields[1], out var parentPid)
                ? parentPid
                : null;
        }
        catch
        {
            return null;
        }
    }

    public IReadOnlyDictionary<string, string> ReadEnvironment(int pid)
    {
        try
        {
            var bytes = File.ReadAllBytes($"/proc/{pid}/environ");
            var entries = System.Text.Encoding.UTF8.GetString(bytes)
                .Split('\0', StringSplitOptions.RemoveEmptyEntries);

            var result = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var entry in entries)
            {
                var separator = entry.IndexOf('=');
                if (separator <= 0)
                {
                    continue;
                }

                result[entry[..separator]] = entry[(separator + 1)..];
            }

            return result;
        }
        catch
        {
            return new Dictionary<string, string>(StringComparer.Ordinal);
        }
    }
}
