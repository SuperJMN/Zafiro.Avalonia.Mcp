using System.ComponentModel;
using ModelContextProtocol.Server;

namespace Zafiro.Avalonia.Mcp.Tool.Tools;

[McpServerToolType]
public sealed class InstructionTools
{
    [McpServerTool(Name = "instructions"), Description("""
        Get usage instructions for the Avalonia MCP server: connection workflow, recommended tool order, and tips. Use page='installation' for setup, page='tools' for the canonical tool catalogue (anti-hallucination), page='readme' (default) for the usage guide.
        Returns: markdown text guide.
        Example: "# Avalonia MCP — Quickstart\n1. Call list_apps...\n"
        """)]
    public static string GetInstructions(string page = "readme")
    {
        return page.ToLowerInvariant() switch
        {
            "installation" => InstallationText,
            "tools" => ToolsCatalogue.GetMarkdown(),
            _ => ReadmeText
        };
    }

    private const string ReadmeText = """
        Zafiro MCP Server for Avalonia — Usage Guide
        =============================================

        This MCP server connects to running Avalonia applications to inspect,
        interact with, and record their UI.
        This is a community project, not officially maintained by Avalonia UI.

        Connection workflow (desktop):
        1. Use 'list_apps' to find running Avalonia apps with MCP diagnostics enabled
        2. Use 'connect_to_app' with a PID to connect
        3. Use inspection/interaction tools

        Connection workflow (Android via ADB):
        Avalonia.Android apps do NOT appear in 'list_apps' — discovery files live on the
        device, not on the host. Use this flow instead (requires `adb` on PATH):

          1. adb devices                                          # confirm a device is attached
          2. PKG=<your.android.package.id>                        # e.g. com.zafiro.avalonia.mcp.sampleapp
          3. PORT=$(adb shell "cat /sdcard/Android/data/$PKG/cache/zafiro-avalonia-mcp/*.json" \
                    | grep -oE '"Endpoint":"tcp:[^"]+"' | grep -oE '[0-9]+"$' | tr -d '"')
          4. adb forward tcp:9999 tcp:$PORT
          5. Call 'connect_adb' with port=9999

        Once connected, every other tool (get_snapshot, click_by_query, screenshot, ...)
        works identically to desktop — Android uses a single TopLevel instead of Windows,
        but handlers transparently support both.

        Tip: if step 3 fails the app may not be running, may not call UseMcpDiagnostics(),
        or may target an old AppHost version. Verify with:
          adb logcat -d -s DOTNET:* | grep ZafiroMcp

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
        Zafiro MCP for Avalonia — Installation
        ======================================

        App-side setup (add to your Avalonia project):

        1. Add the Zafiro.Avalonia.Mcp.AppHost NuGet package:
           dotnet add package Zafiro.Avalonia.Mcp.AppHost

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
           dotnet tool install -g Zafiro.Avalonia.Mcp.Tool

        Configure in your MCP client (e.g., Claude Desktop, Copilot):
           {
             "mcpServers": {
               "zafiro-avalonia-mcp": {
                 "command": "zafiro-avalonia-mcp"
               }
             }
           }

        Android (preview):

        Avalonia.Android apps are supported via TCP loopback + `adb forward`.
        On Android, UseMcpDiagnostics() auto-switches the transport to TCP and
        writes the discovery JSON under Context.ExternalCacheDir
        (/sdcard/Android/data/<pkg>/cache/zafiro-avalonia-mcp/<pid>.json).

        Agent-side flow (one extra tool, then identical to desktop):
          adb forward tcp:9999 tcp:<devicePortFromJson>
          → connect_adb port=9999
          → get_snapshot / click_by_query / screenshot / ...
        """;
}
