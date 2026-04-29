using System.Reflection;

namespace Zafiro.Avalonia.Mcp.AppHost.Discovery;

/// <summary>
/// Lightweight logger that prints to <c>android.util.Log</c> when running on Android (via reflection,
/// keeping the AppHost free of <c>Mono.Android</c> references) and to <see cref="Console"/> elsewhere.
/// </summary>
internal static class McpLog
{
    private const string Tag = "ZafiroMcp";

    private static readonly Lazy<MethodInfo?> InfoMethod = new(() => GetMethod("i"));
    private static readonly Lazy<MethodInfo?> ErrorMethod = new(() => GetMethod("e"));

    public static void Info(string message) => Write(InfoMethod.Value, message, isError: false);
    public static void Error(string message) => Write(ErrorMethod.Value, message, isError: true);

    private static void Write(MethodInfo? method, string message, bool isError)
    {
        if (method is not null)
        {
            try { method.Invoke(null, new object?[] { Tag, message }); return; }
            catch { /* fall through */ }
        }

        var line = $"[{Tag}] {message}";
        if (isError) Console.Error.WriteLine(line);
        else Console.WriteLine(line);
    }

    private static MethodInfo? GetMethod(string name)
    {
        try
        {
            var logType = Type.GetType("Android.Util.Log, Mono.Android", throwOnError: false);
            return logType?.GetMethod(name, BindingFlags.Public | BindingFlags.Static, types: new[] { typeof(string), typeof(string) });
        }
        catch
        {
            return null;
        }
    }
}
