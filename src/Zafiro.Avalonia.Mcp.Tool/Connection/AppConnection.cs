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
    private readonly CancellationTokenSource _lifetimeCts = new();
    private readonly Func<CancellationToken, Task<string?>>? _connectionFailureDetailsProvider;
    private volatile bool _isConnected;
    private int _disposed;

    public AppConnection(DiscoveryInfo info) : this(info, null)
    {
    }

    internal AppConnection(DiscoveryInfo info, Func<CancellationToken, Task<string?>>? connectionFailureDetailsProvider)
    {
        _info = info;
        _connectionFailureDetailsProvider = connectionFailureDetailsProvider;
    }

    public int Pid => _info.Pid;
    public string ProcessName => _info.ProcessName;
    public bool IsConnected => _isConnected && _stream is { CanRead: true, CanWrite: true };

    public async Task ConnectAsync(CancellationToken ct = default)
    {
        DisposeConnectionResources();
        try
        {
            _stream = await TransportClient.ConnectAsync(_info.Endpoint, _info.PipeName, TimeSpan.FromSeconds(5), ct);
            _reader = new StreamReader(_stream, Encoding.UTF8);
            _writer = new StreamWriter(_stream, Encoding.UTF8) { AutoFlush = true };
            _isConnected = true;
        }
        catch
        {
            DisposeConnectionResources();
            throw;
        }
    }

    public async Task<JsonElement?> SendAsync(string method, object? parameters = null, CancellationToken ct = default)
        => await SendAsync(method, parameters, TimeSpan.FromSeconds(30), ct);

    public async Task<JsonElement?> SendAsync(string method, object? parameters, TimeSpan timeout, CancellationToken ct = default)
    {
        using var waitCts = CancellationTokenSource.CreateLinkedTokenSource(ct, _lifetimeCts.Token);
        await _sendLock.WaitAsync(waitCts.Token);
        try
        {
            var writer = _writer ?? throw new InvalidOperationException("Not connected");
            var reader = _reader ?? throw new InvalidOperationException("Not connected");

            var id = Interlocked.Increment(ref _requestId).ToString();

            var request = new DiagnosticRequest
            {
                Method = method,
                Id = id,
                Params = parameters is not null ? ProtocolSerializer.ToElement(parameters) : null
            };

            var json = ProtocolSerializer.Serialize(request);
            try
            {
                using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct, _lifetimeCts.Token);
                timeoutCts.CancelAfter(timeout);

                await writer.WriteLineAsync(json.AsMemory(), timeoutCts.Token);

                var responseLine = await reader.ReadLineAsync(timeoutCts.Token);
                if (responseLine is null)
                    throw new IOException("Connection closed");

                var response = ProtocolSerializer.Deserialize<DiagnosticResponse>(responseLine);
                if (response?.Error is not null)
                    throw new McpRemoteException(response.Error, response.ErrorInfo);

                return response?.Result;
            }
            catch (OperationCanceledException)
            {
                DisposeConnectionResources();
                throw;
            }
            catch (IOException)
            {
                DisposeConnectionResources();
                throw;
            }
            catch (ObjectDisposedException)
            {
                DisposeConnectionResources();
                throw;
            }
        }
        finally
        {
            _sendLock.Release();
        }
    }

    internal Task<string?> GetConnectionFailureDetails(CancellationToken ct) =>
        _connectionFailureDetailsProvider?.Invoke(ct) ?? Task.FromResult<string?>(null);

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return;
        }

        _lifetimeCts.Cancel();
        DisposeConnectionResources();
    }

    private void DisposeConnectionResources()
    {
        DisposeConnectionResource(_writer);
        DisposeConnectionResource(_reader);
        DisposeConnectionResource(_stream);
        _writer = null;
        _reader = null;
        _stream = null;
        _isConnected = false;
    }

    private static void DisposeConnectionResource(IDisposable? disposable)
    {
        if (disposable is null)
        {
            return;
        }

        try
        {
            disposable.Dispose();
        }
        catch (IOException)
        {
        }
        catch (ObjectDisposedException)
        {
        }
    }
}
