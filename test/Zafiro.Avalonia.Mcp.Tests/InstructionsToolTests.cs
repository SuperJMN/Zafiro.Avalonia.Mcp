using Xunit;
using Zafiro.Avalonia.Mcp.Tool.Tools;

namespace Zafiro.Avalonia.Mcp.Tests;

public class InstructionsToolTests
{
    [Fact]
    public void Tools_Page_Lists_Click_Tool()
    {
        var body = InstructionTools.GetInstructions("tools");
        Assert.Contains("zafiro-avalonia-mcp-click", body);
        Assert.Contains("selector", body);
    }

    [Fact]
    public void Tools_Page_Lists_Screenshot_Tool()
    {
        var body = InstructionTools.GetInstructions("tools");
        Assert.Contains("zafiro-avalonia-mcp-screenshot", body);
    }

    [Fact]
    public void Tools_Page_HallucinationTable_Maps_TakeScreenshot_To_Screenshot()
    {
        var body = InstructionTools.GetInstructions("tools");
        var idx = body.IndexOf("take_screenshot", StringComparison.Ordinal);
        Assert.True(idx >= 0, "Expected hallucination row for take_screenshot");

        var eol = body.IndexOf('\n', idx);
        var row = eol > idx ? body[idx..eol] : body[idx..];
        Assert.Contains("`screenshot`", row);
    }

    [Fact]
    public void Tools_Page_Includes_Selector_CheatSheet()
    {
        var body = InstructionTools.GetInstructions("tools");
        Assert.Contains(">>", body);
        Assert.Contains(":has-text(", body);
    }

    [Fact]
    public void Tools_Page_Includes_StandardErrorCodes()
    {
        var body = InstructionTools.GetInstructions("tools");
        Assert.Contains("NO_MATCH", body);
        Assert.Contains("STALE_NODE", body);
    }

    [Fact]
    public void Default_Page_Is_Backward_Compatible()
    {
        var defaultBody = InstructionTools.GetInstructions();
        var readmeBody = InstructionTools.GetInstructions("readme");
        Assert.Equal(readmeBody, defaultBody);
        Assert.Contains("Zafiro MCP Server for Avalonia — Usage Guide", defaultBody);
    }

    [Fact]
    public void Catalogue_Discovers_All_Registered_Tool_Types()
    {
        var tools = ToolsCatalogue.GetTools();
        // Spot-check a few canonical names from each tool class to ensure reflection works end-to-end.
        var names = tools.Select(t => t.Name).ToHashSet();
        Assert.Contains("click", names);
        Assert.Contains("screenshot", names);
        Assert.Contains("get_snapshot", names);
        Assert.Contains("wait_for", names);
        Assert.Contains("instructions", names);
    }
}
