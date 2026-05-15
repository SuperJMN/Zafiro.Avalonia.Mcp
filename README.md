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
> 6. **AXAML preview** — `preview_axaml` launches one view in an isolated MCP-connected desktop preview process; `close_preview` cleans it up.
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

## Preview one AXAML file

For layout work, an agent can open a single desktop AXAML document without navigating the real app:

```jsonc
{
  "axamlPath": "src/MyApp/Views/EditProjectView.axaml",
  "projectPath": "src/MyApp/MyApp.csproj",
  "width": 390,
  "height": 844
}
```

Call `preview_axaml` with `axamlPath` and exactly one of `projectPath` or `assemblyPath`. In `projectPath` mode the tool builds/evaluates the app by default, launches a hidden preview host process, loads the AXAML in design mode, connects MCP to that preview, waits until the preview answers `get_snapshot`, and returns `{ pid, title, axamlPath, connected }`. The global dotnet tool does not embed Avalonia desktop binaries; the temporary preview host is built from the target app output and restores only the runtime XAML loader when that DLL is not already present.

After that, use the normal tools against the preview: `get_snapshot`, `screenshot`, `get_datacontext`, selectors, `wait_for`, and so on. Finish with `close_preview` to terminate the process. Preview is desktop-only; Android still uses the `connect_adb` flow.

On Linux, agent hosts sometimes start the MCP tool without `DISPLAY`, `WAYLAND_DISPLAY`, or `XDG_RUNTIME_DIR` even though the parent graphical session has them. The preview launcher recovers missing graphical session variables from same-user ancestor processes before starting the temporary host. If no display is available after recovery, `preview_axaml` fails before launch with `DISPLAY_UNAVAILABLE` and includes non-sensitive environment hints.

`preview_axaml` uses runtime XAML loading, so it can differ from the compiled AXAML path used by the normal app. See [AXAML preview runtime loader limitations](docs/axaml-preview-loader-limitations.md) for known differences around compiled bindings, design-time data, resource lookup, custom controls, and generated code assumptions.

The preview host executes the target app's real `BuildAvaloniaApp` path before it creates the isolated preview window. That keeps app styles, resources, fonts, ReactiveUI setup, platform options, and other framework configuration available, but it also means app startup code can run in the preview process. Guard production-only work such as service startup, file writes, network calls, timers, database migrations, telemetry, and background sync. The preview process sets `ZAFIRO_AVALONIA_MCP_PREVIEW=1`; apps can branch on that value inside `BuildAvaloniaApp`, `App.Initialize`, or service composition to skip side effects while keeping UI resources loaded.

```csharp
var isPreview = Environment.GetEnvironmentVariable("ZAFIRO_AVALONIA_MCP_PREVIEW") == "1";
var builder = AppBuilder.Configure<App>()
    .UsePlatformDetect();

if (!isPreview)
{
    StartProductionServices();
}
```

When the preview host needs copied native assets from `runtimes/<rid>/native`, it resolves them by runtime identifier instead of directory enumeration order. The exact current RID is tried first, then compatible RIDs from the same OS family and architecture, then the same OS family, and finally any remaining copied RID as a deterministic compatibility fallback. Library file names are matched using the target RID's platform convention (`.dll`, `.dylib`, or `.so`).

## Available tools

> Tool naming convention: bare verbs (`click`, `screenshot`, `scroll`) or `get_*`/`list_*` prefixes. **No `take_*`** — `take_screenshot` was renamed to `screenshot` in v2.0. Call `instructions` with `page='tools'` to receive the full reflection-built catalogue, the selector cheat-sheet, and the error-code table; that page is the canonical surface and updates itself when tools are added.

| Category | Tools |
|---|---|
| **Connection** | `list_apps`, `connect_to_app`, `connect_adb` |
| **Preview** | `preview_axaml`, `close_preview` |
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
| `BUILD_FAILED` | `preview_axaml` could not build/evaluate the target project or generated preview host. |
| `DISPLAY_UNAVAILABLE` | `preview_axaml` could not access a graphical desktop display. |
| `PREVIEW_HOST_EXITED` | The generated preview host exited before it was ready; inspect returned stdout/stderr. |

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
| `preview_axaml` asks for `entryType` | The target assembly has multiple possible Avalonia entry points. Pass the full type name of `Program` with `BuildAvaloniaApp` or the `Application` subclass. |
| `preview_axaml` cannot find the target assembly | In project mode the tool uses MSBuild `TargetPath`; build the requested configuration/framework or leave `build=true`. In assembly mode pass the built app `.dll`. |
| `preview_axaml` reports `DISPLAY_UNAVAILABLE` | The MCP server process has no usable desktop display. Run it from the graphical session or pass `DISPLAY`/`WAYLAND_DISPLAY` and `XDG_RUNTIME_DIR` into that process. |
| `preview_axaml` reports `PREVIEW_HOST_EXITED` | The generated preview host crashed or closed before it was ready. Inspect the returned stdout/stderr and preview host project path to distinguish AXAML load, app startup, resource, and environment failures. |
| `preview_axaml` reports a type or resource resolution failure | Rebuild the target app, confirm the AXAML `x:Class` namespace and assembly, check `avares://` resource paths, and compare with [runtime loader limitations](docs/axaml-preview-loader-limitations.md). |
| `preview_axaml` starts real app services | The preview host runs `BuildAvaloniaApp`. Check `ZAFIRO_AVALONIA_MCP_PREVIEW=1` and skip production-only startup work while keeping UI setup and resources available. |

## Android via ADB (preview)

Avalonia.Android apps are supported through TCP loopback + `adb forward`. Same MCP tool surface as desktop — only the connection step changes.

### App side

`UseMcpDiagnostics()` auto-detects Android and switches to TCP. To force it explicitly:

```csharp
this.UseMcpDiagnostics(opts => opts.Transport = TransportKind.Tcp);
```

On Android the discovery JSON is written to `Context.ExternalCacheDir/zafiro-avalonia-mcp/<pid>.json` (readable via `adb shell cat` without `run-as`).

### Agent side (manual MVP)

```bash
# 1. Find the device-side port
adb shell cat /sdcard/Android/data/<your.package.id>/cache/zafiro-avalonia-mcp/*.json
#    → look for "endpoint": "tcp:127.0.0.1:54321"

# 2. Forward it to a local port of your choice
adb forward tcp:9999 tcp:54321
```

Then in your agent, instead of `list_apps`, call:

```
connect_adb port=9999
```

(`host` defaults to `127.0.0.1`, `label` is optional.) After that, every other MCP tool (`get_snapshot`, `click_by_query`, `text_input`, `screenshot`, …) works exactly as on desktop.

Out of scope for the MVP: auto-discovery of devices, automatic `adb forward` cleanup, TLS, non-loopback bindings. Tracked in [`ROADMAP.md`](ROADMAP.md) Fase 7.
