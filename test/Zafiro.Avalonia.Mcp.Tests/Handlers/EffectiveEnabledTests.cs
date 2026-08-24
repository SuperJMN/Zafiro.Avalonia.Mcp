using System.Text.Json;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;
using Xunit;
using Zafiro.Avalonia.Mcp.AppHost.Handlers;
using Zafiro.Avalonia.Mcp.AppHost.Selectors;
using Zafiro.Avalonia.Mcp.Protocol;
using Zafiro.Avalonia.Mcp.Protocol.Messages;

namespace Zafiro.Avalonia.Mcp.Tests.Handlers;

[Collection("Avalonia")]
public class EffectiveEnabledTests
{
    public EffectiveEnabledTests(AvaloniaTestFixture _)
    {
        NodeRegistry.Clear();
    }

    [Fact]
    public void ReadApis_ReportCommandAndAncestorDisabledButtonsAsDisabled()
    {
        var (window, commandDisabled, ancestorDisabled) = Dispatcher.UIThread.Invoke(() =>
        {
            var commandButton = new Button
            {
                Name = "CommandDisabled",
                Content = "Save",
                Command = new TestCommand(canExecute: false)
            };
            var ancestorButton = new Button { Name = "AncestorDisabled", Content = "Delete" };
            var disabledPanel = new StackPanel { IsEnabled = false, Children = { ancestorButton } };
            var root = new StackPanel { Children = { commandButton, disabledPanel } };
            var host = new Window
            {
                Width = 320,
                Height = 200,
                IsVisible = true,
                Content = root
            };

            host.ApplyTemplate();
            host.Measure(new Size(320, 200));
            host.Arrange(new Rect(0, 0, 320, 200));
            NodeRegistry.GetOrRegister(host);

            return (host, commandButton, ancestorButton);
        });

        try
        {
            var treeCommand = Dispatcher.UIThread.Invoke(() => NodeInfoBuilder.Create(commandDisabled));
            var treeAncestor = Dispatcher.UIThread.Invoke(() => NodeInfoBuilder.Create(ancestorDisabled));
            Assert.False(treeCommand.IsEnabled);
            Assert.False(treeAncestor.IsEnabled);

            var interactables = Serialize(Handle(new InteractablesHandler(), new { }));
            Assert.False(FindByName(interactables, "CommandDisabled").GetProperty("isEnabled").GetBoolean());
            Assert.False(FindByName(interactables, "AncestorDisabled").GetProperty("isEnabled").GetBoolean());

            var snapshot = Dispatcher.UIThread.Invoke(() =>
                Serialize(SnapshotHandler.BuildSnapshot(window, visibleOnly: false)));
            var elements = snapshot.GetProperty("elements");
            Assert.False(FindByName(elements, "CommandDisabled").GetProperty("isEnabled").GetBoolean());
            Assert.False(FindByName(elements, "AncestorDisabled").GetProperty("isEnabled").GetBoolean());
        }
        finally
        {
            Dispatcher.UIThread.Invoke(window.Close);
        }
    }

    [Fact]
    public void EnabledPseudoClasses_UseEffectiveEnabledState()
    {
        var (window, root, commandDisabled, ancestorDisabled) = Dispatcher.UIThread.Invoke(() =>
        {
            var commandButton = new Button
            {
                Name = "CommandDisabled",
                Command = new TestCommand(canExecute: false)
            };
            var ancestorButton = new Button { Name = "AncestorDisabled" };
            var disabledPanel = new StackPanel { IsEnabled = false, Children = { ancestorButton } };
            var root = new StackPanel { Children = { commandButton, disabledPanel } };
            var window = new Window { Width = 320, Height = 200, Content = root };
            window.ApplyTemplate();
            window.Measure(new Size(320, 200));
            window.Arrange(new Rect(0, 0, 320, 200));
            return (window, (Visual)root, commandButton, ancestorButton);
        });

        try
        {
            var disabled = Dispatcher.UIThread.Invoke(() => new SelectorEngine().Resolve("Button:disabled", root));
            var enabled = Dispatcher.UIThread.Invoke(() => new SelectorEngine().Resolve("Button:enabled", root));

            Assert.Contains(commandDisabled, disabled);
            Assert.Contains(ancestorDisabled, disabled);
            Assert.DoesNotContain(commandDisabled, enabled);
            Assert.DoesNotContain(ancestorDisabled, enabled);
        }
        finally
        {
            Dispatcher.UIThread.Invoke(window.Close);
        }
    }

    private static object Handle(IRequestHandler handler, object parameters)
    {
        var request = new DiagnosticRequest
        {
            Id = "test",
            Method = handler.Method,
            Params = JsonSerializer.SerializeToElement(parameters)
        };
        var task = handler.Handle(request);
        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(5);
        while (!task.IsCompleted && DateTime.UtcNow < deadline)
        {
            Dispatcher.UIThread.RunJobs();
            Thread.Sleep(1);
        }

        if (!task.IsCompleted)
            throw new TimeoutException($"{handler.GetType().Name} did not complete.");

        return task.GetAwaiter().GetResult();
    }

    private static JsonElement Serialize(object result) => JsonSerializer.SerializeToElement(result, new JsonSerializerOptions
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    });

    private static JsonElement FindByName(JsonElement elements, string name) =>
        elements.EnumerateArray().Single(element => element.GetProperty("name").GetString() == name);
}
