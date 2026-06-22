using Zafiro.Avalonia.Mcp.Protocol.Messages;

namespace Zafiro.Avalonia.Mcp.Tool.Preview;

internal enum PreviewBackend
{
    Auto,
    Desktop,
    Headless,
}

internal static class PreviewBackendParser
{
    public static PreviewBackend Parse(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return PreviewBackend.Auto;
        }

        return value.Trim().ToLowerInvariant() switch
        {
            "auto" => PreviewBackend.Auto,
            "desktop" => PreviewBackend.Desktop,
            "headless" => PreviewBackend.Headless,
            _ => throw new PreviewValidationException(
                DiagnosticErrorCodes.InvalidParam,
                "backend must be one of: auto, desktop, headless.")
        };
    }

    public static string ToWireValue(this PreviewBackend backend) =>
        backend switch
        {
            PreviewBackend.Auto => "auto",
            PreviewBackend.Desktop => "desktop",
            PreviewBackend.Headless => "headless",
            _ => "auto",
        };
}

internal static class PreviewBackendResolver
{
    public static PreviewBackend Resolve(
        PreviewBackend requested,
        IDictionary<string, string?> environment,
        bool isLinux)
    {
        if (requested == PreviewBackend.Headless)
        {
            return PreviewBackend.Headless;
        }

        if (requested == PreviewBackend.Desktop)
        {
            PreviewGraphicalEnvironment.EnsureAvailable(environment, isLinux);
            return PreviewBackend.Desktop;
        }

        return isLinux && !PreviewGraphicalEnvironment.IsAvailable(environment)
            ? PreviewBackend.Headless
            : PreviewBackend.Desktop;
    }
}
