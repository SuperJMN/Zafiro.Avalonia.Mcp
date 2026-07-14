using System.Globalization;
using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Imaging;

namespace Zafiro.Avalonia.Mcp.AppHost.Handlers;

/// <summary>
/// Composes captured frames into a single static "contact sheet" PNG: a labelled grid of
/// evenly sampled frames. This is the only artifact recording produces, because vision models
/// cannot process animated formats (an animated GIF is rejected by the client with
/// <c>400 Could not process image</c>). The output is a plain PNG, so it travels over the
/// protocol as base64 exactly like <see cref="ScreenshotHandler"/> and works on every transport
/// (local, SSH, Android TCP).
/// </summary>
/// <remarks>
/// Token cost is bounded and predictable, independent of the captured window size, by two knobs:
/// <c>maxCells</c> (how many frames appear in the grid) and <c>maxSheetDimension</c> (the sheet's
/// longest side in pixels). Must be invoked on the Avalonia UI thread.
/// </remarks>
public static class ContactSheetComposer
{
    private const int Padding = 6;
    private const int LabelHeight = 16;
    private const double LabelEmSize = 11;

    private static readonly IBrush SheetBackground = new SolidColorBrush(Color.FromRgb(0x2D, 0x2D, 0x30));
    private static readonly IBrush CellBackground = new SolidColorBrush(Color.FromRgb(0x1E, 0x1E, 0x1E));
    private static readonly IBrush LabelForeground = Brushes.White;

    public sealed record ContactSheet(byte[] Png, int Width, int Height, int Columns, int Rows, int SampledFrames);

    /// <summary>
    /// Builds the contact sheet. Call on the UI thread with at least one frame.
    /// </summary>
    /// <param name="frames">Captured frames in chronological order (all assumed same pixel size).</param>
    /// <param name="frameDelayMs">Delay between consecutive captured frames, used to label elapsed time.</param>
    /// <param name="maxCells">Maximum number of frames shown in the grid (uniformly sampled).</param>
    /// <param name="maxSheetDimension">Upper bound for the sheet's longest side, in pixels.</param>
    public static ContactSheet Compose(
        IReadOnlyList<RenderTargetBitmap> frames,
        int frameDelayMs,
        int maxCells = 9,
        int maxSheetDimension = 1024)
    {
        if (frames.Count == 0)
            throw new ArgumentException("At least one frame is required.", nameof(frames));

        maxCells = Math.Clamp(maxCells, 1, 64);
        maxSheetDimension = Math.Clamp(maxSheetDimension, 128, 4096);

        var sampled = SampleIndices(frames.Count, maxCells);
        var n = sampled.Length;

        var columns = (int)Math.Ceiling(Math.Sqrt(n));
        var rows = (int)Math.Ceiling((double)n / columns);

        var srcW = Math.Max(1, frames[sampled[0]].PixelSize.Width);
        var srcH = Math.Max(1, frames[sampled[0]].PixelSize.Height);
        var aspect = (double)srcH / srcW;

        var (cellW, cellH) = ComputeCellSize(columns, rows, srcW, aspect, maxSheetDimension);

        var sheetW = columns * cellW + (columns + 1) * Padding;
        var sheetH = rows * (cellH + LabelHeight) + (rows + 1) * Padding;

        var sheet = new RenderTargetBitmap(new PixelSize(sheetW, sheetH));
        using (var ctx = sheet.CreateDrawingContext())
        {
            ctx.FillRectangle(SheetBackground, new Rect(0, 0, sheetW, sheetH));

            using (ctx.PushRenderOptions(new RenderOptions { BitmapInterpolationMode = BitmapInterpolationMode.HighQuality }))
            {
                for (var i = 0; i < n; i++)
                {
                    var row = i / columns;
                    var col = i % columns;
                    var blockX = Padding + col * (cellW + Padding);
                    var blockY = Padding + row * (cellH + LabelHeight + Padding);

                    ctx.FillRectangle(CellBackground, new Rect(blockX, blockY, cellW, cellH + LabelHeight));
                    ctx.DrawImage(frames[sampled[i]], new Rect(blockX, blockY, cellW, cellH));
                }
            }

            for (var i = 0; i < n; i++)
            {
                var row = i / columns;
                var col = i % columns;
                var blockX = Padding + col * (cellW + Padding);
                var blockY = Padding + row * (cellH + LabelHeight + Padding);

                var elapsedMs = sampled[i] * frameDelayMs;
                try
                {
                    var label = FormatLabel(i + 1, n, elapsedMs, cellW);
                    ctx.DrawText(label, new Point(blockX + 3, blockY + cellH + 1));
                }
                catch (Exception ex)
                {
                    // Labels are cosmetic; never fail the whole capture over text shaping
                    // (e.g. a fontless headless environment).
                    System.Diagnostics.Trace.TraceWarning($"Contact sheet label rendering skipped: {ex.Message}");
                }
            }
        }

        using var ms = new MemoryStream();
        sheet.Save(ms);
        sheet.Dispose();

        return new ContactSheet(ms.ToArray(), sheetW, sheetH, columns, rows, n);
    }

    private static (int cellW, int cellH) ComputeCellSize(int columns, int rows, int srcW, double aspect, int maxDim)
    {
        var widthBound = (double)(maxDim - (columns + 1) * Padding) / columns;
        var heightBound = (maxDim - rows * LabelHeight - (rows + 1) * Padding) / (rows * aspect);

        var cellW = (int)Math.Floor(Math.Min(Math.Min(widthBound, heightBound), srcW));
        cellW = Math.Max(1, cellW);
        var cellH = Math.Max(1, (int)Math.Floor(cellW * aspect));
        return (cellW, cellH);
    }

    private static int[] SampleIndices(int count, int max)
    {
        if (count <= max)
            return Enumerable.Range(0, count).ToArray();
        if (max == 1)
            return [0];

        var result = new int[max];
        for (var i = 0; i < max; i++)
            result[i] = (int)Math.Round((double)i * (count - 1) / (max - 1));
        return result;
    }

    private static FormattedText FormatLabel(int position, int total, int elapsedMs, int maxWidth)
    {
        var text = new FormattedText(
            $"#{position}/{total} · {elapsedMs}ms",
            CultureInfo.InvariantCulture,
            FlowDirection.LeftToRight,
            Typeface.Default,
            LabelEmSize,
            LabelForeground)
        {
            MaxTextWidth = Math.Max(1, maxWidth - 6),
            MaxLineCount = 1,
            Trimming = TextTrimming.CharacterEllipsis
        };
        return text;
    }
}
