# Migrating to Zafiro.Avalonia.Mcp v2.0

> v2.0.0 is a **breaking release**. The wire protocol changed for every read+action MCP tool, error responses got a stable structured shape, eight new diagnostic tools were added, and one tool was renamed. This document is the canonical upgrade guide.

## Why v2

In v1 every interactive tool took a numeric `nodeId`. To click a button an agent had to:

1. Call `get_snapshot` (or `search`) to populate the node registry.
2. Pick the node it wanted and read out its `nodeId`.
3. Call `click` with that `nodeId`.

That was three round-trips per action and a constant source of `STALE_NODE` errors when the tree changed between steps 2 and 3. v2 collapses it to **one** round-trip with a CSS-like `selector` string the server resolves against the live tree:

```jsonc
// v1 — three calls
{ "method": "get_snapshot", "params": {} }
// → returns nodeId 42 for the Save button
{ "method": "click", "params": { "nodeId": 42 } }

// v2 — one call
{ "method": "click", "params": { "selector": "Button[Content=\"Save\"]" } }
```

Selectors also support DataContext predicates (`[dc.IsValid=true]`, `[dc:'x => x.Items.Count > 5']`), so MVVM-aware lookups stop requiring a per-VM tool.

## Tool name changes

| v1 name | v2 name | Notes |
|---|---|---|
| `take_screenshot` | `screenshot` | Verb-first; `selector` is now optional (omit to capture the first window). |

That is the only rename. Every other v1 tool keeps its name. Verified by grepping `[McpServerTool(Name = "…")]` across `src/Zafiro.Avalonia.Mcp.Tool/Tools/*.cs` and cross-checking the prior tool surface — no other `take_*` or otherwise-renamed tools exist on `main`.

> The hallucination map embedded in `instructions(page='tools')` (e.g. `take_snapshot → get_snapshot`, `set_text → text_input`, `set_property → set_prop`) lists names AI agents tend to invent. Those were never v1 names; they are guard-rails, not renames.

## Parameter migration: `nodeId` → `selector`

Every read+action handler that previously took `int nodeId` now takes `string selector`. Resolution goes through the shared `SelectorRequestHelper.ResolveSingle`, which returns the standardised errors `INVALID_PARAM` (missing selector), `INVALID_SELECTOR`, `NO_MATCH`, or `AMBIGUOUS_SELECTOR`. Multi-target read tools (`get_props`, `get_styles`, `get_datacontext`, `get_bindings`) silently take the first match when the selector is non-unique.

Sources of truth: commits `2dbb35b` (action tools) and `bf87161` (read tools).

### Action tools (commit `2dbb35b`)

| Tool | v1 params | v2 params |
|---|---|---|
| `click` | `{ "nodeId": 42 }` | `{ "selector": "Button[Content=\"Save\"]" }` |
| `tap` | `{ "nodeId": 42 }` | `{ "selector": "#SaveBtn" }` |
| `key_down` | `{ "nodeId": 12, "key": "Enter", "modifiers": "ctrl" }` | `{ "selector": "TextBox#Search", "key": "Enter", "modifiers": "ctrl" }` |
| `key_up` | `{ "nodeId": 12, "key": "Shift" }` | `{ "selector": "TextBox#Search", "key": "Shift" }` |
| `text_input` | `{ "nodeId": 12, "text": "alice", "pressEnter": true }` | `{ "selector": "TextBox#Email", "text": "alice", "pressEnter": true }` |
| `action` | `{ "nodeId": 42, "action": "BringIntoView" }` | `{ "selector": "ListBoxItem:nth(99)", "action": "BringIntoView" }` |
| `pseudo_class` | `{ "nodeId": 15, "pseudoClass": "pressed", "isActive": true }` | `{ "selector": "#SaveBtn", "pseudoClass": "pressed", "isActive": true }` |
| `toggle` | `{ "nodeId": 7, "state": true }` | `{ "selector": "CheckBox[Content=\"Remember me\"]", "state": true }` |
| `set_value` | `{ "nodeId": 21, "value": 75 }` | `{ "selector": "Slider#Volume", "value": 75 }` |
| `scroll` | `{ "nodeId": 35, "direction": "down", "amount": 200 }` | `{ "selector": "ListBox#Items", "direction": "down", "amount": 200 }` |
| `select_item` | `{ "nodeId": 35, "index": 2 }` | `{ "selector": "ListBox#Items", "index": 2 }` |
| `set_prop` | `{ "nodeId": 7, "propertyName": "Background", "value": "#FF0000", "isXamlValue": true }` | `{ "selector": "Border#Hero", "propertyName": "Background", "value": "#FF0000", "isXamlValue": true }` |
| `get_prop_values` | `{ "nodeId": 7, "propertyName": "HorizontalAlignment" }` | `{ "selector": "Border#Hero", "propertyName": "HorizontalAlignment" }` |
| `click_and_wait` | `{ "nodeId": 15, "waitQuery": "Saved", "waitCondition": "exists" }` | `{ "selector": "Button[Content=\"Save\"]", "waitQuery": "Saved", "waitCondition": "exists" }` |

