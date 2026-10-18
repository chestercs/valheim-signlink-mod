using System;
using System.Collections.Generic;

namespace SignLink.Pictures
{
    /// <summary>A decoded animated GIF: full frames (every frame composited over the previous ones, as browsers and
    /// Pillow show them), RGB24, rows top-down, or what Decode's frame made of each, and how long each stays.</summary>
    internal sealed class GifImage
    {
        public int Width, Height;
        public List<byte[]> Frames = new List<byte[]>();
        public List<int> DelaysMs = new List<int>();
        public int SourceFrames;
    }

    /// <summary>GIF87a/89a decoder: LZW, global and local palettes, interlacing, transparency, the disposal methods
    /// (none, keep, restore to background, restore to previous). Unity cannot decode GIFs, so the mod does it itself.
    /// No Unity here: mod/SignLink.Tests compares every frame with Pillow's.</summary>
    internal static class GifDecoder
    {
        public static bool IsGif(byte[] d) => d != null && d.Length >= 6 && d[0] == 'G' && d[1] == 'I' && d[2] == 'F';

        /// <summary>A file with more frames, or more frame pixels in all, than this is refused before any frame is decoded:
        /// a hand-made GIF cannot run away with the decoder.</summary>
        public const int MaxFrames = 5000;
        public const long MaxFramePixels = 1_500_000_000;

        /// <summary>The default of Decode's maxBytes: the most a GIF's full-size frames may hold before every k-th is kept.</summary>
        public const long MaxBytes = 200L << 20;

        /// <summary>A GIF's size and how many frames Decode keeps (every k-th past MaxBytes), from its blocks without
        /// decoding any: what a screen of it would hold is known before the frames are made (0.20.132). False when it is not
        /// a GIF Decode takes (the size, too many frames or frame pixels); a file cut short may decode fewer.</summary>
        public static bool Measure(byte[] d, out int w, out int h, out int frames, long maxBytes = MaxBytes, int maxFrames = int.MaxValue,
                                   Func<int, int, long> frameBytes = null)
        {
            frames = 0;
            if (!Inspect(d, out w, out h, out int count)) return false;
            frames = Math.Min(maxFrames, Kept(count, frameBytes != null ? frameBytes(w, h) : (long)w * h * 3, maxBytes));
            return true;
        }

        /// <summary>A GIF's size and how many frames it holds, from its blocks (none decoded); false when Decode would not
        /// take it (not a GIF, the size, too many frames or frame pixels).</summary>
        public static bool Inspect(byte[] d, out int w, out int h, out int count)
        {
            w = h = count = 0;
            if (!IsGif(d) || d.Length < 13) return false;
            w = d[6] | (d[7] << 8);
            h = d[8] | (d[9] << 8);
            if (w <= 0 || h <= 0 || (long)w * h > 16_000_000) return false;
            count = CountFrames(d, out long framePixels);
            if (count == 0 || count > MaxFrames || framePixels > MaxFramePixels)
            {
                count = 0;
                return false;
            }
            return true;
        }

        /// <summary>Every stride-th frame is kept: count frames of frameBytes each over maxBytes (1.0.2: the bytes of a frame as
        /// kept, a screen's shrunk one, not the full-size frame's, review).</summary>
        public static int Stride(int count, long frameBytes, long maxBytes) =>
            (int)Math.Max(1, (count * Math.Max(1, frameBytes) + maxBytes - 1) / Math.Max(1, maxBytes));

        /// <summary>How many of count frames Decode keeps at that stride.</summary>
        public static int Kept(int count, long frameBytes, long maxBytes)
        {
            if (count <= 0) return 0;
            int stride = Stride(count, frameBytes, maxBytes);
            return (count + stride - 1) / stride;
        }

