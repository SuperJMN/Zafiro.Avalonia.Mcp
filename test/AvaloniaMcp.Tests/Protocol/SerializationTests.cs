using System.Text.Json;
using AvaloniaMcp.Protocol;
using AvaloniaMcp.Protocol.Messages;
using AvaloniaMcp.Protocol.Models;
using Xunit;

namespace AvaloniaMcp.Tests.Protocol;

public class SerializationTests
{
    [Fact]
    public void NodeInfo_AllFields_RoundTrips()
    {
        var node = new NodeInfo
        {
            NodeId = 42,
            Type = "Button",
            Name = "okButton",
            Bounds = new BoundsInfo { X = 10, Y = 20, Width = 100, Height = 50 },
            IsVisible = true,
            Text = "OK",
            IsEnabled = true,
            IsFocused = false,
            IsInteractive = true,
            AutomationId = "btn-ok",
            Role = "button",
            ClassName = "primary",
            ParentId = 1,
            Children = new List<NodeInfo>
            {
                new() { NodeId = 43, Type = "TextBlock" }
            }
        };

        var json = ProtocolSerializer.Serialize(node);
        var result = ProtocolSerializer.Deserialize<NodeInfo>(json);

        Assert.NotNull(result);
        Assert.Equal(42, result.NodeId);
        Assert.Equal("Button", result.Type);
        Assert.Equal("okButton", result.Name);
        Assert.NotNull(result.Bounds);
        Assert.Equal(10, result.Bounds.X);
        Assert.Equal(20, result.Bounds.Y);
        Assert.Equal(100, result.Bounds.Width);
        Assert.Equal(50, result.Bounds.Height);
        Assert.True(result.IsVisible);
        Assert.Equal("OK", result.Text);
        Assert.True(result.IsEnabled);
        Assert.False(result.IsFocused);
        Assert.True(result.IsInteractive);
        Assert.Equal("btn-ok", result.AutomationId);
        Assert.Equal("button", result.Role);
        Assert.Equal("primary", result.ClassName);
        Assert.Equal(1, result.ParentId);
        Assert.NotNull(result.Children);
        Assert.Single(result.Children);
        Assert.Equal(43, result.Children[0].NodeId);
    }

    [Fact]
    public void NodeInfo_NullOptionalFields_OmittedFromJson()
    {
        var node = new NodeInfo
        {
            NodeId = 1,
            Type = "Panel"
        };

        var json = ProtocolSerializer.Serialize(node);

        Assert.Contains("\"nodeId\"", json);
        Assert.Contains("\"type\"", json);
        Assert.DoesNotContain("\"name\"", json);
        Assert.DoesNotContain("\"bounds\"", json);
        Assert.DoesNotContain("\"text\"", json);
        Assert.DoesNotContain("\"isEnabled\"", json);
        Assert.DoesNotContain("\"isFocused\"", json);
        Assert.DoesNotContain("\"isInteractive\"", json);
        Assert.DoesNotContain("\"automationId\"", json);
        Assert.DoesNotContain("\"role\"", json);
        Assert.DoesNotContain("\"className\"", json);
        Assert.DoesNotContain("\"parentId\"", json);
        Assert.DoesNotContain("\"children\"", json);
    }

    [Fact]
    public void PropertyInfo_RoundTrips()
    {
        var prop = new PropertyInfo
        {
            Name = "Width",
            Type = "Double",
            Value = "200",
            Priority = "LocalValue"
        };

        var json = ProtocolSerializer.Serialize(prop);
        var result = ProtocolSerializer.Deserialize<PropertyInfo>(json);

        Assert.NotNull(result);
        Assert.Equal("Width", result.Name);
        Assert.Equal("Double", result.Type);
        Assert.Equal("200", result.Value);
        Assert.Equal("LocalValue", result.Priority);
    }

