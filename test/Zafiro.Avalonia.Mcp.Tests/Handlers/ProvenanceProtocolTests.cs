using Avalonia.Controls;
using Avalonia.Styling;
using Avalonia.Threading;
using Xunit;
using Zafiro.Avalonia.Mcp.AppHost.Handlers;
using Zafiro.Avalonia.Mcp.Protocol;
using Zafiro.Avalonia.Mcp.Protocol.Messages;

namespace Zafiro.Avalonia.Mcp.Tests.Handlers;

[Collection("Avalonia")]
public class ProvenanceProtocolTests
{
    public ProvenanceProtocolTests(AvaloniaTestFixture _)
    {
        NodeRegistry.Clear();
    }

    [Fact]
    public void Dispatcher_ExposesBothOperationsWithSharedSourceIdentity()
    {
        Dispatcher.UIThread.Invoke(() =>
        {
            var target = new Border();
            var style = new Style(selector => selector.OfType<Border>());
            style.Setters.Add(new Setter(Border.WidthProperty, 42d));
            var window = new Window { Content = target };
            window.Styles.Add(style);
            window.Show();
            try
            {
                var selector = $"#{NodeRegistry.GetOrRegister(target)}";
                var explanation = Dispatch(ProtocolMethods.ExplainProperty,
                    new { selector, propertyName = "Layoutable.Width", includeCandidates = true });
                var styles = Dispatch(ProtocolMethods.GetStyles,
                    new { selector, propertyNames = new[] { "Layoutable.Width" } });

                Assert.Null(explanation.ErrorInfo);
                Assert.Null(styles.ErrorInfo);
                var property = explanation.Result!.Value;
                var source = Assert.Single(styles.Result!.Value.GetProperty("styles").EnumerateArray());
                Assert.Equal("42", property.GetProperty("effectiveValue").GetString());
                Assert.Equal(source.GetProperty("sourceId").GetString(), property.GetProperty("origin").GetProperty("sourceId").GetString());
                Assert.True(property.TryGetProperty("timestamp", out _));
                Assert.True(styles.Result.Value.GetProperty("classState").TryGetProperty("pseudoClasses", out _));
            }
            finally
            {
                window.Close();
            }
        });
    }

    [Theory]
    [InlineData(ProtocolMethods.ExplainProperty)]
    [InlineData(ProtocolMethods.GetStyles)]
    public void AmbiguousElements_AreErrorsRatherThanArbitraryFirstMatches(string method)
    {
        Dispatcher.UIThread.Invoke(() =>
        {
            var window = new Window { Content = new StackPanel { Children = { new Border(), new Border() } } };
            window.Show();
            try
            {
                var response = Dispatch(method, new { selector = "Border", propertyName = "Layoutable.Width" });
                Assert.Equal(DiagnosticErrorCodes.AmbiguousSelector, response.ErrorInfo!.Code);
            }
            finally
            {
                window.Close();
            }
        });
    }

    [Theory]
    [InlineData(0)]
    [InlineData(33)]
    public void InvalidDepth_IsRejectedBeforeTargetInspection(int maxDepth)
    {
        var result = Dispatch(ProtocolMethods.ExplainProperty, new { selector = "Border", propertyName = "Width", maxDepth });
        Assert.Equal(DiagnosticErrorCodes.InvalidParam, result.ErrorInfo!.Code);
    }

    [Theory]
    [InlineData(ProtocolMethods.ExplainProperty)]
    [InlineData(ProtocolMethods.GetStyles)]
    public void NonObjectParameters_AreExplicitlyRejected(string method)
    {
        var result = Dispatch(method, new[] { "not", "parameters" });
        Assert.Equal(DiagnosticErrorCodes.InvalidParam, result.ErrorInfo!.Code);
    }

    private static DiagnosticResponse Dispatch(string method, object parameters)
    {
        var request = new DiagnosticRequest { Id = "provenance", Method = method, Params = ProtocolSerializer.ToElement(parameters) };
        var task = new RequestDispatcher().Dispatch(ProtocolSerializer.Serialize(request));
        if (!task.IsCompleted)
            Dispatcher.UIThread.RunJobs();
        Assert.True(task.IsCompleted, "The request did not finish within the UI-thread snapshot.");
        return ProtocolSerializer.Deserialize<DiagnosticResponse>(task.GetAwaiter().GetResult())!;
    }
}