        /// <summary>Decodes the frames. If they would take more than maxBytes, only every k-th frame is kept (its delay
        /// grows k times), so the animation keeps its speed. maxFrames stops early (1: the first frame, for a preview).
        /// cancelled is asked before every frame (the window closed, another file picked): the decode then stops with an
        /// OperationCanceledException. frame: what to keep of each kept frame (Frames gets what it returns), handed the
        /// canvas as composed (RGB24, rows top-down, width x height), which is drawn over for the next frame: it returns a
        /// copy or what it makes of it (the screen's shrunk frame, 0.20.132, review: a copy of every full-size frame was
        /// held besides the screen's); null keeps a copy. frameBytes: what a kept frame takes (the screen's size), for the
        /// stride; null: the full-size frame's.</summary>
        public static GifImage Decode(byte[] d, long maxBytes = MaxBytes, int maxFrames = int.MaxValue, Func<bool> cancelled = null,
                                      Func<byte[], int, int, byte[]> frame = null, Func<int, int, long> frameBytes = null)
        {
            if (!IsGif(d)) throw new ArgumentException("not a GIF");
            if (d.Length < 13) throw new ArgumentException("the GIF is cut short");
            int w = d[6] | (d[7] << 8), h = d[8] | (d[9] << 8);
            if (w <= 0 || h <= 0 || (long)w * h > 16_000_000) throw new ArgumentException($"the GIF is {w} x {h} pixels: at most 16 million pixels a frame (4000 x 4000)");
            int count = CountFrames(d, out long framePixels);
            if (count == 0) throw new ArgumentException("the GIF has no frames");
            if (count > MaxFrames || framePixels > MaxFramePixels)
                throw new ArgumentException($"the GIF is too long or too large to read: {count} frames, {framePixels / 1000000} million pixels in all " +
                                            $"(at most {MaxFrames} frames and {MaxFramePixels / 1000000} million pixels: about {MaxFramePixels / (1920L * 1080)} frames of 1920 x 1080). " +
                                            "Make it shorter or smaller");
            int stride = Stride(count, frameBytes != null ? frameBytes(w, h) : (long)w * h * 3, maxBytes);
            var lzw = new LzwBuffers();                                         // one set for every frame

            var gif = new GifImage { Width = w, Height = h, SourceFrames = count };
            int packed = d[10];
            byte[] global = null;
            int pos = 13;
            if ((packed & 0x80) != 0)
            {
                int n = 3 << ((packed & 7) + 1);
                global = Slice(d, pos, n);
                pos += n;
            }
            int bgIndex = d[11];
            var canvas = new byte[w * h * 3];
            byte[] saved = null;                                                // disposal 3: the frame's rectangle before it
            int savedLeft = 0, savedTop = 0, savedW = 0, savedH = 0;
            int disposal = 0, delay = 0, transparent = -1, index = 0;
            long pendingDelay = 0;

            while (pos < d.Length)
            {
                if (cancelled != null && cancelled()) throw new OperationCanceledException("the GIF decode was cancelled");
                int block = d[pos++];
                if (block == 0x3B) break;                                       // trailer
                if (block == 0x21)                                              // extension
                {
                    if (pos >= d.Length) break;
                    int label = d[pos++];
                    if (label == 0xF9 && pos + 4 < d.Length && d[pos] >= 4)     // graphic control
                    {
                        int p = d[pos + 1];
                        disposal = (p >> 2) & 7;
                        delay = (d[pos + 2] | (d[pos + 3] << 8)) * 10;
                        transparent = (p & 1) != 0 ? d[pos + 4] : -1;
                    }
                    pos = SkipBlocks(d, pos);
                    continue;
                }
                if (block != 0x2C) break;                                       // unknown: stop like browsers do
                if (pos + 9 > d.Length) break;                                  // cut short: keep what decoded

                int left = d[pos] | (d[pos + 1] << 8), top = d[pos + 2] | (d[pos + 3] << 8);
                int fw = d[pos + 4] | (d[pos + 5] << 8), fh = d[pos + 6] | (d[pos + 7] << 8);
                int fp = d[pos + 8];
                pos += 9;
                if ((long)fw * fh > 16_000_000) throw new ArgumentException($"a GIF frame of {fw} x {fh} pixels is too large");
                byte[] palette = global;
                if ((fp & 0x80) != 0)
                {
                    int n = 3 << ((fp & 7) + 1);
                    palette = Slice(d, pos, n);
                    pos += n;
                }
                if (palette == null) throw new ArgumentException("a GIF frame without a palette");
                if (pos >= d.Length) break;
                int minCode = d[pos++];
                if (minCode < 1 || minCode > 11) throw new ArgumentException($"a GIF frame with LZW code size {minCode}");
                byte[] data = Gather(d, ref pos);
                int cw = Math.Min(fw, w - left), ch = Math.Min(fh, h - top);    // the part of the frame on the canvas
                bool empty = cw <= 0 || ch <= 0;                                 // off the canvas: its delay counts, nothing to decode

                if (disposal == 3 && !empty)
                {
                    saved = new byte[cw * ch * 3];                              // only the frame's rectangle, not the canvas
                    savedLeft = left; savedTop = top; savedW = cw; savedH = ch;
                    for (int y = 0; y < ch; y++) Buffer.BlockCopy(canvas, ((top + y) * w + left) * 3, saved, y * cw * 3, cw * 3);
                }
                if (!empty)
                {
                    byte[] idx = Lzw(data, minCode, fw * fh, lzw);
                    if ((fp & 0x40) != 0) idx = Deinterlace(idx, fw, fh, lzw);
                    for (int y = 0; y < ch; y++)
                    {
                        int cy = top + y;
                        for (int x = 0; x < cw; x++)
                        {
                            int cx = left + x;
                            int c = idx[y * fw + x];
                            if (c == transparent) continue;
                            int o = (cy * w + cx) * 3, pi = c * 3;
                            if (pi + 2 >= palette.Length) continue;
                            canvas[o] = palette[pi];
                            canvas[o + 1] = palette[pi + 1];
                            canvas[o + 2] = palette[pi + 2];
                        }
                    }
                }

                pendingDelay += stride > 1 && delay <= 10 ? 100 : delay;       // as browsers time it, before adding
                                                                                // up the frames that are left out
                if (index % stride == 0)
                {
                    gif.Frames.Add(frame != null ? frame(canvas, w, h) : (byte[])canvas.Clone());
                    gif.DelaysMs.Add((int)Math.Min(int.MaxValue, pendingDelay));
                    pendingDelay = 0;
                    if (gif.Frames.Count >= maxFrames) break;
                }
                else if (gif.DelaysMs.Count > 0)
                {
                    gif.DelaysMs[gif.DelaysMs.Count - 1] = (int)Math.Min(int.MaxValue, gif.DelaysMs[gif.DelaysMs.Count - 1] + pendingDelay);
                    pendingDelay = 0;
                }
                index++;

                if (disposal == 2)                                              // restore the frame's area to the background
                {
                    byte r = 0, g = 0, b = 0;
                    if (transparent < 0 && global != null && bgIndex * 3 + 2 < global.Length)
                    {
                        r = global[bgIndex * 3];
                        g = global[bgIndex * 3 + 1];
                        b = global[bgIndex * 3 + 2];
                    }
                    for (int y = top; y < Math.Min(h, top + fh); y++)
                        for (int x = left; x < Math.Min(w, left + fw); x++)
                        {
                            int o = (y * w + x) * 3;
                            canvas[o] = r;
                            canvas[o + 1] = g;
                            canvas[o + 2] = b;
                        }
                }
                else if (disposal == 3 && saved != null)                        // restore the frame's rectangle
                {
                    for (int y = 0; y < savedH; y++) Buffer.BlockCopy(saved, y * savedW * 3, canvas, ((savedTop + y) * w + savedLeft) * 3, savedW * 3);
                    saved = null;
                }
                disposal = 0;
                delay = 0;
                transparent = -1;
            }
            if (gif.Frames.Count == 0) throw new ArgumentException("the GIF has no frames");
            return gif;
        }

