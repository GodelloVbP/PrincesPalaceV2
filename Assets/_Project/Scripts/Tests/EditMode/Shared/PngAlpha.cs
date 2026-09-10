using System;
using System.IO;
using System.IO.Compression;

namespace PrincesPalace.Domain.Tests
{
    // JUST THE ALPHA CHANNEL OF A PNG, decoded without an engine.
    //
    // WHY THIS EXISTS AT ALL. StanceManifestValidationTests has to re-measure
    // where a figure's feet sit inside its own canvas, which means reading the
    // committed stills. Unity would hand them over as Sprites -- but a test
    // that needs Unity to read a file is a test that costs 20s of editor boot
    // instead of 4s of `dotnet test`, and this suite's whole fast loop is the
    // EditMode half running without one. Every stance PNG in this project is
    // written by Pillow: 8-bit, colour type 6 (RGBA), non-interlaced. That is
    // the only shape supported here, and anything else throws by name rather
    // than being guessed at.
    //
    // ONLY ALPHA IS RETURNED BY DEFAULT. Almost nothing that reads this
    // cares what colour a pixel is -- the questions are nearly all "is there
    // art here", so carrying the RGB planes around would be three quarters of
    // the memory for none of the answers, across a whole roster of stills.
    //
    // ONE READER DOES CARE, and asks for it explicitly: PcPlateArtTests has
    // to find the EMBOSSED head on a PC plate, which is tone-on-tone -- it has
    // no alpha of its own and barely any hue, so the only thing separating it
    // from empty leather is how much the luminance VARIES down a column.
    // `Read(path, keepLuminance: true)` keeps one extra byte plane for that
    // and LuminanceAt throws without it, so the default posture is unchanged
    // and a caller cannot silently read zeros.
    internal sealed class PngAlpha
    {
        public int Width { get; }
        public int Height { get; }

        // Row-major, Height * Width entries, [0..255]. Row 0 is the TOP of the
        // image, which is the direction PNG stores scanlines and the opposite
        // of the direction a ground line is measured in -- see
        // LowestOpaqueRow's own note.
        private readonly byte[] _alpha;

        // Null unless the caller asked for it -- see the header. One byte per
        // pixel: the flat mean of R, G and B, which is what a "does this
        // column vary" question needs and is not a colorimetric luminance.
        private readonly byte[] _luminance;

        private PngAlpha(int width, int height, byte[] alpha, byte[] luminance)
        {
            Width = width;
            Height = height;
            _alpha = alpha;
            _luminance = luminance;
        }

        public byte At(int x, int y) => _alpha[y * Width + x];

        public byte LuminanceAt(int x, int y)
        {
            if (_luminance == null)
            {
                throw new InvalidOperationException(
                    "this PNG was read alpha-only. Pass keepLuminance: true to PngAlpha.Read when the " +
                    "question is about what the art LOOKS like rather than where it is.");
            }

            return _luminance[y * Width + x];
        }

        // The bottom-most row holding a pixel at least this opaque, or -1 for
        // a wholly transparent image. Rows count DOWN from the top, so a
        // larger number is lower on the canvas.
        //
        // The threshold matches sheet_slicing.ALPHA_THRESHOLD, and it is not
        // zero for a reason the slicer's own header gives: a feathered edge
        // leaves single-digit alpha several pixels past the drawing, so
        // "anything above nothing" measures the feather rather than the art.
        public int LowestOpaqueRow(int threshold = 8)
        {
            for (int y = Height - 1; y >= 0; y--)
            {
                int row = y * Width;
                for (int x = 0; x < Width; x++)
                {
                    if (_alpha[row + x] > threshold)
                    {
                        return y;
                    }
                }
            }

            return -1;
        }

        // The tightest box holding every pixel at least this opaque, or null
        // for a wholly transparent image. Left/top/right/bottom, inclusive,
        // in the same top-down/left-right pixel coordinates `At` and
        // `LowestOpaqueRow` already use -- row 0 is the canvas TOP.
        //
        // Exists for StanceManifestValidationTests' castPoint check: an
        // authored point outside this box is pointing at empty air, the same
        // class of typo `groundLine`'s 8px band already catches on the
        // vertical axis alone.
        public (int Left, int Top, int Right, int Bottom)? AlphaBBox(int threshold = 8)
        {
            int left = Width, top = Height, right = -1, bottom = -1;

            for (int y = 0; y < Height; y++)
            {
                int row = y * Width;
                for (int x = 0; x < Width; x++)
                {
                    if (_alpha[row + x] <= threshold) continue;

                    if (x < left) left = x;
                    if (x > right) right = x;
                    if (y < top) top = y;
                    if (y > bottom) bottom = y;
                }
            }

            return right < 0 ? ((int, int, int, int)?)null : (left, top, right, bottom);
        }

