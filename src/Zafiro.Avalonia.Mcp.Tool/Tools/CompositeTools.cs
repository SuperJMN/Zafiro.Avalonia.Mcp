using System.ComponentModel;
using System.Text.Json;
using ModelContextProtocol.Server;
using Zafiro.Avalonia.Mcp.Protocol;
using Zafiro.Avalonia.Mcp.Tool.Connection;

namespace Zafiro.Avalonia.Mcp.Tool.Tools;

[McpServerToolType]
public sealed class CompositeTools
{
    [McpServerTool(Name = "fill_form"), Description("""
        Fill multiple form fields in a single round-trip and (optionally) click a submit selector at the end. Per-field errors do NOT abort the batch — you get a complete diagnostic of which fields succeeded and which failed.

        Each field MUST set exactly one of: value (string→TextBox), checked (bool→CheckBox/RadioButton/ToggleSwitch), select (string-by-text or int-by-index→ListBox/ComboBox/TabControl), number (double→Slider/NumericUpDown/ProgressBar). Set "secret": true to redact the echoed value in the response.

        Request JSON shape:
        {
          "fields": [
            { "selector": "TextBox#Email", "value": "user@example.com" },
            { "selector": "TextBox#Password", "value": "hunter2", "secret": true },
            { "selector": "CheckBox#Remember", "checked": true },
            { "selector": "ComboBox#Country", "select": "Spain" },
            { "selector": "Slider#Volume", "number": 75 }
          ],
          "submit": "Button:has-text('Sign in')"
        }

        Returns: {results:[{selector, ok, nodeId?, applied?, errorInfo?}], successCount, failureCount, submit?:{ok, errorInfo?}}.
        """)]
    public static async Task<string> FillForm(
        ConnectionPool pool,
        [Description("JSON object: { \"fields\": [ {selector, value|checked|select|number, secret?} ], \"submit\"?: \"selector\" }")]
        string request)
    {
        var conn = pool.GetActive();
        using var doc = JsonDocument.Parse(request);
        return await conn.InvokeAsync(ProtocolMethods.FillForm, doc.RootElement.Clone());
    }
}
