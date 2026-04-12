using Avalonia;
using Avalonia.Media.Imaging;

namespace AvaloniaMcp.AppHost.Handlers;

/// <summary>
/// Minimal GIF89a encoder for animated screenshots.
/// Produces uncompressed GIF frames using LZW minimum code size trick.
/// </summary>
public static class GifEncoder
{
    public static byte[] Encode(IReadOnlyList<RenderTargetBitmap> frames, int delayMs)
    {
        if (frames.Count == 0) return [];

        using var ms = new MemoryStream();
        using var writer = new BinaryWriter(ms);

        var firstFrame = frames[0];
        var width = (ushort)firstFrame.PixelSize.Width;
        var height = (ushort)firstFrame.PixelSize.Height;

        // GIF Header
        writer.Write("GIF89a"u8);
        writer.Write(width);
        writer.Write(height);
        writer.Write((byte)0x70); // No GCT, 8 bits color resolution
        writer.Write((byte)0);    // Background color index
        writer.Write((byte)0);    // Pixel aspect ratio

        // Netscape extension for looping
        writer.Write((byte)0x21); // Extension
        writer.Write((byte)0xFF); // Application Extension
        writer.Write((byte)11);   // Block size
        writer.Write("NETSCAPE2.0"u8);
        writer.Write((byte)3);    // Sub-block size
        writer.Write((byte)1);    // Loop sub-block id
        writer.Write((ushort)0);  // Loop count (0 = infinite)
        writer.Write((byte)0);    // Block terminator

        var delayCs = (ushort)(delayMs / 10); // Convert ms to centiseconds

        foreach (var frame in frames)
        {
            var pixels = GetPixelData(frame);

            // Build quantized palette (simple median cut → 256 colors)
            var (palette, indexed) = QuantizeFrame(pixels, width, height);

            // Graphic Control Extension
            writer.Write((byte)0x21); // Extension
            writer.Write((byte)0xF9); // GCE
            writer.Write((byte)4);    // Block size
            writer.Write((byte)0x08); // Dispose: restore to background (avoids frame accumulation)
            writer.Write(delayCs);    // Delay
            writer.Write((byte)0);    // Transparent color index
            writer.Write((byte)0);    // Block terminator

            // Image Descriptor
            writer.Write((byte)0x2C); // Image separator
            writer.Write((ushort)0);  // Left
            writer.Write((ushort)0);  // Top
            writer.Write(width);
            writer.Write(height);
            writer.Write((byte)0x87); // Local color table, 256 entries (2^(7+1))

            // Local Color Table (256 × 3 bytes)
            for (int i = 0; i < 256; i++)
            {
                if (i < palette.Length)
                {
                    writer.Write(palette[i].R);
                    writer.Write(palette[i].G);
                    writer.Write(palette[i].B);
                }
                else
                {
                    writer.Write((byte)0);
                    writer.Write((byte)0);
                    writer.Write((byte)0);
                }
            }

            // LZW compressed image data
            WriteLzwData(writer, indexed, 8);
        }

        // GIF Trailer
        writer.Write((byte)0x3B);

        return ms.ToArray();
    }

    private static byte[] GetPixelData(RenderTargetBitmap bitmap)
    {
        // Save to PNG and re-read as WriteableBitmap for pixel access
        using var pngStream = new MemoryStream();
        bitmap.Save(pngStream);
        pngStream.Position = 0;

        var size = bitmap.PixelSize;
        var stride = size.Width * 4;
        var totalBytes = stride * size.Height;
        var buffer = new byte[totalBytes];

        using var writeable = WriteableBitmap.Decode(pngStream);
        using var fb = writeable.Lock();
        unsafe
        {
            var src = (byte*)fb.Address;
            for (int row = 0; row < size.Height; row++)
            {
                var srcOff = row * fb.RowBytes;
                var dstOff = row * stride;
                var len = Math.Min(stride, fb.RowBytes);
                for (int i = 0; i < len; i++)
                    buffer[dstOff + i] = src[srcOff + i];
            }
        }

        return buffer;
    }

    private readonly record struct Rgb(byte R, byte G, byte B);

