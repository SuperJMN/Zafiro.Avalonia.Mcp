using System.ComponentModel;
using System.Globalization;
using System.Reflection;
using System.Text;
using ModelContextProtocol.Server;
using Zafiro.Avalonia.Mcp.Protocol.Messages;

namespace Zafiro.Avalonia.Mcp.Tool.Tools;

/// <summary>
/// Builds, by reflection, a markdown catalogue of every <see cref="McpServerToolAttribute"/>-decorated
/// method exposed by this assembly. Used by the <c>instructions(page='tools')</c> entry point so AI
/// agents always see the canonical tool surface and stop hallucinating tool names.
/// </summary>
internal static class ToolsCatalogue
{
    private const string ToolNamePrefix = "zafiro-avalonia-mcp-";

    private static readonly Lazy<string> CachedMarkdown = new(BuildMarkdown);
    private static readonly Lazy<IReadOnlyList<ToolEntry>> CachedTools = new(DiscoverTools);

    public static string GetMarkdown() => CachedMarkdown.Value;

    public static IReadOnlyList<ToolEntry> GetTools() => CachedTools.Value;

    internal sealed record ParameterInfoEntry(string Name, string Type, bool Required, string? Description);

    internal sealed record ToolEntry(
        string Name,
        string PrefixedName,
        string Purpose,
        IReadOnlyList<ParameterInfoEntry> Parameters);

    private static IReadOnlyList<ToolEntry> DiscoverTools()
    {
        var assembly = typeof(ToolsCatalogue).Assembly;
        var entries = new List<ToolEntry>();

        foreach (var type in assembly.GetTypes())
        {
            if (type.GetCustomAttribute<McpServerToolTypeAttribute>() is null)
                continue;

            foreach (var method in type.GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance))
            {
                var toolAttr = method.GetCustomAttribute<McpServerToolAttribute>();
                if (toolAttr is null)
                    continue;

                var name = !string.IsNullOrWhiteSpace(toolAttr.Name)
                    ? toolAttr.Name!
                    : ToKebabCase(method.Name);

                var purpose = ExtractFirstLine(method.GetCustomAttribute<DescriptionAttribute>()?.Description);

                var parameters = method.GetParameters()
                    .Where(p => !IsInfrastructureParameter(p))
                    .Select(p => new ParameterInfoEntry(
                        p.Name ?? "?",
                        FormatType(p.ParameterType),
                        Required: !p.HasDefaultValue && !IsNullable(p),
                        p.GetCustomAttribute<DescriptionAttribute>()?.Description))
                    .ToList();

                entries.Add(new ToolEntry(name, ToolNamePrefix + name, purpose, parameters));
            }
        }