        /// <summary>How many image blocks the file has and how many pixels they hold in all, without decoding them.</summary>
        static int CountFrames(byte[] d, out long pixels)
        {
            int pos = 13, n = 0;
            pixels = 0;
            if ((d[10] & 0x80) != 0) pos += 3 << ((d[10] & 7) + 1);
            while (pos < d.Length)
            {
                int block = d[pos++];
                if (block == 0x21) { pos = SkipBlocks(d, pos + 1); continue; }
                if (block != 0x2C || pos + 9 > d.Length) break;
                int fw = d[pos + 4] | (d[pos + 5] << 8), fh = d[pos + 6] | (d[pos + 7] << 8);
                int fp = d[pos + 8];
                pos += 9;
                if ((fp & 0x80) != 0) pos += 3 << ((fp & 7) + 1);
                pos = SkipBlocks(d, pos + 1);
                pixels += (long)fw * fh;
                n++;
            }
            return n;
        }

        static int SkipBlocks(byte[] d, int pos)
        {
            while (pos < d.Length)
            {
                int len = d[pos++];
                if (len == 0) break;
                pos += len;
            }
            return pos;
        }

        static byte[] Gather(byte[] d, ref int pos)
        {
            int start = pos, total = 0;
            for (int p = pos; p < d.Length;)
            {
                int len = d[p++];
                if (len == 0) break;
                total += len;
                p += len;
            }
            var o = new byte[total];
            int k = 0;
            while (pos < d.Length)
            {
                int len = d[pos++];
                if (len == 0) break;
                int take = Math.Min(len, d.Length - pos);
                Buffer.BlockCopy(d, pos, o, k, take);
                k += take;
                pos += len;
            }
            return o;
        }

