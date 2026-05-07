# AXAML Previewer Architecture Review

## Context

The `preview_axaml` tool is useful because it opens one AXAML document in an isolated
desktop process and connects MCP directly to that preview window. This keeps agents
focused on one screen instead of forcing them to navigate the full application.

The current architecture is pragmatic and already good enough for real layout work,
especially after the preview host fix that separates:

- the application assembly used to build and initialize Avalonia, and
- the AXAML local assembly used by `AvaloniaRuntimeXamlLoader`.

That split matters for common Avalonia solutions where `Program.BuildAvaloniaApp`
lives in a desktop host project while views live in a shared UI assembly.

## Strengths

- Preview runs in a separate process, so failures do not take down the MCP server.
- It reuses the target app's real `BuildAvaloniaApp` path, so styles, resources,
  fonts, ReactiveUI initialization, platform setup, and `App.axaml` are available.
- It loads a single AXAML document and scopes MCP to that preview window.
- It supports design-time data by applying `Design.DataContext` to the loaded root.
- The temporary host references the target app output, avoiding a large tool package
  with embedded Avalonia desktop binaries.

## Risks And Weak Spots

- The preview host is generated from a large C# source string. This makes refactors
  hard to review and encourages brittle `Assert.Contains` tests.
- Host process failures are underreported. `PreviewProcessManager` drains stdout and
  stderr, then reports only that the process exited.
- The runtime XAML loader is not identical to compiled AXAML. Some binding,
  compiled-binding, resource, or design-time behaviors can diverge from the real
  compiled application path.
- `Program.BuildAvaloniaApp` can have side effects before the preview window is
  shown. The preview closes initial windows, but it cannot prevent file writes,
  service startup, background timers, network calls, or database initialization.
- Native runtime resolution currently works through copied `runtimes` assets and a
  fallback lookup. This should become RID-aware and explicit so it is predictable on
  Linux, macOS, Windows, and ARM devices.
- Current automated tests cover important units, but there is no end-to-end fixture
  proving `preview_axaml` against a multi-assembly app with a desktop host and shared
  UI project.

## Proposed GitHub Issues

### 1. Surface preview host stdout/stderr when `preview_axaml` fails

GitHub issue: [#7](https://github.com/SuperJMN/Zafiro.Avalonia.Mcp/issues/7)

When the preview host exits before publishing MCP discovery, return captured stdout
and stderr in the structured error details. This makes failures such as XAML type
resolution, ReactiveUI initialization, and native library loading immediately
diagnosable from the tool response.

Suggested labels: `bug`, `enhancement`.

### 2. Add an end-to-end `preview_axaml` fixture for multi-assembly apps

GitHub issue: [#8](https://github.com/SuperJMN/Zafiro.Avalonia.Mcp/issues/8)

Create a small test app where `Program.BuildAvaloniaApp` lives in a desktop host
assembly and the AXAML view lives in a referenced UI assembly. The test should call
the real preview flow and assert that MCP connects and can read visible text.

Suggested labels: `enhancement`.

### 3. Replace the generated preview host source string with a maintainable host template

GitHub issue: [#9](https://github.com/SuperJMN/Zafiro.Avalonia.Mcp/issues/9)

Move the preview host implementation out of one large embedded string. Prefer a
template file, source-generated resource, or small reusable host project so normal C#
tooling, formatting, and refactoring can cover it.

Suggested labels: `enhancement`.

### 4. Make native runtime asset resolution RID-aware

GitHub issue: [#10](https://github.com/SuperJMN/Zafiro.Avalonia.Mcp/issues/10)

Replace broad recursive native asset probing with explicit RID selection and clear
fallback rules. This should cover `linux-x64`, `linux-arm64`, `osx-*`, `win-*`, and
future target RIDs without relying on directory enumeration order.

Suggested labels: `enhancement`.

### 5. Define and document the preview side-effect contract

GitHub issue: [#11](https://github.com/SuperJMN/Zafiro.Avalonia.Mcp/issues/11)

Document that `preview_axaml` executes the target app's `BuildAvaloniaApp` path and
therefore may trigger app startup side effects. Consider adding a recommended
preview-safe app builder pattern, an opt-in preview mode flag, or guidance for
guarding production services during previews.

Suggested labels: `documentation`, `enhancement`.

### 6. Document runtime XAML loader limitations

GitHub issue: [#12](https://github.com/SuperJMN/Zafiro.Avalonia.Mcp/issues/12)

Document where `AvaloniaRuntimeXamlLoader` may differ from compiled AXAML, including
compiled bindings, design-time-only constructs, resource lookup, and custom controls.
The goal is to set expectations for preview accuracy and make known limitations easy
to distinguish from bugs.

Suggested labels: `documentation`.
