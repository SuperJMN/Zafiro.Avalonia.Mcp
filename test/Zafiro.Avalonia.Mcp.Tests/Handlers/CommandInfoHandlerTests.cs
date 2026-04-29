using System.Windows.Input;
using Avalonia.Controls;
using Xunit;
using Zafiro.Avalonia.Mcp.AppHost.Handlers;

namespace Zafiro.Avalonia.Mcp.Tests.Handlers;

/// <summary>
/// Tests for CommandInfoHandler.Analyze — runs entirely without the dispatcher.
/// </summary>
[Collection("Avalonia")]
public class CommandInfoHandlerTests
{
    public CommandInfoHandlerTests(AvaloniaTestFixture _)
    {
        NodeRegistry.Clear();
    }

    [Fact]
    public void EnabledCommand_Returns_EnableReason_Enabled()
    {
        var btn = new Button { Command = new TestCommand(canExecute: true) };

        var r = CommandInfoHandler.Analyze(btn);

        Assert.Equal("enabled", r.enableReason);
        Assert.True(r.canExecute);
        Assert.True(r.isEnabled);
    }

    [Fact]
    public void CannotExecute_Returns_EnableReason_CommandCannotExecute()
    {
        var btn = new Button { Command = new TestCommand(canExecute: false) };

        var r = CommandInfoHandler.Analyze(btn);

        Assert.Equal("command_cannot_execute", r.enableReason);
        Assert.False(r.canExecute);
    }

    [Fact]
    public void IsEnabledFalse_NoCommand_Returns_EnableReason_IsEnabledFalse()
    {
        var btn = new Button { IsEnabled = false };

        var r = CommandInfoHandler.Analyze(btn);

        Assert.Equal("is_enabled_false", r.enableReason);
        Assert.False(r.isEnabled);
    }

    [Fact]
    public void ParentPanelDisabled_Returns_EnableReason_ParentDisabled()
    {
        var btn = new Button { Command = new TestCommand(canExecute: true) };
        var panel = new StackPanel { IsEnabled = false };
        panel.Children.Add(btn);

        var r = CommandInfoHandler.Analyze(btn);

        Assert.Equal("parent_disabled", r.enableReason);
    }

    [Fact]
    public void NoCommand_Returns_NullCommandType_AndEnabledReason()
    {
        var btn = new Button();

        var r = CommandInfoHandler.Analyze(btn);

        Assert.Null(r.commandType);
        Assert.Null(r.canExecute);
        Assert.True(r.isEnabled);
        Assert.Equal("enabled", r.enableReason);
    }

    [Fact]
    public void CommandWithParameter_Passes_ParameterStringToResult()
    {
        var btn = new Button
        {
            Command = new TestCommand(canExecute: true),
            CommandParameter = "hello"
        };

        var r = CommandInfoHandler.Analyze(btn);

        Assert.Equal("hello", r.parameter);
        Assert.True(r.canExecute);
    }
}

internal sealed class TestCommand : ICommand
{
    private readonly bool _canExecute;

    public TestCommand(bool canExecute) => _canExecute = canExecute;

    public bool CanExecute(object? parameter) => _canExecute;
    public void Execute(object? parameter) { }
    public event EventHandler? CanExecuteChanged;
}