### Read tools (commit `bf87161`)

| Tool | v1 params | v2 params |
|---|---|---|
| `get_props` | `{ "nodeId": 42, "propertyNames": "Width,Height" }` | `{ "selector": "Button[Content=\"Save\"]", "propertyNames": "Width,Height" }` |
| `get_styles` | `{ "nodeId": 42, "includeDefaults": false }` | `{ "selector": "#SaveBtn", "includeDefaults": false }` |
| `get_ancestors` | `{ "nodeId": 42 }` | `{ "selector": "#SaveBtn" }` |
| `get_datacontext` | `{ "nodeId": 1 }` | `{ "selector": "Window:nth(0)" }` |
| `get_bindings` | `{ "nodeId": 12 }` | `{ "selector": "TextBox#Email" }` |
| `find_view_source` | `{ "nodeId": 1 }` | `{ "selector": "Window:nth(0)" }` |
| `get_xaml` | `{ "nodeId": 1 }` | `{ "selector": "Window:nth(0)" }` |
| `screenshot` | `{ "nodeId": 42 }` *(or omitted for first window)* | `{ "selector": "#Hero" }` *(or omitted for first window)* |
| `get_tree` | `{ "nodeId": 1, "depth": 10 }` *(or omitted)* | `{ "selector": "Window:nth(0)", "depth": 10 }` *(or omitted)* |
| `get_snapshot` | `{ "nodeId": 1 }` *(or omitted for first window)* | `{ "selector": "#Hero" }` *(or omitted for first window)* |
| `get_screen_text` | `{ "nodeId": 1 }` *(or omitted for first window)* | `{ "selector": "#Hero" }` *(or omitted for first window)* |
| `get_interactables` | `{ "nodeId": 1 }` *(or omitted for all windows)* | `{ "selector": "Window:nth(0)" }` *(or omitted for all windows)* |
| `get_resources` | `{ "nodeId": 1, "onlySelf": false }` *(or omitted for Application)* | `{ "selector": "Window:nth(0)", "onlySelf": false }` *(or omitted for Application)* |
| `start_recording` | `{ "nodeId": 1, "fps": 15 }` *(or omitted for first window)* | `{ "selector": "Window:nth(0)", "fps": 15 }` *(or omitted for first window)* |
| `capture_animation` | `{ "nodeId": 1, "durationSec": 3 }` *(or omitted for first window)* | `{ "selector": "Window:nth(0)", "durationSec": 3 }` *(or omitted for first window)* |
| `diff_tree` | `{ "nodeId": 1, "action": "diff" }` *(or omitted for first window)* | `{ "selector": "Window:nth(0)", "action": "diff" }` *(or omitted for first window)* |

## New tools in v2

