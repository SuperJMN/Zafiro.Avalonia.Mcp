using Zafiro.Avalonia.Mcp.Tool.Connection;
using Zafiro.Avalonia.Mcp.Tool.Tools;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ModelContextProtocol;

var builder = Host.CreateApplicationBuilder(args);

// Stdio transport uses stdout for JSON-RPC — keep it clean
builder.Logging.ClearProviders();
builder.Logging.AddConsole(options => options.LogToStandardErrorThreshold = LogLevel.Trace);

builder.Services.AddSingleton<ConnectionPool>();

builder.Services
    .AddMcpServer(options =>
    {
        options.ServerInfo = new()
        {
            Name = "zafiro-avalonia-mcp",
            Version = "2.1.0"
        };
    })
    .WithStdioServerTransport()
    .WithTools<ConnectionTools>()
    .WithTools<AdbTools>()
    .WithTools<TreeTools>()
    .WithTools<PropertyTools>()
    .WithTools<InputTools>()
    .WithTools<InteractionTools>()
    .WithTools<CaptureTools>()
    .WithTools<ResourceTools>()
    .WithTools<InstructionTools>();

await builder.Build().RunAsync();