    [Fact]
    public void DiscoveryInfo_RoundTrips()
    {
        var time = DateTimeOffset.UtcNow;
        var info = new DiscoveryInfo
        {
            Pid = 1234,
            PipeName = "test-pipe",
            ProcessName = "TestApp",
            StartTime = time,
            ProtocolVersion = "1.0.0"
        };

        var json = ProtocolSerializer.Serialize(info);
        var result = ProtocolSerializer.Deserialize<DiscoveryInfo>(json);

        Assert.NotNull(result);
        Assert.Equal(1234, result.Pid);
        Assert.Equal("test-pipe", result.PipeName);
        Assert.Equal("TestApp", result.ProcessName);
        Assert.Equal(time, result.StartTime);
        Assert.Equal("1.0.0", result.ProtocolVersion);
    }

    [Fact]
    public void DiagnosticRequest_RoundTrips()
    {
        var request = new DiagnosticRequest
        {
            Method = "get_tree",
            Id = "req-1",
            Params = ProtocolSerializer.ToElement(new { nodeId = 5, depth = 3 })
        };

        var json = ProtocolSerializer.Serialize(request);
        var result = ProtocolSerializer.Deserialize<DiagnosticRequest>(json);

        Assert.NotNull(result);
        Assert.Equal("get_tree", result.Method);
        Assert.Equal("req-1", result.Id);
        Assert.NotNull(result.Params);
        Assert.Equal(5, result.Params.Value.GetProperty("nodeId").GetInt32());
        Assert.Equal(3, result.Params.Value.GetProperty("depth").GetInt32());
    }

    [Fact]
    public void DiagnosticResponse_RoundTrips()
    {
        var element = ProtocolSerializer.ToElement(new { ok = true });
        var response = new DiagnosticResponse
        {
            Id = "resp-1",
            Result = element
        };

        var json = ProtocolSerializer.Serialize(response);
        var result = ProtocolSerializer.Deserialize<DiagnosticResponse>(json);

        Assert.NotNull(result);
        Assert.Equal("resp-1", result.Id);
        Assert.NotNull(result.Result);
        Assert.True(result.Result.Value.GetProperty("ok").GetBoolean());
        Assert.Null(result.Error);
    }

    [Fact]
    public void InteractableInfo_RoundTrips()
    {
        var info = new InteractableInfo
        {
            NodeId = 10,
            Type = "TextBox",
            Role = "textbox",
            Text = "Hello",
            Name = "inputField",
            AutomationId = "txt-input",
            IsEnabled = true,
            IsFocused = false,
            Value = "sample"
        };

        var json = ProtocolSerializer.Serialize(info);
        var result = ProtocolSerializer.Deserialize<InteractableInfo>(json);

        Assert.NotNull(result);
        Assert.Equal(10, result.NodeId);
        Assert.Equal("TextBox", result.Type);
        Assert.Equal("textbox", result.Role);
        Assert.Equal("Hello", result.Text);
        Assert.Equal("inputField", result.Name);
        Assert.Equal("txt-input", result.AutomationId);
        Assert.True(result.IsEnabled);
        Assert.False(result.IsFocused);
        Assert.Equal("sample", result.Value);
    }

    [Fact]
    public void DiagnosticResponse_Success_SetsResultAndNullError()
    {
        var element = ProtocolSerializer.ToElement(new { data = 123 });
        var response = DiagnosticResponse.Success("s-1", element);

        Assert.Equal("s-1", response.Id);
        Assert.NotNull(response.Result);
        Assert.Equal(123, response.Result.Value.GetProperty("data").GetInt32());
        Assert.Null(response.Error);
    }

    [Fact]
    public void DiagnosticResponse_Failure_SetsErrorAndNullResult()
    {
        var response = DiagnosticResponse.Failure("f-1", "Something went wrong");

        Assert.Equal("f-1", response.Id);
        Assert.Null(response.Result);
        Assert.Equal("Something went wrong", response.Error);
    }
}