        public static PngAlpha Read(string path, bool keepLuminance = false)
        {
            byte[] bytes = File.ReadAllBytes(path);

            if (bytes.Length < 8 || bytes[0] != 0x89 || bytes[1] != 'P' || bytes[2] != 'N' || bytes[3] != 'G')
            {
                throw new InvalidDataException($"'{path}' is not a PNG.");
            }

            int width = 0, height = 0;
            var idat = new MemoryStream();
            int at = 8;

            while (at + 8 <= bytes.Length)
            {
                int length = ReadInt32(bytes, at);
                string type = System.Text.Encoding.ASCII.GetString(bytes, at + 4, 4);
                int dataAt = at + 8;

                if (type == "IHDR")
                {
                    width = ReadInt32(bytes, dataAt);
                    height = ReadInt32(bytes, dataAt + 4);
                    byte bitDepth = bytes[dataAt + 8];
                    byte colourType = bytes[dataAt + 9];
                    byte interlace = bytes[dataAt + 12];

                    if (bitDepth != 8 || colourType != 6 || interlace != 0)
                    {
                        throw new NotSupportedException(
                            $"'{path}' is bitDepth {bitDepth}, colourType {colourType}, interlace {interlace}. " +
                            "This reader handles only 8-bit non-interlaced RGBA, which is what every tool in " +
                            "tools/ writes. Re-export it, or teach this reader the new shape deliberately.");
                    }
                }
                else if (type == "IDAT")
                {
                    idat.Write(bytes, dataAt, length);
                }
                else if (type == "IEND")
                {
                    break;
                }

                at = dataAt + length + 4; // + CRC
            }

            if (width <= 0 || height <= 0)
            {
                throw new InvalidDataException($"'{path}' has no usable IHDR.");
            }

            idat.Position = 0;
            // Two bytes of zlib header, then a raw deflate stream. DeflateStream
            // is what both hosts have -- ZLibStream arrived in .NET 6 and Unity
            // does not ship it, so skipping the header by hand is what keeps one
            // file compiling under both.
            idat.ReadByte();
            idat.ReadByte();

            byte[] raw = Inflate(idat, height * (width * 4 + 1));
            byte[] luminance = keepLuminance ? new byte[width * height] : null;
            return new PngAlpha(width, height, Unfilter(raw, width, height, luminance), luminance);
        }

        private static byte[] Inflate(Stream compressed, int expected)
        {
            var outBytes = new byte[expected];
            int filled = 0;

            using (var inflater = new DeflateStream(compressed, CompressionMode.Decompress, leaveOpen: true))
            {
                while (filled < expected)
                {
                    int read = inflater.Read(outBytes, filled, expected - filled);
                    if (read <= 0)
                    {
                        throw new InvalidDataException(
                            $"the PNG's deflate stream ended {expected - filled} bytes short of its declared size.");
                    }

                    filled += read;
                }
            }

            return outBytes;
        }

        // PNG scanline filters, undone in place. Four bytes per pixel (RGBA at
        // depth 8), one filter byte per row. The five filter types are the
        // whole of the format's compression pre-pass and Pillow uses all of
        // them, so none can be skipped as "probably not used".
        private static byte[] Unfilter(byte[] raw, int width, int height, byte[] luminance = null)
        {
            const int bpp = 4;
            int stride = width * bpp;
            var alpha = new byte[width * height];
            var previous = new byte[stride];
            var current = new byte[stride];

            for (int y = 0; y < height; y++)
            {
                int rowAt = y * (stride + 1);
                byte filter = raw[rowAt];
                Array.Copy(raw, rowAt + 1, current, 0, stride);

                for (int i = 0; i < stride; i++)
                {
                    int a = i >= bpp ? current[i - bpp] : 0;
                    int b = previous[i];
                    int c = i >= bpp ? previous[i - bpp] : 0;

                    switch (filter)
                    {
                        case 0: break;
                        case 1: current[i] = (byte)(current[i] + a); break;
                        case 2: current[i] = (byte)(current[i] + b); break;
                        case 3: current[i] = (byte)(current[i] + (a + b) / 2); break;
                        case 4: current[i] = (byte)(current[i] + Paeth(a, b, c)); break;
                        default:
                            throw new InvalidDataException($"unknown PNG scanline filter {filter} on row {y}.");
                    }
                }

                for (int x = 0; x < width; x++)
                {
                    alpha[y * width + x] = current[x * bpp + 3];
                    if (luminance != null)
                    {
                        luminance[y * width + x] =
                            (byte)((current[x * bpp] + current[x * bpp + 1] + current[x * bpp + 2]) / 3);
                    }
                }

                var swap = previous;
                previous = current;
                current = swap;
            }

            return alpha;
        }

        private static int Paeth(int a, int b, int c)
        {
            int p = a + b - c;
            int pa = Math.Abs(p - a);
            int pb = Math.Abs(p - b);
            int pc = Math.Abs(p - c);
            if (pa <= pb && pa <= pc) return a;
            return pb <= pc ? b : c;
        }

        private static int ReadInt32(byte[] bytes, int at)
        {
            return (bytes[at] << 24) | (bytes[at + 1] << 16) | (bytes[at + 2] << 8) | bytes[at + 3];
        }
    }
}
