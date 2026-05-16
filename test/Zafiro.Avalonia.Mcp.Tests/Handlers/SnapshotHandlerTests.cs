using System.Text.Json;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Templates;
using Avalonia.Threading;
using Xunit;
using Zafiro.Avalonia.Mcp.AppHost.Handlers;
using Zafiro.Avalonia.Mcp.Protocol.Messages;

namespace Zafiro.Avalonia.Mcp.Tests.Handlers;

[Collection("Avalonia")]
public class SnapshotHandlerTests
{
    public SnapshotHandlerTests(AvaloniaTestFixture _)
    {
        NodeRegistry.Clear();
    }

    private static T Run<T>(Func<T> f) => Dispatcher.UIThread.Invoke(f);

    [Fact]
    public void Snapshot_DoesNotEmitGenericContainerTextSummaries()
    {
        var snapshot = Run(() =>
        {
            var root = new StackPanel
            {
                Children =
                {
                    new TextBlock { Text = "Choose mode" },
                    new StackPanel
                    {
                        Children =
                        {
                            new TextBlock { Text = "Battle Tower" },
                            new Button { Content = "Start" },
                        }
                    }
                }
            };

            Layout(root);
            return BuildSnapshot(root);
        });

        var elements = Elements(snapshot);
        Assert.Contains(elements, e => Text(e) == "Choose mode" && Role(e) == "text");
        Assert.Contains(elements, e => Text(e) == "Battle Tower" && Role(e) == "text");
        Assert.Contains(elements, e => Text(e) == "Start" && Role(e) == "button");
        Assert.DoesNotContain(elements, e => Text(e) == "Choose mode · Battle Tower");
    }

    [Fact]
    public void Snapshot_VerboseDetail_EmitsGenericContainerTextSummaries()
    {
        var snapshot = Run(() =>
        {
            var root = new StackPanel
            {
                Children =
                {
                    new TextBlock { Text = "Choose mode" },
                    new StackPanel
                    {
                        Children =
                        {
                            new TextBlock { Text = "Battle Tower" },
                            new Button { Content = "Start" },
                        }
                    }
                }
            };

            Layout(root);
            return BuildSnapshot(root, "verbose");
        });

        var elements = Elements(snapshot);
        Assert.Contains(elements, e => Text(e)?.Contains("Choose mode · Battle Tower", StringComparison.Ordinal) == true);
    }

    [Fact]
    public void Snapshot_RepresentsLabeledRegionsAsHierarchy()
    {
        var snapshot = Run(() =>
        {
            var actions = new StackPanel { Name = "ActionsRegion" };
            AutomationProperties.SetName(actions, "Actions");
            actions.Children.Add(new Button { Content = "Random" });

            var root = new StackPanel
            {
                Children =
                {
                    new TextBlock { Text = "Trainer setup" },
                    actions,
                }
            };

            Layout(root);
            return BuildSnapshot(root);
        });

        var elements = Elements(snapshot);
        var group = Assert.Single(elements, e => Role(e) == "group" && Text(e) == "Actions");
        var button = Assert.Single(elements, e => Role(e) == "button" && Text(e) == "Random");

        Assert.Equal(group.GetProperty("nodeId").GetInt32(), button.GetProperty("parentId").GetInt32());
        Assert.Equal(group.GetProperty("level").GetInt32() + 1, button.GetProperty("level").GetInt32());
    }

    [Fact]
    public void Snapshot_UsesAutomationControlTypeOverrideAsSemanticRole()
    {
        var snapshot = Run(() =>
        {
            var pane = new StackPanel { Name = "SidebarPane" };
            AutomationProperties.SetControlTypeOverride(pane, AutomationControlType.Pane);
            pane.Children.Add(new Button { Content = "Open" });

            var root = new StackPanel { Children = { pane } };

            Layout(root);
            return BuildSnapshot(root);
        });

        var elements = Elements(snapshot);
        var pane = Assert.Single(elements, e => Role(e) == "pane");
        var button = Assert.Single(elements, e => Role(e) == "button" && Text(e) == "Open");

        Assert.Equal("StackPanel", pane.GetProperty("type").GetString());
        Assert.Equal(pane.GetProperty("nodeId").GetInt32(), button.GetProperty("parentId").GetInt32());
    }

    [Fact]
    public void Snapshot_SuppressesTemplateTextCoveredByInteractiveParent()
    {
        var snapshot = Run(() =>
        {
            var button = new Button
            {
                Content = "🎲 Aleatorio",
                Template = new FuncControlTemplate<Button>((control, _) => new ContentPresenter
                {
                    Name = "PART_ContentPresenter",
                    Content = control.Content
                })
            };

            var root = new StackPanel { Children = { button } };
            Layout(root);
            button.ApplyTemplate();

            return BuildSnapshot(root);
        });

        var elements = Elements(snapshot);
        Assert.Single(elements, e => Text(e) == "🎲 Aleatorio");
        Assert.Contains(elements, e => Text(e) == "🎲 Aleatorio" && Role(e) == "button");
    }

    [Fact]
    public void Snapshot_VisibleOnly_UsesAbsoluteBoundsWithoutDoubleOffset()
    {
        var snapshot = Run(() =>
        {
            var button = new Button
            {
                Content = "Visible bottom action",
                Width = 180,
                Height = 40
            };
            Canvas.SetLeft(button, 20);
            Canvas.SetTop(button, 520);

            var root = new Canvas
            {
                Width = 800,
                Height = 600,
                Children = { button }
            };

            Layout(root);
            button.ApplyTemplate();

            return BuildSnapshot(root, visibleOnly: true);
        });

        var elements = Elements(snapshot);
        Assert.Contains(elements, e => Text(e) == "Visible bottom action" && Role(e) == "button");
    }

    [Fact]
    public void Snapshot_InvalidDetail_ReturnsInvalidParamError()
    {
        var result = Run(() =>
        {
            var root = new StackPanel { Children = { new TextBlock { Text = "Hello" } } };
            Layout(root);
            return SnapshotHandler.BuildSnapshot(root, visibleOnly: false, "raw");
        });

        var error = Assert.IsType<HandlerErrorResult>(result);
        Assert.Equal(DiagnosticErrorCodes.InvalidParam, error.Error.Code);
        Assert.Contains("smart", error.Error.Message);
        Assert.Contains("verbose", error.Error.Message);
    }

    private static JsonElement BuildSnapshot(Visual root, string detail = "smart", bool visibleOnly = false)
    {
        var result = SnapshotHandler.BuildSnapshot(root, visibleOnly, detail);
        return JsonSerializer.SerializeToElement(result, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
        });
    }

    private static JsonElement[] Elements(JsonElement snapshot) =>
        snapshot.GetProperty("elements").EnumerateArray().ToArray();

    private static string? Text(JsonElement element) =>
        element.TryGetProperty("text", out var text) ? text.GetString() : null;

    private static string Role(JsonElement element) =>
        element.GetProperty("role").GetString()!;

    private static void Layout(Control root)
    {
        root.Measure(new Size(800, 600));
        root.Arrange(new Rect(0, 0, 800, 600));
    }
}
