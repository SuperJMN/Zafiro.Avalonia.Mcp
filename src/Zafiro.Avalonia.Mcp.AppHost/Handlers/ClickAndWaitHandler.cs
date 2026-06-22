using System.Diagnostics;
using System.Text.Json;
using Avalonia.Threading;
using Zafiro.Avalonia.Mcp.AppHost.Selectors;
using Zafiro.Avalonia.Mcp.Protocol;
using Zafiro.Avalonia.Mcp.Protocol.Messages;

namespace Zafiro.Avalonia.Mcp.AppHost.Handlers;

public sealed class ClickAndWaitHandler : IRequestHandler
{
    public string Method => ProtocolMethods.ClickAndWait;

    public async Task<object> Handle(DiagnosticRequest request)
    {
        string? selector = null;
        var waitQuery = "";
        var waitCondition = "exists";
        string? waitValue = null;
        var timeoutMs = 5000;

        if (request.Params is JsonElement p)
        {
            if (p.TryGetProperty("selector", out var s)) selector = s.GetString();
            if (p.TryGetProperty("waitQuery", out var q)) waitQuery = q.GetString() ?? "";
            if (p.TryGetProperty("waitCondition", out var c)) waitCondition = c.GetString() ?? "exists";
            if (p.TryGetProperty("waitValue", out var v)) waitValue = v.GetString();
            if (p.TryGetProperty("timeoutMs", out var t)) timeoutMs = t.GetInt32();
        }

        timeoutMs = Math.Clamp(timeoutMs, 100, 30000);

        var sw = Stopwatch.StartNew();

        // Perform click on UI thread
        var clickResult = await Dispatcher.UIThread.InvokeAsync<object>(() =>
        {
            var (visual, error) = SelectorRequestHelper.ResolveSingle(selector);
            if (visual is null) return error!;

            return InputHandler.Click(visual);
        });

        // If click failed, return immediately
        if (clickResult is { } cr)
        {
            var json = System.Text.Json.JsonSerializer.Serialize(cr);
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.TryGetProperty("error", out _))
                return new { success = false, click_result = clickResult, elapsed_ms = (int)sw.ElapsedMilliseconds };
        }

        // Run the wait polling loop
        var waitResult = await WaitForHandler.PollUntilCondition(waitQuery, waitCondition, waitValue, timeoutMs);

        var totalElapsed = (int)sw.ElapsedMilliseconds;

        if (waitResult is not null)
            return new { success = true, click_result = clickResult, wait_result = waitResult, elapsed_ms = totalElapsed };

        return new
        {
            success = false,
            click_result = clickResult,
            wait_result = new { success = false, error = $"Timeout after {timeoutMs}ms", condition = waitCondition, query = waitQuery },
            elapsed_ms = totalElapsed
        };
    }
}
