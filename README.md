# Zafiro.Avalonia.Mcp

> **Community project** — Not officially affiliated with, endorsed by, or maintained by AvaloniaUI OÜ. "Avalonia" is a trademark of AvaloniaUI OÜ.

MCP (Model Context Protocol) bridge that lets AI agents **inspect, interact with, and capture** a running Avalonia UI application in real time — without screenshots.

```
┌─────────────────┐       named pipe        ┌──────────────────────┐
│  Avalonia App   │◄──────────────────────►│  Zafiro.Avalonia.Mcp │
│  (AppHost)      │  zafiro-avalonia-mcp-   │  (.NET tool)         │
│                 │  {PID}                  │                      │
└─────────────────┘                         └──────────┬───────────┘
                                                       │ stdio JSON-RPC
                                                       ▼
                                              ┌─────────────────┐
                                              │   AI Agent      │
                                              │ (Copilot, Claude│
                                              │  Codex, etc.)   │
                                              └─────────────────┘
```

## Prerequisites

- **.NET 10 SDK** — required for the `dnx` command used to run the tool without installing it
- **Avalonia 12.x** application
- An MCP-capable AI agent (see [Configure your agent](#configure-your-agent) below)

## Step 1 — Add AppHost to your Avalonia app

In the **Desktop project** (the one with `Program.cs` and `AppBuilder`):

```bash
dotnet add <YourDesktopProject.csproj> package Zafiro.Avalonia.Mcp.AppHost
```

Add a single line to your `AppBuilder`:

```csharp
using Zafiro.Avalonia.Mcp.AppHost;

public static AppBuilder BuildAvaloniaApp()
    => AppBuilder.Configure<App>()
        .UsePlatformDetect()
        .UseMcpDiagnostics()   // ← Add this
        .WithInterFont()
        .LogToTrace();
```

That's all. On startup the app writes a discovery file to `{TEMP}/zafiro-avalonia-mcp/{PID}.json` and starts a named-pipe server. The AI tool finds it automatically.

> **Debug-only variant** — wrap with `#if DEBUG` and add a conditional `PackageReference` if you don't want diagnostics in production builds.

## Step 2 — Configure your agent

No installation is needed. The `dnx` command (new in .NET 10) checks NuGet for the latest version **on every invocation** and downloads it automatically if needed. You always get the newest release without any manual update step.

---

### GitHub Copilot CLI

Edit **`~/.copilot/mcp.json`**:

```json
{
  "servers": {
    "zafiro-avalonia-mcp": {
      "type": "stdio",
      "command": "dnx",
      "args": ["Zafiro.Avalonia.Mcp.Tool", "--yes"]
    }
  }
}
```

---

### Claude Code

Run once to register the server:

```bash
claude mcp add --transport stdio zafiro-avalonia-mcp -- dnx Zafiro.Avalonia.Mcp.Tool --yes
```

Or add a **`.mcp.json`** at the project root (shared with your team via version control):

```json
{
  "mcpServers": {
    "zafiro-avalonia-mcp": {
      "command": "dnx",
      "args": ["Zafiro.Avalonia.Mcp.Tool", "--yes"]
    }
  }
}
```

---

### Codex CLI

Add to **`~/.codex/config.toml`**:

```toml
[mcp_servers.zafiro-avalonia-mcp]
command = "dnx"
args = ["Zafiro.Avalonia.Mcp.Tool", "--yes"]
```

---

### VS Code (GitHub Copilot)

Create or update **`.vscode/mcp.json`** in your workspace:

```json
{
  "servers": {
    "zafiro-avalonia-mcp": {
      "type": "stdio",
      "command": "dnx",
      "args": ["Zafiro.Avalonia.Mcp.Tool", "--yes"]
    }
  }
}
```

---

### Other MCP clients

Any client that supports stdio transport:
- **Command:** `dnx`
- **Args:** `Zafiro.Avalonia.Mcp.Tool`, `--yes`

> **No .NET 10?** Install the tool globally instead and use `zafiro-avalonia-mcp` as the command with no args:
> ```bash
> dotnet tool install -g Zafiro.Avalonia.Mcp.Tool
> dotnet tool update  -g Zafiro.Avalonia.Mcp.Tool   # to update
> ```

## Step 3 — Verify

1. Run your Avalonia app.
2. In the AI agent, call `list_apps` — your app should appear.
3. Call `connect_to_app` to connect.
4. Start inspecting with `get_snapshot`, `get_screen_text`, `click`, etc.

## Available tools

| Category | Tools |
|---|---|
| **Connection** | `list_apps`, `connect_to_app` |
| **Inspection** | `get_snapshot`, `get_tree`, `get_screen_text`, `get_interactables`, `search`, `get_ancestors` |
| **Properties** | `get_props`, `set_prop`, `get_styles`, `get_resources` |
| **MVVM / XAML** | `get_datacontext`, `get_bindings`, `find_view_source`, `get_xaml` |
| **Input** | `click`, `click_by_query`, `click_and_wait`, `key_down`, `key_up`, `text_input` |
| **Interaction** | `select_item`, `toggle`, `set_value`, `scroll`, `action` |
| **Visual states** | `pseudo_class` |
| **Capture** | `screenshot`, `capture_animation`, `start_recording`, `stop_recording` |
| **Assets** | `list_assets`, `open_asset` |
| **Windows** | `list_windows` |
| **Utilities** | `wait_for`, `diff_tree`, `instructions` |

## Troubleshooting

| Issue | Solution |
|---|---|
| `list_apps` returns empty | Ensure the app is running with `UseMcpDiagnostics()`. Check `{TEMP}/zafiro-avalonia-mcp/` for discovery files. |
| `dnx` not found | Requires .NET 10 SDK. Run `dotnet --version`. Fall back to global install for .NET 8/9. |
| New release not picked up yet | NuGet HTTP responses are briefly cached. Force an immediate check: `dnx --no-http-cache Zafiro.Avalonia.Mcp.Tool --yes` |
| Want a specific version | Pin it explicitly: `dnx Zafiro.Avalonia.Mcp.Tool@1.2.3 --yes` |
| `TypeLoadException` | Version mismatch — `AppHost` targets Avalonia 12.x, not compatible with Avalonia 11.x. |
| Stale discovery files | If the app crashed, delete leftover `.json` files from `{TEMP}/zafiro-avalonia-mcp/`. |
