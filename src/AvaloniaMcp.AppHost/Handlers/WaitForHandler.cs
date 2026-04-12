using System.Diagnostics;
using System.Text.Json;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using AvaloniaMcp.Protocol;
using AvaloniaMcp.Protocol.Messages;

namespace AvaloniaMcp.AppHost.Handlers;

public sealed class WaitForHandler : IRequestHandler
{
    public string Method => ProtocolMethods.WaitFor;

    public async Task<object> Handle(DiagnosticRequest request)
    {
        var query = "";
        var condition = "exists";
        string? value = null;
        var timeoutMs = 5000;

        if (request.Params is JsonElement p)
        {
            if (p.TryGetProperty("query", out var q)) query = q.GetString() ?? "";
            if (p.TryGetProperty("condition", out var c)) condition = c.GetString() ?? "exists";
            if (p.TryGetProperty("value", out var v)) value = v.GetString();
            if (p.TryGetProperty("timeoutMs", out var t)) timeoutMs = t.GetInt32();
        }

        timeoutMs = Math.Clamp(timeoutMs, 100, 30000);

        return await PollUntilCondition(query, condition, value, timeoutMs)
            ?? new { success = false, error = $"Timeout after {timeoutMs}ms", condition, query };
    }

    public static async Task<object?> PollUntilCondition(string query, string condition, string? value, int timeoutMs)
    {
        var sw = Stopwatch.StartNew();

        while (sw.ElapsedMilliseconds < timeoutMs)
        {
            var result = await Dispatcher.UIThread.InvokeAsync(() =>
                EvaluateCondition(query, condition, value, (int)sw.ElapsedMilliseconds));

            if (result is not null)
                return result;

            await Task.Delay(100);
        }

        return null;
    }

    private static object? EvaluateCondition(string query, string condition, string? value, int elapsedMs)
    {
        var matches = FindMatches(query);

        switch (condition.ToLowerInvariant())
        {
            case "exists":
                if (matches.Count > 0)
                    return new { success = true, elapsed_ms = elapsedMs, matchCount = matches.Count };
                break;

            case "not_exists":
                if (matches.Count == 0)
                    return new { success = true, elapsed_ms = elapsedMs, matchCount = 0 };
                break;

            case "visible":
                var visibleCount = matches.Count(v => v.IsVisible);
                if (visibleCount > 0)
                    return new { success = true, elapsed_ms = elapsedMs, matchCount = visibleCount };
                break;

            case "enabled":
                var enabledCount = matches.Count(v => v is InputElement { IsEnabled: true });
                if (enabledCount > 0)
                    return new { success = true, elapsed_ms = elapsedMs, matchCount = enabledCount };
                break;

            case "text_equals":
                var textEqualsCount = matches.Count(v => GetVisualText(v)?.Equals(value, StringComparison.Ordinal) == true);
                if (textEqualsCount > 0)
                    return new { success = true, elapsed_ms = elapsedMs, matchCount = textEqualsCount };
                break;

            case "text_contains":
                var textContainsCount = matches.Count(v =>
                    value is not null && GetVisualText(v)?.Contains(value, StringComparison.OrdinalIgnoreCase) == true);
                if (textContainsCount > 0)
                    return new { success = true, elapsed_ms = elapsedMs, matchCount = textContainsCount };
                break;

            case "count_equals":
                if (value is not null && int.TryParse(value, out var expected) && matches.Count == expected)
                    return new { success = true, elapsed_ms = elapsedMs, matchCount = matches.Count };
                break;
        }

        return null;
    }

    private static string? GetVisualText(Visual visual) => visual switch
    {
        TextBlock tb => tb.Text,
        TextBox tb => tb.Text,
        ContentControl cc when cc.Content is string s => s,
        _ => null,
    };

    private static List<Visual> FindMatches(string query)
    {
        var results = new List<Visual>();
        var visuals = NodeRegistry.GetWindows()
            .SelectMany(w => new[] { (Visual)w }.Concat(w.GetVisualDescendants()));

        foreach (var visual in visuals)
        {
            if (Matches(visual, query))
                results.Add(visual);
        }

        return results;
    }

    private static bool Matches(Visual visual, string query)
    {
        if (string.IsNullOrEmpty(query)) return true;

        var typeName = visual.GetType().Name;
        if (typeName.Contains(query, StringComparison.OrdinalIgnoreCase))
            return true;

        if (visual is Control control && control.Name is not null
            && control.Name.Contains(query, StringComparison.OrdinalIgnoreCase))
            return true;

        if (visual is TextBlock tb && tb.Text?.Contains(query, StringComparison.OrdinalIgnoreCase) == true)
            return true;

        if (visual is ContentControl cc && cc.Content is string s
            && s.Contains(query, StringComparison.OrdinalIgnoreCase))
            return true;

        if (visual is TextBox textBox && textBox.Text?.Contains(query, StringComparison.OrdinalIgnoreCase) == true)
            return true;

        return false;
    }
}
