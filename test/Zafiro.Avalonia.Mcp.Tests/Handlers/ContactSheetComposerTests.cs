using Avalonia;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Xunit;
using Zafiro.Avalonia.Mcp.AppHost.Handlers;

namespace Zafiro.Avalonia.Mcp.Tests.Handlers;

[Collection("Avalonia")]
public class ContactSheetComposerTests
{
    private static readonly byte[] PngSignature = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];

    public ContactSheetComposerTests(AvaloniaTestFixture _)
    {
    }

    private static ContactSheetComposer.ContactSheet Compose(
        int frameCount,
        int frameWidth,
        int frameHeight,
        int frameDelayMs = 100,
        int maxCells = 9,
        int maxSheetDimension = 1024)
    {
        return Dispatcher.UIThread.Invoke(() =>
        {
            var frames = new List<RenderTargetBitmap>();
            for (var i = 0; i < frameCount; i++)
                frames.Add(new RenderTargetBitmap(new PixelSize(frameWidth, frameHeight)));

            try
            {
                return ContactSheetComposer.Compose(frames, frameDelayMs, maxCells, maxSheetDimension);
            }
            finally
            {
                foreach (var frame in frames)
                    frame.Dispose();
            }
        });
    }

    [Fact]
    public void Compose_ReturnsCoherentSheet()
    {
        var sheet = Compose(frameCount: 5, frameWidth: 320, frameHeight: 180);

        Assert.NotNull(sheet.Png);
        Assert.True(sheet.Width > 0);
        Assert.True(sheet.Height > 0);
        Assert.Equal(5, sheet.SampledFrames);
        Assert.True(sheet.Columns * sheet.Rows >= sheet.SampledFrames);

        // Pixel encoding depends on the rendering backend. The shared test fixture uses the
        // no-op headless drawing, where Save yields no bytes; a real backend (Skia in the app)
        // must produce a valid PNG.
        if (sheet.Png.Length > 0)
            Assert.Equal(PngSignature, sheet.Png.Take(PngSignature.Length).ToArray());
    }

    [Fact]
    public void Compose_BoundsLongestSideByMaxSheetDimension()
    {
        var sheet = Compose(frameCount: 12, frameWidth: 1920, frameHeight: 1080, maxCells: 9, maxSheetDimension: 512);

        Assert.True(Math.Max(sheet.Width, sheet.Height) <= 512,
            $"Sheet {sheet.Width}x{sheet.Height} exceeds the 512px cap.");
    }

    [Fact]
    public void Compose_SamplesDownToMaxCells()
    {
        var sheet = Compose(frameCount: 20, frameWidth: 200, frameHeight: 200, maxCells: 9);

        Assert.Equal(9, sheet.SampledFrames);
        Assert.Equal(3, sheet.Columns);
        Assert.Equal(3, sheet.Rows);
    }

    [Fact]
    public void Compose_FewerFramesThanMax_UsesAll()
    {
        var sheet = Compose(frameCount: 3, frameWidth: 200, frameHeight: 200, maxCells: 9);

        Assert.Equal(3, sheet.SampledFrames);
        Assert.Equal(2, sheet.Columns); // ceil(sqrt(3))
        Assert.Equal(2, sheet.Rows);    // ceil(3/2)
    }

    [Fact]
    public void Compose_SingleFrame_ProducesOneCell()
    {
        var sheet = Compose(frameCount: 1, frameWidth: 640, frameHeight: 480, maxSheetDimension: 512);

        Assert.Equal(1, sheet.SampledFrames);
        Assert.Equal(1, sheet.Columns);
        Assert.Equal(1, sheet.Rows);
        Assert.True(Math.Max(sheet.Width, sheet.Height) <= 512);
    }

    [Fact]
    public void Compose_EmptyList_Throws()
    {
        Assert.Throws<ArgumentException>(() =>
            Dispatcher.UIThread.Invoke(() =>
                ContactSheetComposer.Compose(new List<RenderTargetBitmap>(), frameDelayMs: 100)));
    }
}
