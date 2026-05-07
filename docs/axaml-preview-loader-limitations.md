# AXAML Preview Runtime Loader Limitations

`preview_axaml` loads one AXAML document with `AvaloniaRuntimeXamlLoader` in design mode. That is close enough for layout inspection, design data, screenshots, and selector-based MCP checks, but it is not the same path as compiled AXAML in a normal application build.

Use preview failures as diagnostics, not as proof that the compiled app is broken. If the same view builds and runs in the app but fails in `preview_axaml`, check the differences below first.

## Where Behavior Can Differ

- **Compiled bindings**: compiled binding metadata and generated helpers are produced by the normal build pipeline. Runtime loading may report type or property resolution failures that the compiled app handles through generated code.
- **Design-time constructs**: `preview_axaml` runs with design mode enabled and applies `Design.DataContext` to the loaded root. Design-only data can mask runtime `DataContext` problems, and runtime-only services can be unavailable unless the app startup path registers them.
- **Resource lookup**: the preview host initializes the target app, then loads a single AXAML file dynamically. App-level styles and resources are available, but relative resource references, merged dictionaries, theme variants, and asset URIs still depend on correct assembly names and `avares://` paths.
- **Custom controls**: controls with static initialization, native dependencies, platform checks, or assumptions about a full navigation shell may behave differently in an isolated preview window.
- **Generated code assumptions**: partial classes, `InitializeComponent`, generated name fields, and code-behind assumptions require the AXAML local assembly to be resolved correctly. Multi-assembly apps should pass the desktop host project as `projectPath`; the previewer will locate the AXAML class in the referenced output assemblies.

## Troubleshooting

| Symptom | What to Check |
|---|---|
| Type resolution fails for `x:Class` or a custom control | Build the target project first, use `projectPath` for host-project scenarios, and pass `entryType` when there are multiple `BuildAvaloniaApp` candidates. Confirm the AXAML file's namespace matches the compiled UI assembly. |
| A resource, style, or asset is missing | Verify the compiled app registers the dictionary in `App.axaml`, uses the expected assembly name in `avares://` URIs, and copies referenced assets to output. If the resource is local to another assembly, confirm that assembly is referenced by the host output. |
| Compiled binding errors appear only in preview | Treat the preview as runtime XAML loading. Check `x:DataType`, namespaces, and design data, then verify the compiled app build separately before deciding it is an application bug. |
| A custom control fails during startup | Look for native runtime assets, platform-specific checks, static constructors, background services, and production startup work. The preview host runs in a desktop process and loads the view in isolation. |
| The preview shows design data but the app does not | `Design.DataContext` is intentionally applied to the root in preview. Reproduce with the real app flow when debugging runtime ViewModel creation or dependency injection. |

When in doubt, compare three signals: `dotnet build` of the target app, `preview_axaml` output details, and an MCP snapshot from the real running app. Differences between those signals usually identify whether the issue is in the app, in runtime loader compatibility, or in preview host setup.