    private static (Rgb[] palette, byte[] indexed) QuantizeFrame(byte[] pixels, int width, int height)
    {
        var totalPixels = width * height;
        var indexed = new byte[totalPixels];

        // Simple uniform quantization: 8 levels per channel (8×8×4 = 256)
        var palette = new Rgb[256];
        for (int i = 0; i < 256; i++)
        {
            int r = (i >> 5) & 0x07;
            int g = (i >> 2) & 0x07;
            int b = i & 0x03;
            palette[i] = new Rgb(
                (byte)(r * 255 / 7),
                (byte)(g * 255 / 7),
                (byte)(b * 255 / 3));
        }

        for (int i = 0; i < totalPixels; i++)
        {
            var offset = i * 4;
            byte pb = pixels[offset];
            byte pg = pixels[offset + 1];
            byte pr = pixels[offset + 2];

            int ri = (pr * 7 / 255) & 0x07;
            int gi = (pg * 7 / 255) & 0x07;
            int bi = (pb * 3 / 255) & 0x03;
            indexed[i] = (byte)((ri << 5) | (gi << 2) | bi);
        }

        return (palette, indexed);
    }

    private static void WriteLzwData(BinaryWriter writer, byte[] indexed, int minCodeSize)
    {
        writer.Write((byte)minCodeSize);

        // Simple LZW encoder
        var clearCode = 1 << minCodeSize;
        var eoiCode = clearCode + 1;

        using var dataStream = new MemoryStream();
        var bitWriter = new LzwBitWriter(dataStream);

        var codeSize = minCodeSize + 1;
        var nextCode = eoiCode + 1;
        var maxCode = (1 << codeSize) - 1;

        // Initialize table with single-character strings
        var table = new Dictionary<(int prefix, byte suffix), int>();

        bitWriter.WriteBits(clearCode, codeSize);

        if (indexed.Length == 0)
        {
            bitWriter.WriteBits(eoiCode, codeSize);
            bitWriter.Flush();
            WriteSubBlocks(writer, dataStream.ToArray());
            return;
        }

        int current = indexed[0];

        for (int i = 1; i < indexed.Length; i++)
        {
            var next = indexed[i];
            var key = (current, next);

            if (table.TryGetValue(key, out var code))
            {
                current = code;
            }
            else
            {
                bitWriter.WriteBits(current, codeSize);

                if (nextCode <= 4095)
                {
                    table[key] = nextCode++;
                    if (nextCode > maxCode && codeSize < 12)
                    {
                        codeSize++;
                        maxCode = (1 << codeSize) - 1;
                    }
                }
                else
                {
                    // Table full, emit clear code
                    bitWriter.WriteBits(clearCode, codeSize);
                    table.Clear();
                    codeSize = minCodeSize + 1;
                    nextCode = eoiCode + 1;
                    maxCode = (1 << codeSize) - 1;
                }

                current = next;
            }
        }

        bitWriter.WriteBits(current, codeSize);
        bitWriter.WriteBits(eoiCode, codeSize);
        bitWriter.Flush();

        WriteSubBlocks(writer, dataStream.ToArray());
    }

    private static void WriteSubBlocks(BinaryWriter writer, byte[] data)
    {
        int offset = 0;
        while (offset < data.Length)
        {
            var blockSize = Math.Min(255, data.Length - offset);
            writer.Write((byte)blockSize);
            writer.Write(data.AsSpan(offset, blockSize));
            offset += blockSize;
        }
        writer.Write((byte)0); // Block terminator
    }

    private sealed class LzwBitWriter
    {
        private readonly Stream _stream;
        private int _buffer;
        private int _bitsInBuffer;

        public LzwBitWriter(Stream stream)
        {
            _stream = stream;
        }

        public void WriteBits(int value, int bits)
        {
            _buffer |= value << _bitsInBuffer;
            _bitsInBuffer += bits;

            while (_bitsInBuffer >= 8)
            {
                _stream.WriteByte((byte)(_buffer & 0xFF));
                _buffer >>= 8;
                _bitsInBuffer -= 8;
            }
        }

        public void Flush()
        {
            if (_bitsInBuffer > 0)
            {
                _stream.WriteByte((byte)(_buffer & 0xFF));
                _buffer = 0;
                _bitsInBuffer = 0;
            }
        }
    }
}