        return entries
            .OrderBy(e => e.Name, StringComparer.Ordinal)
            .ToList();
    }

    private static bool IsInfrastructureParameter(ParameterInfo p)
    {
        var t = p.ParameterType;
        // Skip DI-injected services and framework context (e.g. ConnectionPool, IMcpServer, CancellationToken).
        if (t == typeof(CancellationToken)) return true;
        var ns = t.Namespace ?? string.Empty;
        if (ns.StartsWith("ModelContextProtocol", StringComparison.Ordinal)) return true;
        if (ns.StartsWith("Microsoft.Extensions", StringComparison.Ordinal)) return true;
        if (t.Name.EndsWith("Pool", StringComparison.Ordinal)) return true;
        return false;
    }

    private static bool IsNullable(ParameterInfo p)
    {
        if (p.ParameterType.IsValueType)
            return Nullable.GetUnderlyingType(p.ParameterType) is not null;
        var ctx = new NullabilityInfoContext();
        return ctx.Create(p).WriteState == NullabilityState.Nullable;
    }

    private static string FormatType(Type t)
    {
        var underlying = Nullable.GetUnderlyingType(t);
        if (underlying is not null) return FormatType(underlying) + "?";
        return t switch
        {
            _ when t == typeof(string) => "string",
            _ when t == typeof(int) => "int",
            _ when t == typeof(long) => "long",
            _ when t == typeof(bool) => "bool",
            _ when t == typeof(double) => "double",
            _ when t == typeof(float) => "float",
            _ => t.Name
        };
    }

    private static string ExtractFirstLine(string? description)
    {
        if (string.IsNullOrWhiteSpace(description)) return string.Empty;
        var idx = description.IndexOf('\n');
        var first = (idx >= 0 ? description[..idx] : description).Trim();
        return first;
    }

    private static string ToKebabCase(string s)
    {
        if (string.IsNullOrEmpty(s)) return s;
        var sb = new StringBuilder(s.Length + 8);
        for (int i = 0; i < s.Length; i++)
        {
            var c = s[i];
            if (char.IsUpper(c))
            {
                if (i > 0) sb.Append('-');
                sb.Append(char.ToLower(c, CultureInfo.InvariantCulture));
            }
            else sb.Append(c);
        }
        return sb.ToString();
    }

    private static string BuildMarkdown()
    {
        var tools = GetTools();
        var byName = tools.ToDictionary(t => t.Name, StringComparer.Ordinal);
        var sb = new StringBuilder();

        sb.AppendLine("# Avalonia MCP — Tool Catalogue");
        sb.AppendLine();
        sb.AppendLine($"<!-- Generated at session start from {tools.Count} registered tools — never edit by hand. -->");
        sb.AppendLine();
        sb.AppendLine("These are the **exact** tool names the MCP framework exposes. If you call a tool ");
        sb.AppendLine("not in this list, it does not exist — re-read this page instead of guessing.");
        sb.AppendLine();

        sb.AppendLine("## 1. Tools");
        sb.AppendLine();
        sb.AppendLine("| Tool name | Purpose | Required params (with types) |");
        sb.AppendLine("|---|---|---|");
        foreach (var t in tools)
        {
            var required = t.Parameters.Where(p => p.Required).ToList();
            var paramText = required.Count == 0
                ? "_(none)_"
                : string.Join(", ", required.Select(p => $"`{p.Name}: {p.Type}`"));
            sb.Append("| `").Append(t.PrefixedName).Append("` | ")
              .Append(EscapePipe(t.Purpose)).Append(" | ")
              .Append(paramText).AppendLine(" |");
        }
        sb.AppendLine();

        sb.AppendLine("## 2. Common hallucinations — Don't say → Say");
        sb.AppendLine();
        sb.AppendLine("These are tool names AI agents frequently invent. Use the right column instead.");
        sb.AppendLine();
        sb.AppendLine("| Don't say | Say |");
        sb.AppendLine("|---|---|");
        foreach (var (wrong, candidates) in Hallucinations)
        {
            var resolved = candidates
                .Select(c => byName.ContainsKey(c) ? $"`{c}`" : null)
                .Where(c => c is not null)
                .ToList();
            string right = resolved.Count > 0
                ? string.Join(" or ", resolved)
                : $"NOTE: re-check this table — no canonical match for `{string.Join(" / ", candidates)}` was found in the live catalogue above.";
            sb.Append("| `").Append(wrong).Append("` | ").Append(right).AppendLine(" |");
        }
        sb.AppendLine();

        sb.AppendLine("## 3. Selector quick-reference");
        sb.AppendLine();
        sb.AppendLine("Since the v2.0.0 breaking migration (Phase 6.2), `selector` is the universal first ");
        sb.AppendLine("parameter of every action and read tool. NodeIds returned by previous calls can still ");
        sb.AppendLine("be passed embedded as `#42`.");
        sb.AppendLine();
        sb.AppendLine("```");
        sb.AppendLine("TextBox#42                          → by nodeId");
        sb.AppendLine("TextBox#Username                    → by x:Name");
        sb.AppendLine("Button:has-text(\"Sign in\")          → by visible text");
        sb.AppendLine("Button:enabled:visible              → by state pseudo-classes");
        sb.AppendLine("ListBoxItem[dc.Id=42]               → by DataContext property");
        sb.AppendLine("ListBoxItem[dc:'Id==42 && IsActive']→ by DataContext predicate (Roslyn)");
        sb.AppendLine("TabItem >> TextBox                  → descendant combinator");
        sb.AppendLine("Window > Grid > Button              → child combinator");
        sb.AppendLine("*[role=button]:nth(2)               → by role + 1-based index");
        sb.AppendLine("button:has-text(\"OK\"), Cancel       → comma = alternatives");
        sb.AppendLine("```");
        sb.AppendLine();

        sb.AppendLine("## 4. Standard error codes");
        sb.AppendLine();
        sb.AppendLine("Tool failures surface a structured `code` field. Recover as follows:");
        sb.AppendLine();
        sb.AppendLine("| Code | Typical recovery |");
        sb.AppendLine("|---|---|");
        foreach (var (code, hint) in GetErrorCodes())
        {
            sb.Append("| `").Append(code).Append("` | ").Append(hint).AppendLine(" |");
        }
        sb.AppendLine();

        sb.AppendLine("## 5. Recommended call order");
        sb.AppendLine();
        sb.AppendLine("- **\"What's on screen?\"** → `get_snapshot` (cheapest), then `get_screen_text` if you only need text.");
        sb.AppendLine("- **\"Click the X button\"** → `click_by_query` (atomic find+click — avoids stale-node races).");
        sb.AppendLine("- **\"Inspect a control\"** → `get_props` + `get_styles` + `get_layout_info`.");
        sb.AppendLine("- **\"Verify after action\"** → `wait_for` or `click_and_wait`. NEVER poll with `screenshot`.");
        sb.AppendLine();

        return sb.ToString();
    }

    private static string EscapePipe(string s) => s.Replace("|", "\\|");

    private static readonly (string Wrong, string[] Candidates)[] Hallucinations =
    [
        ("take_screenshot",  new[] { "screenshot" }),
        ("take_snapshot",    new[] { "get_snapshot" }),
        ("find_element",     new[] { "search", "click_by_query" }),
        ("query_selector",   new[] { "search", "click_by_query" }),
        ("get_text",         new[] { "get_screen_text" }),
        ("read_props",       new[] { "get_props" }),
        ("inspect_props",    new[] { "get_props" }),
        ("wait_visible",     new[] { "wait_for" }),
        ("wait_until",       new[] { "wait_for" }),
        ("subscribe_event",  new[] { "subscribe" }),
        ("poll_event",       new[] { "poll_events" }),
        ("tap_button",       new[] { "click", "tap" }),
        ("press_button",     new[] { "click", "tap" }),
        ("set_text",         new[] { "text_input" }),
        ("update_property",  new[] { "set_prop" }),
        ("set_property",     new[] { "set_prop" }),
    ];

    private static IEnumerable<(string Code, string Hint)> GetErrorCodes()
    {
        var hints = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [DiagnosticErrorCodes.NoMatch]             = "The selector matched nothing — re-call `get_snapshot` or relax the selector.",
            [DiagnosticErrorCodes.AmbiguousSelector]   = "Selector matched multiple nodes — add `:nth(N)` or a more specific predicate.",
            [DiagnosticErrorCodes.StaleNode]           = "NodeId no longer valid (tree changed) — re-resolve via `search` / `get_snapshot`.",
            [DiagnosticErrorCodes.InvalidParam]        = "Argument failed validation — re-read the tool's parameter list above.",
            [DiagnosticErrorCodes.InvalidSelector]     = "Selector failed to parse — see the selector cheat-sheet in section 3.",
            [DiagnosticErrorCodes.UnsupportedOperation]= "The control does not support this operation — pick a more specific tool (e.g. `toggle` for CheckBox).",
            [DiagnosticErrorCodes.Timeout]             = "The condition was not met within the timeout — increase `timeoutMs` or verify the precondition.",
            [DiagnosticErrorCodes.Internal]            = "Internal server error — retry once; if it persists, capture the error message and report it.",
        };

        var fields = typeof(DiagnosticErrorCodes)
            .GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(f => f.IsLiteral && f.FieldType == typeof(string));

        foreach (var f in fields)
        {
            var code = (string?)f.GetRawConstantValue();
            if (code is null) continue;
            yield return (code, hints.TryGetValue(code, out var hint) ? hint : "Re-check tool docs and arguments.");
        }
    }
}
