# Zafiro.Avalonia.Mcp

> **Community project** — Not officially affiliated with, endorsed by, or maintained by AvaloniaUI OÜ. "Avalonia" is a trademark of AvaloniaUI OÜ.

MCP (Model Context Protocol) bridge that lets AI agents **inspect, interact with, and capture** a running Avalonia UI application in real time — without screenshots.

> **🚀 v2.0 highlights** — breaking release. See [`MIGRATION-v2.md`](MIGRATION-v2.md) for the upgrade guide.
>
> 1. **Universal CSS-like `selector`** replaces numeric `nodeId` in every read+action tool. Resolve elements in one round-trip: `Button[Content="Save"]`, `#SaveBtn`, `ListBox >> ListBoxItem[dc.Id=7]`, `[dc:'x => x.IsValid']`.
> 2. **Structured errors** (`DiagnosticError` with `code`, `message`, `suggested`, `details`) — 8 stable codes, machine-recoverable.
> 3. **New diagnostics**: `get_focus`, `get_active_window`, `get_open_dialogs`, `get_command_info`, `get_validation_errors`, `get_layout_info`, `find_by_datacontext`, `get_item`.
> 4. **Composite tool** `fill_form` — apply a list of fields + optional submit in one call, with `secret:true` redaction.
> 5. **Event subscriptions** — `subscribe` + `poll_events` + `unsubscribe` (kinds: `property_changed`, `window_opened`, `window_closed`, `focus_changed`).
>
> Plus: tool-naming hygiene (`take_screenshot` → `screenshot`), and `instructions(page='tools')` returns the full tool catalogue + selector cheat-sheet so agents stop hallucinating tool names.

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

### GitHub Copilot

GitHub Copilot uses **two separate config files** depending on the surface:

**Copilot CLI** (`~/.copilot/mcp.json`):

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

**Copilot coding agent / Copilot Chat** (`~/.copilot/mcp-config.json`):

```json
{
  "mcpServers": {
    "zafiro-avalonia-mcp": {
      "type": "stdio",
      "command": "dnx",
      "args": ["Zafiro.Avalonia.Mcp.Tool", "--yes"]
    }
  }
}
```

> If you see `spawn zafiro-avalonia-mcp ENOENT`, the config still has the old command name. Replace `"command": "zafiro-avalonia-mcp"` with `"command": "dnx"` and add `"args": ["Zafiro.Avalonia.Mcp.Tool", "--yes"]`.

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
4. Start inspecting with `get_snapshot`, `get_screen_text`, then act with selector-based tools — e.g. `click` with `selector: "Button[Content=\"Save\"]"`.

## Available tools

> Tool naming convention: bare verbs (`click`, `screenshot`, `scroll`) or `get_*`/`list_*` prefixes. **No `take_*`** — `take_screenshot` was renamed to `screenshot` in v2.0. Call `instructions` with `page='tools'` to receive the full reflection-built catalogue (51 tools), the selector cheat-sheet, and the error-code table; that page is the canonical surface and updates itself when tools are added.

| Category | Tools |
|---|---|
| **Connection** | `list_apps`, `connect_to_app` |
| **Inspection** | `get_snapshot`, `get_tree`, `get_screen_text`, `get_interactables`, `search`, `get_ancestors` |
| **Diagnostics** *(new in v2)* | `get_focus`, `get_active_window`, `get_open_dialogs`, `get_command_info`, `get_validation_errors`, `get_layout_info`, `find_by_datacontext`, `get_item` |
| **Properties** | `get_props`, `set_prop`, `get_prop_values`, `get_styles`, `get_resources` |
| **MVVM / XAML** | `get_datacontext`, `get_bindings`, `find_view_source`, `get_xaml` |
| **Input** | `click`, `click_by_query`, `click_and_wait`, `key_down`, `key_up`, `text_input`, `tap` |
| **Interaction** | `select_item`, `toggle`, `set_value`, `scroll`, `action` |
| **Composite** *(new in v2)* | `fill_form` |
| **Events** *(new in v2)* | `subscribe`, `poll_events`, `unsubscribe` |
| **Visual states** | `pseudo_class` |
| **Capture** | `screenshot`, `capture_animation`, `start_recording`, `stop_recording` |
| **Assets** | `list_assets`, `open_asset` |
| **Windows** | `list_windows` |
| **Utilities** | `wait_for`, `diff_tree`, `instructions` |

