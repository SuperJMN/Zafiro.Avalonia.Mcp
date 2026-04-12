using AvaloniaMcp.Server.Connection;
using AvaloniaMcp.Server.Tools;
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
            Name = "avalonia-mcp",
            Version = "1.2.0"
        };
    })
    .WithStdioServerTransport()
    .WithTools<ConnectionTools>()
    .WithTools<TreeTools>()
    .WithTools<PropertyTools>()
    .WithTools<InputTools>()
    .WithTools<InteractionTools>()
    .WithTools<CaptureTools>()
    .WithTools<ResourceTools>()
    .WithTools<InstructionTools>();

await builder.Build().RunAsync();
