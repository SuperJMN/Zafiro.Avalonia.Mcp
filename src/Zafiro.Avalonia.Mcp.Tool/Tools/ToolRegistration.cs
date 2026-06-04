using Microsoft.Extensions.DependencyInjection;

namespace Zafiro.Avalonia.Mcp.Tool.Tools;

internal static class ToolRegistration
{
    public static IReadOnlyList<Type> RegisteredToolTypes { get; } =
    [
        typeof(ConnectionTools),
        typeof(AdbTools),
        typeof(AppLaunchTools),
        typeof(PreviewTools),
        typeof(TreeTools),
        typeof(PropertyTools),
        typeof(DataTools),
        typeof(DiagnosticTools),
        typeof(InputTools),
        typeof(InteractionTools),
        typeof(CompositeTools),
        typeof(CaptureTools),
        typeof(EventTools),
        typeof(ResourceTools),
        typeof(InstructionTools),
    ];

    public static IMcpServerBuilder WithRegisteredTools(this IMcpServerBuilder builder)
        => builder
            .WithTools<ConnectionTools>()
            .WithTools<AdbTools>()
            .WithTools<AppLaunchTools>()
            .WithTools<PreviewTools>()
            .WithTools<TreeTools>()
            .WithTools<PropertyTools>()
            .WithTools<DataTools>()
            .WithTools<DiagnosticTools>()
            .WithTools<InputTools>()
            .WithTools<InteractionTools>()
            .WithTools<CompositeTools>()
            .WithTools<CaptureTools>()
            .WithTools<EventTools>()
            .WithTools<ResourceTools>()
            .WithTools<InstructionTools>();
}
