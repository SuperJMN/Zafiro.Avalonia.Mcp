using Xunit;
using Zafiro.Avalonia.Mcp.Protocol.Messages;
using Zafiro.Avalonia.Mcp.Tool.Preview;

namespace Zafiro.Avalonia.Mcp.Tests.Preview;

public sealed class PreviewGraphicalEnvironmentTests
{
    [Fact]
    public void Apply_RecoversGraphicalVariablesFromAncestor_WhenToolEnvironmentIsSanitized()
    {
        var environment = new Dictionary<string, string?>(StringComparer.Ordinal);
        var reader = new FakeProcessEnvironmentReader(
            parents: new Dictionary<int, int>
            {
                [30] = 20,
                [20] = 10,
            },
            environments: new Dictionary<int, IReadOnlyDictionary<string, string>>
            {
                [20] = new Dictionary<string, string>(StringComparer.Ordinal),
                [10] = new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["DISPLAY"] = ":1",
                    ["WAYLAND_DISPLAY"] = "wayland-1",
                    ["XDG_RUNTIME_DIR"] = "/run/user/1000",
                    ["XDG_SESSION_TYPE"] = "wayland",
                    ["XDG_CURRENT_DESKTOP"] = "COSMIC",
                    ["DBUS_SESSION_BUS_ADDRESS"] = "unix:path=/run/user/1000/bus",
                },
            });

        PreviewGraphicalEnvironment.Apply(environment, currentPid: 30, reader);

        Assert.Equal(":1", environment["DISPLAY"]);
        Assert.Equal("wayland-1", environment["WAYLAND_DISPLAY"]);
        Assert.Equal("/run/user/1000", environment["XDG_RUNTIME_DIR"]);
        Assert.Equal("wayland", environment["XDG_SESSION_TYPE"]);
        Assert.Equal("COSMIC", environment["XDG_CURRENT_DESKTOP"]);
        Assert.Equal("unix:path=/run/user/1000/bus", environment["DBUS_SESSION_BUS_ADDRESS"]);
    }

    [Fact]
    public void Apply_PreservesExistingGraphicalVariables()
    {
        var environment = new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            ["DISPLAY"] = ":99",
        };
        var reader = new FakeProcessEnvironmentReader(
            parents: new Dictionary<int, int>
            {
                [30] = 10,
            },
            environments: new Dictionary<int, IReadOnlyDictionary<string, string>>
            {
                [10] = new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["DISPLAY"] = ":1",
                    ["XDG_RUNTIME_DIR"] = "/run/user/1000",
                },
            });

        PreviewGraphicalEnvironment.Apply(environment, currentPid: 30, reader);

        Assert.Equal(":99", environment["DISPLAY"]);
        Assert.Equal("/run/user/1000", environment["XDG_RUNTIME_DIR"]);
    }

    [Fact]
    public void EnsureAvailable_ThrowsDisplayUnavailable_WhenLinuxEnvironmentHasNoDisplay()
    {
        var environment = new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            ["XDG_SESSION_TYPE"] = "wayland",
            ["XDG_RUNTIME_DIR"] = "/run/user/1000",
        };

        var ex = Assert.Throws<PreviewValidationException>(() =>
            PreviewGraphicalEnvironment.EnsureAvailable(environment, isLinux: true));

        Assert.Equal(DiagnosticErrorCodes.DisplayUnavailable, ex.Code);
        Assert.Contains("graphical session", ex.Message);
        Assert.Contains("DISPLAY", ex.Message);
        var suggested = Assert.IsType<string>(ex.Suggested);
        Assert.Contains("XDG_RUNTIME_DIR", suggested);
        var details = Assert.IsType<PreviewDisplayEnvironmentDetails>(ex.Details);
        Assert.Null(details.Display);
        Assert.Null(details.WaylandDisplay);
        Assert.Equal("wayland", details.XdgSessionType);
        Assert.True(details.XdgRuntimeDirSet);
    }

    [Fact]
    public void EnsureAvailable_AllowsDisplayVariable()
    {
        var environment = new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            ["DISPLAY"] = ":1",
        };

        PreviewGraphicalEnvironment.EnsureAvailable(environment, isLinux: true);
    }

    private sealed class FakeProcessEnvironmentReader(
        IReadOnlyDictionary<int, int> parents,
        IReadOnlyDictionary<int, IReadOnlyDictionary<string, string>> environments)
        : IProcessEnvironmentReader
    {
        public int? GetParentPid(int pid) =>
            parents.TryGetValue(pid, out var parentPid) ? parentPid : null;

        public IReadOnlyDictionary<string, string> ReadEnvironment(int pid) =>
            environments.TryGetValue(pid, out var environment)
                ? environment
                : new Dictionary<string, string>(StringComparer.Ordinal);
    }
}