- **Diagnostics** — `get_focus`, `get_active_window`, `get_open_dialogs`, `get_command_info` (ICommand `CanExecute`), `get_validation_errors` (walks `INotifyDataErrorInfo`), `get_layout_info` (layout box, margins, alignment, clipping), `find_by_datacontext` (top-level Roslyn predicate over the tree), `get_item` (virtualised `ItemsControl` child by index/text/dc).
- **Composite** — `fill_form`: takes `fields: [{ selector, value, secret? }, …]` and an optional `submit` selector to click at the end. Returns per-field outcomes; when `secret:true` the recorded `applied` reads `"<value> (redacted)"`.
- **Events** — `subscribe`, `poll_events`, `unsubscribe`. Kinds: `property_changed`, `window_opened`, `window_closed`, `focus_changed`. Long-poll up to 60 s (default 30 s), 1000-event bounded queue per subscription, 5-minute TTL, 32 concurrent subscriptions cap (`SUBSCRIPTION_LIMIT` error code on overflow).
- **Catalogue** — `instructions(page='tools')` now returns a reflection-built tool catalogue (51 tools) plus the selector cheat-sheet, error-code table and recommended call orders. The default `instructions(page='readme')` is unchanged.

## Error response shape

### v1

```json
{ "error": "Cannot click this element" }
```

A free-form string. Agents had to grep prose to know whether to retry, re-resolve, or give up.

### v2

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

Stable codes (switch on `error.code`):

| Code | Recovery |
|---|---|
| `NO_MATCH` | Re-call `get_snapshot` or relax the selector. |
| `AMBIGUOUS_SELECTOR` | Add `:nth(N)` or a tighter attribute predicate. |
| `STALE_NODE` | A cached `nodeId` is no longer in the tree — re-resolve via selector. |
| `INVALID_PARAM` | Re-read the tool's parameter list. |
| `INVALID_SELECTOR` | Selector failed to parse — see grammar below. |
| `UNSUPPORTED_OPERATION` | The control does not support this operation. Pick a more specific tool (e.g. `toggle` for `CheckBox`). |
| `TIMEOUT` | Increase `timeoutMs` or verify the precondition. |
| `INTERNAL` | Retry once; if it persists, capture the message and report it. |

(Plus `SUBSCRIPTION_LIMIT` from the events module when the 32-subscription cap is hit.)

## Selector grammar quick reference

```text
#42                                  digits  → existing nodeId
#SaveBtn                             ident   → x:Name match
Button                               type    → all controls of that type
Button[Content="Save"]               attribute equality (case-insensitive)
TextBox[Text*="hello"]               *= contains, ^= starts, $= ends, ~= word
[dc.User.Name="Alice"]               dc.Path → DataContext property path
[dc:'x => x.IsValid']                Roslyn predicate (200ms, sandboxed)
ListBox >> ListBoxItem:nth(2)        '>>' descendant, ':nth(N)' positional
StackPanel > Button                  '>' direct child
:focused, :visible, :enabled, :checked   pseudo-classes (hyphenated)
Button[Content="OK"], Button[Content="Cancel"]   ',' alternatives
```

The full cheat-sheet — including all attribute operators and pseudo-classes — lives in the README and in the runtime catalogue (`instructions(page='tools')`). The implementation lives in `src/Zafiro.Avalonia.Mcp.Protocol/Selectors/` (parser + AST) and `SelectorEngine.Default` (resolver).

## Removed or deprecated tools

**None.** Every v1 tool still exists in v2; the only surface change is the `take_screenshot → screenshot` rename and the `nodeId → selector` parameter swap on read+action tools.

## Versioning and release

The repo uses **GitVersion** with `workflow: GitHubFlow/v1` and strategies `ConfiguredNextVersion` + `Mainline` (see `GitVersion.yml`). There is no `<Version>` or `<VersionPrefix>` in `Directory.Build.props` or any `.csproj`, so version is derived entirely from git tags — the v2.0.0 bump is materialised by the tag itself, not by a file edit.

To cut the release, the maintainer should run:

```bash
git tag -a v2.0.0 -m "v2.0.0 — universal selector API, structured errors, diagnostics, fill_form, events"
git push origin v2.0.0
```

The CI pipeline (`azure-pipelines.yml`) and `deployer.yaml` pick up the tag and publish the NuGet packages.
