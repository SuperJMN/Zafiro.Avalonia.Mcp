using System.IO.Pipes;
using System.Net.Sockets;

namespace Zafiro.Avalonia.Mcp.Tool.Connection;

/// <summary>
/// Resolves a <see cref="DiscoveryInfo"/>-style endpoint into a connected duplex <see cref="Stream"/>.
/// Endpoint forms supported:
///   - "pipe:&lt;name&gt;"        → Named pipe client (desktop default)
///   - "tcp:host:port"           → TCP client (Android via adb forward, remote dev)
/// If <paramref name="endpoint"/> is null, falls back to the legacy <paramref name="pipeName"/>
/// argument so that v2.1 tool clients can still connect to v2.0 hosts.
/// </summary>
internal static class TransportClient
{
    public static async Task<Stream> ConnectAsync(string? endpoint, string? pipeName, TimeSpan timeout, CancellationToken ct)
    {
        var (kind, target) = ParseEndpoint(endpoint, pipeName);

        if (kind == "tcp")
        {
            var (host, port) = ParseHostPort(target);
            var client = new TcpClient();
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeoutCts.CancelAfter(timeout);
            await client.ConnectAsync(host, port, timeoutCts.Token);
            return new TcpClientStream(client);
        }

        // Named pipe (default).
        var pipe = new NamedPipeClientStream(".", target, PipeDirection.InOut, PipeOptions.Asynchronous);
        await pipe.ConnectAsync((int)timeout.TotalMilliseconds, ct);
        return pipe;
    }

    private static (string kind, string target) ParseEndpoint(string? endpoint, string? pipeName)
    {
        if (!string.IsNullOrEmpty(endpoint))
        {
            var idx = endpoint.IndexOf(':');
            if (idx > 0)
            {
                var kind = endpoint[..idx];
                var rest = endpoint[(idx + 1)..];
                if (kind is "pipe" or "tcp")
                    return (kind, rest);
            }

            // Endpoint without explicit kind: treat as TCP host:port if it looks like one, else pipe.
            if (endpoint.Contains(':'))
                return ("tcp", endpoint);
            return ("pipe", endpoint);
        }

        if (!string.IsNullOrEmpty(pipeName))
            return ("pipe", pipeName);

        throw new InvalidOperationException("DiscoveryInfo has neither endpoint nor pipeName.");
    }

    private static (string host, int port) ParseHostPort(string target)
    {
        var idx = target.LastIndexOf(':');
        if (idx <= 0) throw new FormatException($"Expected host:port in TCP endpoint, got '{target}'.");
        var host = target[..idx];
        if (!int.TryParse(target[(idx + 1)..], out var port))
            throw new FormatException($"Invalid port in TCP endpoint '{target}'.");
        return (host, port);
    }

    /// <summary>
    /// Wraps a <see cref="TcpClient"/> so the underlying client is disposed when the stream is.
    /// <see cref="TcpClient.GetStream"/> normally outlives the client, which is the wrong ownership for our usage.
    /// </summary>
    private sealed class TcpClientStream : Stream
    {
        private readonly TcpClient _client;
        private readonly NetworkStream _inner;

        public TcpClientStream(TcpClient client)
        {
            _client = client;
            _inner = client.GetStream();
        }

        public override bool CanRead => _inner.CanRead;
        public override bool CanSeek => false;
        public override bool CanWrite => _inner.CanWrite;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
        public override void Flush() => _inner.Flush();
        public override Task FlushAsync(CancellationToken ct) => _inner.FlushAsync(ct);
        public override int Read(byte[] buffer, int offset, int count) => _inner.Read(buffer, offset, count);
        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken ct) => _inner.ReadAsync(buffer, offset, count, ct);
        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken ct = default) => _inner.ReadAsync(buffer, ct);
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => _inner.Write(buffer, offset, count);
        public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken ct) => _inner.WriteAsync(buffer, offset, count, ct);
        public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken ct = default) => _inner.WriteAsync(buffer, ct);

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                try { _inner.Dispose(); } catch { }
                try { _client.Dispose(); } catch { }
            }
            base.Dispose(disposing);
        }
    }
}
