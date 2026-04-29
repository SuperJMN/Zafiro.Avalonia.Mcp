using System.IO.Pipes;
using System.Text;
using System.Text.Json;
using Zafiro.Avalonia.Mcp.Protocol;
using Zafiro.Avalonia.Mcp.Protocol.Messages;
using Zafiro.Avalonia.Mcp.Protocol.Models;

namespace Zafiro.Avalonia.Mcp.Tool.Connection;

public sealed class AppConnection : IDisposable
{
    private readonly DiscoveryInfo _info;
    private Stream? _stream;
    private StreamReader? _reader;
    private StreamWriter? _writer;
    private int _requestId;
    private readonly SemaphoreSlim _sendLock = new(1, 1);

    public AppConnection(DiscoveryInfo info)
    {
        _info = info;
    }

    public int Pid => _info.Pid;
    public string ProcessName => _info.ProcessName;
    public bool IsConnected => _stream is { CanRead: true, CanWrite: true };

    public async Task ConnectAsync(CancellationToken ct = default)
    {
        _stream = await TransportClient.ConnectAsync(_info.Endpoint, _info.PipeName, TimeSpan.FromSeconds(5), ct);
        _reader = new StreamReader(_stream, Encoding.UTF8);
        _writer = new StreamWriter(_stream, Encoding.UTF8) { AutoFlush = true };
    }

    public async Task<JsonElement?> SendAsync(string method, object? parameters = null, CancellationToken ct = default)
        => await SendAsync(method, parameters, TimeSpan.FromSeconds(30), ct);

    public async Task<JsonElement?> SendAsync(string method, object? parameters, TimeSpan timeout, CancellationToken ct = default)
    {
        if (_writer is null || _reader is null)
            throw new InvalidOperationException("Not connected");

        await _sendLock.WaitAsync(ct);
        try
        {
            var id = Interlocked.Increment(ref _requestId).ToString();

            var request = new DiagnosticRequest
            {
                Method = method,
                Id = id,
                Params = parameters is not null ? ProtocolSerializer.ToElement(parameters) : null
            };

            var json = ProtocolSerializer.Serialize(request);
            await _writer.WriteLineAsync(json.AsMemory(), ct);

            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeoutCts.CancelAfter(timeout);

            var responseLine = await _reader.ReadLineAsync(timeoutCts.Token);
            if (responseLine is null)
                throw new IOException("Connection closed");

            var response = ProtocolSerializer.Deserialize<DiagnosticResponse>(responseLine);
            if (response?.Error is not null)
                throw new McpRemoteException(response.Error, response.ErrorInfo);

            return response?.Result;
        }
        finally
        {
            _sendLock.Release();
        }
    }

    public void Dispose()
    {
        _writer?.Dispose();
        _reader?.Dispose();
        _stream?.Dispose();
        _sendLock.Dispose();
    }
}
