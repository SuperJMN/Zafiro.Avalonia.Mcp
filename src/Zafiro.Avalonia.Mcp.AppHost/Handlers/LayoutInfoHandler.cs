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
            return HandlerResult.Error(
                DiagnosticErrorCodes.NoMatch,
                $"No element matched selector '{selector}'",
                "Check the selector or call get_snapshot to inspect available elements.",
                new { selector });

        var visual = matches[0];
        var matchedCount = matches.Count;
        return (object)BuildResult(visual, matchedCount > 1 ? matchedCount : (int?)null);
    }

    /// <summary>
    /// Builds the layout snapshot for a visual. Can be called directly in tests without a dispatcher.
    /// </summary>
    /// <summary>
    /// <c>rootBounds</c> are device-independent pixels relative to the containing window or popup
    /// (<c>root</c>); they are only comparable between elements of the same root. <c>screenBounds</c>
    /// are physical screen pixels, comparable across windows and popups.
    /// </summary>
    private static (object? root, object? rootBounds, object? screenBounds) GetPlacement(Visual visual, Rect bounds)
    {
        var topLevel = TopLevel.GetTopLevel(visual);
        if (topLevel is null || visual.TransformToVisual(topLevel) is not { } transform)
            return (null, null, null);

        var local = new Rect(bounds.Size).TransformToAABB(transform);
        var root = new
        {
            nodeId = NodeRegistry.GetOrRegister(topLevel),
            type = topLevel.GetType().Name,
            renderScaling = topLevel.RenderScaling,
        };
        var rootBounds = new { x = local.X, y = local.Y, w = local.Width, h = local.Height, unit = "dip" };

        object? screenBounds = null;
        try
        {
            var topLeft = topLevel.PointToScreen(local.TopLeft);
            var bottomRight = topLevel.PointToScreen(local.BottomRight);
            screenBounds = new
            {
                x = topLeft.X,
                y = topLeft.Y,
                w = bottomRight.X - topLeft.X,
                h = bottomRight.Y - topLeft.Y,
                unit = "px",
            };
        }
        catch
        {
            // Platforms without screen placement still get root-relative bounds.
        }

        return (root, rootBounds, screenBounds);
    }

    internal static object BuildResult(Visual visual, int? matchedCount = null)
    {
        var bounds = visual.Bounds;
            var (root, rootBounds, screenBounds) = GetPlacement(visual, bounds);

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
                isEnabled = ie.IsEffectivelyEnabled;
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
                root,
                rootBounds,
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
