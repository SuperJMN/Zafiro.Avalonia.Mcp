using System.Text.Json;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Zafiro.Avalonia.Mcp.AppHost.Selectors;
using Zafiro.Avalonia.Mcp.Protocol;
using Zafiro.Avalonia.Mcp.Protocol.Messages;

namespace Zafiro.Avalonia.Mcp.AppHost.Handlers;

/// <summary>
/// Returns a comprehensive layout snapshot for a control identified by selector.
/// Useful for diagnosing visibility, sizing, and alignment issues.
/// </summary>
public sealed class LayoutInfoHandler : IRequestHandler
{
    public string Method => ProtocolMethods.GetLayoutInfo;

    public Task<object> Handle(DiagnosticRequest request)
    {
        string selector = "";
        if (request.Params is JsonElement p && p.TryGetProperty("selector", out var s))
            selector = s.GetString() ?? "";

        if (Dispatcher.UIThread.CheckAccess())
            return Task.FromResult(Resolve(selector));

        return Dispatcher.UIThread.InvokeAsync<object>(() => Resolve(selector)).GetTask();
    }

    private static object Resolve(string selector)
    {
        var engine = new SelectorEngine();
        var matches = engine.Resolve(selector);
        if (matches.Count == 0)
            return new { error = $"No element matched selector '{selector}'" };

        var visual = matches[0];
        var matchedCount = matches.Count;
        return (object)BuildResult(visual, matchedCount > 1 ? matchedCount : (int?)null);
    }

    /// <summary>
    /// Builds the layout snapshot for a visual. Can be called directly in tests without a dispatcher.
    /// </summary>
    internal static object BuildResult(Visual visual, int? matchedCount = null)
    {
        var bounds = visual.Bounds;
            object? screenBounds = null;
            try
            {
                var topLevel = TopLevel.GetTopLevel(visual as Control);
                if (topLevel is not null)
                {
                    var transform = visual.TransformToVisual(topLevel);
                    if (transform.HasValue)
                    {
                        var origin = transform.Value.Transform(new Point(0, 0));
                        screenBounds = new
                        {
                            x = origin.X,
                            y = origin.Y,
                            w = bounds.Width,
                            h = bounds.Height
                        };
                    }
                }
            }
            catch { }

            object? margin = null;
            object? padding = null;
            double? width = null;
            double? height = null;
            double? minWidth = null;
            double? minHeight = null;
            double? maxWidth = null;
            double? maxHeight = null;
            string? horizontalAlignment = null;
            string? verticalAlignment = null;
            string? horizontalContentAlignment = null;
            string? verticalContentAlignment = null;
            bool? isMeasureValid = null;
            bool? isArrangeValid = null;
            bool? clipToBounds = null;

            if (visual is Layoutable layoutable)
            {
                var m = layoutable.Margin;
                margin = new { l = m.Left, t = m.Top, r = m.Right, b = m.Bottom };
                width = double.IsNaN(layoutable.Width) ? null : layoutable.Width;
                height = double.IsNaN(layoutable.Height) ? null : layoutable.Height;
                minWidth = double.IsNaN(layoutable.MinWidth) ? null : layoutable.MinWidth;
                minHeight = double.IsNaN(layoutable.MinHeight) ? null : layoutable.MinHeight;
                maxWidth = double.IsInfinity(layoutable.MaxWidth) ? null : layoutable.MaxWidth;
                maxHeight = double.IsInfinity(layoutable.MaxHeight) ? null : layoutable.MaxHeight;
                horizontalAlignment = layoutable.HorizontalAlignment.ToString();
                verticalAlignment = layoutable.VerticalAlignment.ToString();
                isMeasureValid = layoutable.IsMeasureValid;
                isArrangeValid = layoutable.IsArrangeValid;
            }

            if (visual is Control ctrl)
            {
                clipToBounds = ctrl.ClipToBounds;
            }

            if (visual is Decorator decorator)
            {
                var pad = decorator.Padding;
                padding = new { l = pad.Left, t = pad.Top, r = pad.Right, b = pad.Bottom };
            }
            else if (visual is TemplatedControl tc)
            {
                var pad = tc.Padding;
                padding = new { l = pad.Left, t = pad.Top, r = pad.Right, b = pad.Bottom };
            }

            if (visual is ContentControl cc)
            {
                horizontalContentAlignment = cc.HorizontalContentAlignment.ToString();
                verticalContentAlignment = cc.VerticalContentAlignment.ToString();
            }

            bool isEffectivelyVisible = true;
            Visual? cur = visual;
            while (cur is not null)
            {
                if (!cur.IsVisible) { isEffectivelyVisible = false; break; }
                cur = cur.GetVisualParent();
            }

            bool isEnabled = true;
            bool isHitTestVisible = true;
            if (visual is InputElement ie)
            {
                isEnabled = ie.IsEnabled;
                isHitTestVisible = ie.IsHitTestVisible;
            }

            var parent = visual.GetVisualParent();
            object? parentInfo = null;
            if (parent is not null)
                parentInfo = new { nodeId = NodeRegistry.GetOrRegister(parent), type = parent.GetType().Name };

            var desiredSizeVal = visual is Layoutable lForDesired
                ? new { w = lForDesired.DesiredSize.Width, h = lForDesired.DesiredSize.Height }
                : new { w = 0.0, h = 0.0 };

            return new
            {
                matchedCount,
                bounds = new { x = bounds.X, y = bounds.Y, w = bounds.Width, h = bounds.Height },
                screenBounds,
                desiredSize = desiredSizeVal,
                renderSize = new { w = bounds.Width, h = bounds.Height },
                margin,
                padding,
                horizontalAlignment,
                verticalAlignment,
                horizontalContentAlignment,
                verticalContentAlignment,
                width,
                height,
                minWidth,
                minHeight,
                maxWidth,
                maxHeight,
                isVisible = visual.IsVisible,
                isEffectivelyVisible,
                isEnabled,
                isHitTestVisible,
                clipToBounds,
                isMeasureValid,
                isArrangeValid,
                parent = parentInfo
        };
    }
}
