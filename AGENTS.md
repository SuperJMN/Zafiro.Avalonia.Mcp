# Repository Guidelines

## Project Structure & Module Organization

This repository is a .NET solution for `Zafiro.Avalonia.Mcp`, an MCP bridge for inspecting and interacting with running Avalonia apps.

- `src/Zafiro.Avalonia.Mcp.Protocol`: shared request, response, and model types plus JSON serialization.
- `src/Zafiro.Avalonia.Mcp.AppHost`: in-app Avalonia diagnostics host, discovery, node registry, and request handlers.
- `src/Zafiro.Avalonia.Mcp.Tool`: stdio MCP tool and client-side connection/tool wrappers.
- `test/Zafiro.Avalonia.Mcp.Tests`: xUnit tests, including protocol and handler coverage.
- `samples/SampleApp`: Avalonia sample app using the AppHost.
- `tools/`: helper scripts, such as `pipe_client.py`.

## Build, Test, and Development Commands

- `dotnet restore Zafiro.Avalonia.Mcp.slnx`: restore NuGet dependencies.
- `dotnet build Zafiro.Avalonia.Mcp.slnx`: build all source, sample, and test projects.
- `dotnet test test/Zafiro.Avalonia.Mcp.Tests/Zafiro.Avalonia.Mcp.Tests.csproj`: run the test suite.
- `dotnet run --project samples/SampleApp/SampleApp.csproj`: run the sample Avalonia app for manual MCP diagnostics checks.
- `dotnet run --project src/Zafiro.Avalonia.Mcp.Tool/Zafiro.Avalonia.Mcp.Tool.csproj`: run the MCP tool from source.

## Coding Style & Naming Conventions

The solution uses C# with nullable reference types, implicit usings, and `LangVersion=latest` from `Directory.Build.props`. Keep code idiomatic C#: four-space indentation, PascalCase for public types and members, camelCase for locals and parameters, and `_camelCase` for private fields. Prefer small request handlers with one clear MCP method responsibility. Keep protocol DTOs simple and serialization-friendly.

## Testing Guidelines

Tests use xUnit and live under `test/Zafiro.Avalonia.Mcp.Tests`. Name test classes after the subject, and use descriptive fact names such as `NodeInfo_AllFields_RoundTrips` or `Resolve_ReturnsNull_ForUnknownId`. For Avalonia UI objects, use the existing `AvaloniaTestFixture` and `[Collection("Avalonia")]` pattern. Add focused tests for protocol compatibility, handler behavior, and regressions before changing public MCP tool behavior.

## Commit & Pull Request Guidelines

Recent history follows Conventional Commits, for example `feat: add tap interaction tool`, `fix: propagate actual error messages from MCP tools to the AI client`, and `docs: clarify dnx auto-update behavior`. Use that format and include issue or PR references when relevant.

Pull requests should include a concise summary, linked issues, test results, and screenshots or short recordings when UI behavior changes. Call out compatibility impacts for Avalonia versions, target frameworks, protocol fields, or MCP tool names.

## Security & Configuration Tips

Do not commit `.env`, local secrets, or machine-specific MCP client settings. Discovery files are runtime artifacts under the temp directory and should not be versioned.

When inspecting Zafiro internals, use the local source trees instead of guessing: `/mnt/fast/Repos/Zafiro` and `/mnt/fast/Repos/Zafiro.Avalonia`.