## Selector cheat-sheet

Every read and action tool that targets an element accepts a single CSS-like `selector` string instead of a numeric `nodeId`. The engine resolves it against the live visual tree.

```text
#42                                  digits  → existing nodeId
#SaveBtn                             ident   → x:Name match
Button                               type    → all controls of that type
Button[Content="Save"]               attribute equality (case-insensitive)
TextBox[Text*="hello"]               *= contains, ^= starts, $= ends, ~= word
[dc.User.Name="Alice"]               dc.Path → DataContext property path
[dc:'x => x.IsValid && x.Items.Count > 0']  Roslyn predicate (200ms, sandboxed)
ListBox >> ListBoxItem:nth(2)        '>>' descendant, ':nth(N)' positional
StackPanel > Button                  '>' direct child
:focused, :visible, :enabled, :checked   pseudo-classes (hyphenated)
Button[Content="OK"], Button[Content="Cancel"]   ',' alternatives
```

**Examples**
- `click` selector `Button[Content="Save"]` instead of nodeId `42`.
- `set_prop` selector `#NameInput` property `Text` value `"Alice"`.
- `get_layout_info` selector `ListBox >> ListBoxItem[dc.Id=7]`.

For the full grammar and recommended workflows, call `instructions` with `page='tools'`.

## Error handling

Every failure response carries a structured `DiagnosticError`:

```json
{
  "error": {
    "code": "AMBIGUOUS_SELECTOR",
    "message": "Selector 'Button' matched 4 elements.",
    "suggested": "Add ':nth(N)' or a more specific predicate (e.g. Button[Content=\"Save\"]).",
    "details": { "matchCount": 4, "selector": "Button" }
  }
}
```

Stable codes you can switch on:

| Code | Meaning |
|---|---|
| `NO_MATCH` | Selector matched nothing. Re-read the snapshot or relax the predicate. |
| `AMBIGUOUS_SELECTOR` | Selector matched >1 element where exactly one was required. Add `:nth(N)`. |
| `STALE_NODE` | A previously-cached `nodeId` is no longer in the tree. Re-resolve via selector. |
| `INVALID_PARAM` | An argument failed validation. Re-read the tool's parameter list. |
| `INVALID_SELECTOR` | Selector failed to parse. See the cheat-sheet above. |
| `UNSUPPORTED_OPERATION` | The control does not support this operation. Pick a more specific tool. |
| `TIMEOUT` | A condition was not met within the timeout. |
| `INTERNAL` | Internal server error. Retry once; report if it persists. |

## Troubleshooting

| Issue | Solution |
|---|---|
| `spawn zafiro-avalonia-mcp ENOENT` | The config still uses the old command name. Replace `"command": "zafiro-avalonia-mcp"` with `"command": "dnx"` and add `"args": ["Zafiro.Avalonia.Mcp.Tool", "--yes"]` in all relevant config files (`~/.copilot/mcp-config.json`, `~/.copilot/mcp.json`, `.vscode/mcp.json`, etc.). |
| `list_apps` returns empty | Ensure the app is running with `UseMcpDiagnostics()`. Check `{TEMP}/zafiro-avalonia-mcp/` for discovery files. |
| `dnx` not found | Requires .NET 10 SDK. Run `dotnet --version`. Fall back to global install for .NET 8/9. |
| New release not picked up yet | NuGet HTTP responses are briefly cached. Force an immediate check: `dnx --no-http-cache Zafiro.Avalonia.Mcp.Tool --yes` |
| Want a specific version | Pin it explicitly: `dnx Zafiro.Avalonia.Mcp.Tool@1.2.3 --yes` |
| `TypeLoadException` | Version mismatch — `AppHost` targets Avalonia 12.x, not compatible with Avalonia 11.x. |
| Stale discovery files | If the app crashed, delete leftover `.json` files from `{TEMP}/zafiro-avalonia-mcp/`. |
