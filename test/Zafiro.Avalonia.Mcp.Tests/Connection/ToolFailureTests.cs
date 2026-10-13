using System.Net;
using System.Net.Sockets;
using System.Text;
using ModelContextProtocol.Protocol;
using Xunit;
using Zafiro.Avalonia.Mcp.Protocol;
using Zafiro.Avalonia.Mcp.Protocol.Messages;
using Zafiro.Avalonia.Mcp.Protocol.Models;
using Zafiro.Avalonia.Mcp.Tool.Connection;
using Zafiro.Avalonia.Mcp.Tool.Tools;

namespace Zafiro.Avalonia.Mcp.Tests.Connection;

public sealed class ToolFailureTests
{
    [Fact]
    public async Task InvokeAsync_RemoteFailure_MarksCallResultAsError()
    {
        var error = new DiagnosticError("Selector '#Missing' matched nothing.", DiagnosticErrorCodes.NoMatch, "Check the selector.");
        await using var app = await FakeApp.Start(id => DiagnosticResponse.Failure(id, error));
        using var conn = await app.Connect();

        var result = await ToolFailure.Track(async () => TextResult(await conn.InvokeAsync(ProtocolMethods.SetProperty)));

        Assert.True(result.IsError);
        var text = Assert.IsType<TextContentBlock>(Assert.Single(result.Content)).Text;
        Assert.Contains("[NO_MATCH]", text);
        Assert.Contains("Hint: Check the selector.", text);
    }

    [Fact]
    public async Task InvokeRichAsync_RemoteFailure_MarksCallResultAsError()
    {
        var error = new DiagnosticError("boom", DiagnosticErrorCodes.Internal);
        await using var app = await FakeApp.Start(id => DiagnosticResponse.Failure(id, error));
        using var conn = await app.Connect();

        var result = await ToolFailure.Track(async () => new CallToolResult
        {
            Content = [.. await conn.InvokeRichAsync(ProtocolMethods.Screenshot, null, _ => [])],
        });

        Assert.True(result.IsError);
    }

    [Fact]
    public async Task InvokeAsync_Success_LeavesCallResultUnflagged()
    {
        await using var app = await FakeApp.Start(id => DiagnosticResponse.Success(id, ProtocolSerializer.ToElement(new { ok = true })));
        using var conn = await app.Connect();

        var result = await ToolFailure.Track(async () => TextResult(await conn.InvokeAsync(ProtocolMethods.Ping)));

        Assert.NotEqual(true, result.IsError);
    }

    [Fact]
    public async Task InvokeAsync_LocalFailure_MarksCallResultAsError()
    {
        using var conn = new AppConnection(new DiscoveryInfo { PipeName = "unused", ProcessName = "Test" });

        var result = await ToolFailure.Track(async () => TextResult(await conn.InvokeAsync(ProtocolMethods.Ping)));

        Assert.True(result.IsError);
    }

    private static CallToolResult TextResult(string text) => new() { Content = [new TextContentBlock { Text = text }] };

    private sealed class FakeApp : IAsyncDisposable
    {
        private readonly TcpListener _listener;
        private readonly Task _serve;

        private FakeApp(TcpListener listener, Func<string, DiagnosticResponse> respond)
        {
            _listener = listener;
            _serve = Serve(respond);
        }

        public static Task<FakeApp> Start(Func<string, DiagnosticResponse> respond)
        {
            var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            return Task.FromResult(new FakeApp(listener, respond));
        }

        public async Task<AppConnection> Connect()
        {
            var port = ((IPEndPoint)_listener.LocalEndpoint).Port;
            var conn = new AppConnection(new DiscoveryInfo
            {
                PipeName = "unused",
                ProcessName = "FakeApp",
                Transport = "tcp",
                Endpoint = $"tcp:127.0.0.1:{port}",
            });
            await conn.ConnectAsync();
            return conn;
        }

        private async Task Serve(Func<string, DiagnosticResponse> respond)
        {
            using var client = await _listener.AcceptTcpClientAsync();
            await using var stream = client.GetStream();
            using var reader = new StreamReader(stream, Encoding.UTF8);
            await using var writer = new StreamWriter(stream, Encoding.UTF8) { AutoFlush = true };
            var line = await reader.ReadLineAsync();
            var request = ProtocolSerializer.Deserialize<DiagnosticRequest>(line!);
            await writer.WriteLineAsync(ProtocolSerializer.Serialize(respond(request!.Id)));
        }

        public async ValueTask DisposeAsync()
        {
            try
            {
                await _serve.WaitAsync(TimeSpan.FromSeconds(5));
            }
            finally
            {
                _listener.Stop();
            }
        }
    }
}
