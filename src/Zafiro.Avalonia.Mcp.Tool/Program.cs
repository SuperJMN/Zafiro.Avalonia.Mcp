using Zafiro.Avalonia.Mcp.Tool.Connection;
using Zafiro.Avalonia.Mcp.Tool.Preview;
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
builder.Services.AddSingleton<PreviewProcessManager>();
builder.Services.AddSingleton<IProcessRunner, DotnetProcessRunner>();
builder.Services.AddSingleton<PreviewTargetResolver>();

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
    .WithRegisteredTools();

await builder.Build().RunAsync();
