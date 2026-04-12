using System.ComponentModel;
using ModelContextProtocol.Server;

namespace AvaloniaMcp.Server.Tools;

[McpServerToolType]
public sealed class InstructionTools
{
    [McpServerTool(Name = "instructions"), Description("Get usage instructions for the Avalonia MCP server. Returns a guide covering connection workflow, available tools, and tips. Use page='installation' for setup instructions.")]
    public static string GetInstructions(string page = "readme")
    {
        return page.ToLowerInvariant() switch
        {
            "installation" => InstallationText,
            _ => ReadmeText
        };
    }

    private const string ReadmeText = """
        Avalonia MCP Server — Usage Guide
        ==================================

        This MCP server connects to running Avalonia applications to inspect,
        interact with, and record their UI.

        Connection workflow:
        1. Use 'list_apps' to find running Avalonia apps with MCP diagnostics enabled
        2. Use 'connect_to_app' with a PID to connect
        3. Use inspection/interaction tools

        Inspection:
        - get_tree: Get Visual/Logical/Merged tree
        - search: Find elements by type or name
        - get_ancestors: Get parent chain to root
        - get_props: Read properties of an element
        - get_styles: Get applied styles and classes

        Interaction:
        - click: Click a button or control
        - text_input: Enter text into a TextBox
        - key_down / key_up: Send keyboard events
        - action: Focus, Enable, Disable, BringIntoView
        - set_prop: Change property values
        - pseudo_class: Toggle pseudo-classes (:pointerover, :pressed, etc.)

        Capture:
        - screenshot: Capture PNG of any element or window
        - start_recording + stop_recording: Record animated GIF
        - capture_animation: One-shot record for N seconds

        Resources:
        - get_resources: Inspect resource dictionaries
        - list_assets: List embedded assets (avares://)
        - open_asset: Download an asset by URL

        Tips:
        - Node IDs from get_tree/search are used with other tools
        - All tree operations invalidate IDs — re-query after changes
        - Screenshots/GIFs are returned as base64-encoded images
        """;

    private const string InstallationText = """
        Avalonia MCP — Installation
        ===========================

        App-side setup (add to your Avalonia project):

        1. Add the AvaloniaMcp.AppHost NuGet package:
           dotnet add package AvaloniaMcp.AppHost

        2. In Program.cs, add .UseMcpDiagnostics() to your AppBuilder:

           public static AppBuilder BuildAvaloniaApp()
               => AppBuilder.Configure<App>()
                   .UsePlatformDetect()
                   .UseMcpDiagnostics()   // ← Add this
                   .WithInterFont()
                   .LogToTrace();

        3. Run your app. It will create a discovery file and named pipe.

        MCP Server setup (for AI agent integration):

        Install as a global dotnet tool:
           dotnet tool install -g AvaloniaMcp.Server

        Configure in your MCP client (e.g., Claude Desktop, Copilot):
           {
             "mcpServers": {
               "avalonia": {
                 "command": "avalonia-mcp"
               }
             }
           }
        """;
}
