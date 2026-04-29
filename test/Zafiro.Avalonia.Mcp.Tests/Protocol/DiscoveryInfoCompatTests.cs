using Xunit;
using Zafiro.Avalonia.Mcp.Protocol;
using Zafiro.Avalonia.Mcp.Protocol.Models;

namespace Zafiro.Avalonia.Mcp.Tests.Protocol;

public class DiscoveryInfoCompatTests
{
    [Fact]
    public void Deserialize_V20Json_Without_Endpoint_Works()
    {
        // Shape published by hosts before v2.1 — only pipeName, no transport/endpoint.
        var json = """
        {
          "pid": 1234,
          "pipeName": "zafiro-avalonia-mcp-1234",
          "processName": "MyApp",
          "startTime": "2024-01-01T00:00:00+00:00",
          "protocolVersion": "1.0.0"
        }
        """;

        var info = ProtocolSerializer.Deserialize<DiscoveryInfo>(json);

        Assert.NotNull(info);
        Assert.Equal(1234, info!.Pid);
        Assert.Equal("zafiro-avalonia-mcp-1234", info.PipeName);
        Assert.Null(info.Transport);
        Assert.Null(info.Endpoint);
        Assert.Null(info.PackageId);
    }

    [Fact]
    public void Deserialize_V21Json_With_TcpEndpoint_Works()
    {
        var json = """
        {
          "pid": 5555,
          "pipeName": "zafiro-avalonia-mcp-5555",
          "processName": "MyApp.Android",
          "startTime": "2024-01-01T00:00:00+00:00",
          "protocolVersion": "1.0.0",
          "transport": "tcp",
          "endpoint": "127.0.0.1:54321",
          "packageId": "com.example.myapp"
        }
        """;

        var info = ProtocolSerializer.Deserialize<DiscoveryInfo>(json);

        Assert.NotNull(info);
        Assert.Equal("tcp", info!.Transport);
        Assert.Equal("127.0.0.1:54321", info.Endpoint);
        Assert.Equal("com.example.myapp", info.PackageId);
    }

    [Fact]
    public void Serialize_V21Info_With_NullOptionals_Omits_Them()
    {
        var info = new DiscoveryInfo
        {
            Pid = 1,
            PipeName = "p",
            ProcessName = "n",
            StartTime = DateTimeOffset.UnixEpoch
        };

        var json = ProtocolSerializer.Serialize(info);

        Assert.DoesNotContain("\"transport\"", json);
        Assert.DoesNotContain("\"endpoint\"", json);
        Assert.DoesNotContain("\"packageId\"", json);
    }

    [Fact]
    public void Serialize_V21Info_With_Endpoint_Includes_It()
    {
        var info = new DiscoveryInfo
        {
            Pid = 1,
            PipeName = "p",
            ProcessName = "n",
            StartTime = DateTimeOffset.UnixEpoch,
            Transport = "tcp",
            Endpoint = "127.0.0.1:9999"
        };

        var json = ProtocolSerializer.Serialize(info);

        Assert.Contains("\"transport\":\"tcp\"", json);
        Assert.Contains("\"endpoint\":\"127.0.0.1:9999\"", json);
    }
}