        static byte[] Slice(byte[] d, int pos, int n)
        {
            var o = new byte[n];
            Buffer.BlockCopy(d, pos, o, 0, Math.Min(n, d.Length - pos));
            return o;
        }

        /// <summary>The LZW table and the index buffers, made once per decode and used for every frame (0.20.132, review:
        /// a frame's worth of new arrays per frame). Each frame starts from what new arrays held: the table zeroed, the
        /// pixels past the frame's data zero.</summary>
        sealed class LzwBuffers
        {
            public const int Max = 4096;
            public readonly short[] Prefix = new short[Max];
            public readonly byte[] Suffix = new byte[Max];
            public readonly byte[] Stack = new byte[Max + 1];
            byte[] pixels, deinterlaced;

            public byte[] Pixels(int n) => pixels != null && pixels.Length >= n ? pixels : pixels = new byte[n];

            public byte[] Deinterlaced(int n) => deinterlaced != null && deinterlaced.Length >= n ? deinterlaced : deinterlaced = new byte[n];
        }

        /// <summary>Variable-length-code LZW as GIF uses it (codes of minCode+1 .. 12 bits, clear and end codes; a full
        /// table without a clear keeps its codes). The frame's npix indexes, in b's pixel buffer (at least npix long).</summary>
        static byte[] Lzw(byte[] data, int minCode, int npix, LzwBuffers b)
        {
            const int Max = LzwBuffers.Max;
            short[] prefix = b.Prefix;
            byte[] suffix = b.Suffix, stack = b.Stack, pixels = b.Pixels(npix);
            Array.Clear(prefix, 0, Max);                                        // as new: a malformed code reads zeros, not
            Array.Clear(suffix, 0, Max);                                        // the frame before's table
            int clear = 1 << minCode, end = clear + 1, avail = clear + 2, old = -1;
            int size = minCode + 1, mask = (1 << size) - 1;
            for (int c = 0; c < clear; c++) suffix[c] = (byte)c;
            int datum = 0, bits = 0, first = 0, top = 0, pi = 0, bi = 0;
            while (pi < npix)
            {
                if (top == 0)
                {
                    if (bits < size)
                    {
                        if (bi >= data.Length) break;
                        datum |= data[bi++] << bits;
                        bits += 8;
                        continue;
                    }
                    int code = datum & mask;
                    datum >>= size;
                    bits -= size;
                    if (code == clear)
                    {
                        size = minCode + 1;
                        mask = (1 << size) - 1;
                        avail = clear + 2;
                        old = -1;
                        continue;
                    }
                    if (code == end || code > avail) break;
                    if (old == -1)
                    {
                        stack[top++] = suffix[code];
                        old = first = code;
                        continue;
                    }
                    int inCode = code;
                    if (code == avail)
                    {
                        stack[top++] = (byte)first;
                        code = old;
                    }
                    while (code > clear)
                    {
                        stack[top++] = suffix[code];
                        code = prefix[code];
                    }
                    first = suffix[code];
                    stack[top++] = (byte)first;
                    if (avail < Max)
                    {
                        prefix[avail] = (short)old;
                        suffix[avail] = (byte)first;
                        avail++;
                        if ((avail & mask) == 0 && avail < Max)
                        {
                            size++;
                            mask += avail;
                        }
                    }
                    old = inCode;
                }
                pixels[pi++] = stack[--top];
            }
            Array.Clear(pixels, pi, npix - pi);                                 // data that ends early: the rest index 0, as new
            return pixels;
        }

        /// <summary>An interlaced frame's rows in order, into b's other buffer (every row of the frame is written).</summary>
        static byte[] Deinterlace(byte[] idx, int w, int h, LzwBuffers b)
        {
            byte[] o = b.Deinterlaced(w * h);
            int src = 0;
            int[] start = { 0, 4, 2, 1 }, step = { 8, 8, 4, 2 };
            for (int pass = 0; pass < 4; pass++)
                for (int y = start[pass]; y < h; y += step[pass])
                {
                    Buffer.BlockCopy(idx, src, o, y * w, w);
                    src += w;
                }
            return o;
        }
    }
}
