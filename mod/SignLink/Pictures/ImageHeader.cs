using System;
using System.IO;

namespace SignLink.Pictures
{
    /// <summary>Picture sizes (PNG, JPEG, GIF) without decoding, and a JPEG's EXIF orientation (no Unity here:
    /// mod/SignLink.Tests checks it on real files).</summary>
    internal static class ImageHeader
    {
        /// <summary>Width and height as the picture shows (a JPEG turned by its EXIF orientation), from a PNG (IHDR),
        /// JPEG (SOF) or GIF header, without decoding: the window shows the size when a file is picked, and a full decode
        /// of a big photo would stall the game.</summary>
        public static bool ImageSize(string path, out int w, out int h)
        {
            using (FileStream f = File.OpenRead(path))
            {
                bool ok = Read(f, out w, out h, out int orientation);
                if (ok && orientation >= 5) Swap(ref w, ref h);
                return ok;
            }
        }

        /// <summary>The same from the file's bytes: the stored width and height (before any turn) and the orientation
        /// (1..8; 1 = as stored).</summary>
        public static bool Size(byte[] data, out int w, out int h, out int orientation)
        {
            using (var f = new MemoryStream(data, false)) return Read(f, out w, out h, out orientation);
        }

        static void Swap(ref int a, ref int b)
        {
            int t = a;
            a = b;
            b = t;
        }

        static bool Read(Stream f, out int w, out int h, out int orientation)
        {
            w = h = 0;
            orientation = 1;
            var b = new byte[24];
            if (f.Read(b, 0, 24) < 24) return false;
            if (b[0] == 0x89 && b[1] == 'P' && b[2] == 'N' && b[3] == 'G')
            {
                if (b[12] != 'I' || b[13] != 'H' || b[14] != 'D' || b[15] != 'R') return false;   // the first chunk is IHDR
                w = (b[16] << 24) | (b[17] << 16) | (b[18] << 8) | b[19];
                h = (b[20] << 24) | (b[21] << 16) | (b[22] << 8) | b[23];
                return w > 0 && h > 0;
            }
            if (b[0] == 'G' && b[1] == 'I' && b[2] == 'F')               // logical screen size
            {
                w = b[6] | (b[7] << 8);
                h = b[8] | (b[9] << 8);
                return w > 0 && h > 0;
            }
            if (b[0] != 0xFF || b[1] != 0xD8) return false;
            f.Position = 2;
            byte[] app = App1(f, out int sof);
            if (app != null)
            {
                int o = ExifOrientation(app);
                if (o > 0) orientation = o;
            }
            if (sof < 0) return false;
            f.Position = sof;
            if (f.Read(b, 0, 5) < 5) return false;
            h = (b[1] << 8) | b[2];
            w = (b[3] << 8) | b[4];
            return w > 0 && h > 0;
        }

        /// <summary>Walks a JPEG's segments from the stream's position: the EXIF APP1 payload (null when there is none,
        /// or it is cut short) and the position of the frame header's data (SOF; -1 when there is none).</summary>
        static byte[] App1(Stream f, out int sof)
        {
            sof = -1;
            byte[] app = null;
            var b = new byte[2];
            while (true)
            {
                int c = f.ReadByte();
                if (c < 0) return app;
                if (c != 0xFF) continue;
                int m = f.ReadByte();
                while (m == 0xFF) m = f.ReadByte();
                if (m < 0) return app;
                if (m == 0xD8 || m == 0x01 || (m >= 0xD0 && m <= 0xD7)) continue;
                if (m == 0xD9 || m == 0xDA) return app;                     // the end, or the scan: no more headers
                if (f.Read(b, 0, 2) < 2) return app;
                int len = (b[0] << 8) | b[1];
                if (m == 0xE1 && len >= 16 && app == null)                // APP1: EXIF, where phones say how to turn the photo
                {
                    var payload = new byte[len - 2];
                    if (!ReadFully(f, payload)) return null;
                    if (payload.Length >= 6 && payload[0] == 'E' && payload[1] == 'x' && payload[2] == 'i' && payload[3] == 'f') app = payload;
                    continue;
                }
                if (m >= 0xC0 && m <= 0xCF && m != 0xC4 && m != 0xC8 && m != 0xCC)
                {
                    sof = (int)f.Position;
                    return app;
                }
                if (len < 2) return app;
                f.Position += len - 2;
            }
        }

        /// <summary>A JPEG's own small preview, if its EXIF carries one (phone photos do: IFD1's JPEGInterchangeFormat,
        /// tags 0x0201 and 0x0202), as JPEG bytes. Cheap to decode: a list of big photos shows at once.</summary>
        public static bool Thumbnail(byte[] jpeg, out byte[] thumb)
        {
            thumb = null;
            if (jpeg == null || jpeg.Length < 4 || jpeg[0] != 0xFF || jpeg[1] != 0xD8) return false;
            byte[] a;
            using (var f = new MemoryStream(jpeg, false))
            {
                f.Position = 2;
                a = App1(f, out _);
            }
            if (a == null || a.Length < 14 || a[0] != 'E' || a[1] != 'x' || a[2] != 'i' || a[3] != 'f') return false;
            const int t = 6;
            bool le = a[t] == 'I' && a[t + 1] == 'I';
            if (!le && !(a[t] == 'M' && a[t + 1] == 'M')) return false;
            int U16(long p) => p < 0 || p + 1 >= a.Length ? -1 : le ? a[p] | (a[p + 1] << 8) : (a[p] << 8) | a[p + 1];
            long U32(long p) => p < 0 || p + 3 >= a.Length ? -1
                : le ? (long)(uint)(a[p] | (a[p + 1] << 8) | (a[p + 2] << 16) | (a[p + 3] << 24))
                : (long)(uint)((a[p] << 24) | (a[p + 1] << 16) | (a[p + 2] << 8) | a[p + 3]);
            long ifd0 = U32(t + 4);
            if (ifd0 < 0) return false;
            int n0 = U16(t + ifd0);
            if (n0 < 0 || n0 > 512) return false;
            long ifd1 = U32(t + ifd0 + 2 + n0 * 12L);                       // the link after IFD0's entries
            if (ifd1 <= 0) return false;
            int n1 = U16(t + ifd1);
            if (n1 < 0 || n1 > 512) return false;
            long offset = -1, length = -1;
            int compression = 6;
            for (int i = 0; i < n1; i++)
            {
                long e = t + ifd1 + 2 + i * 12L;
                int tag = U16(e);
                if (tag < 0) return false;
                if (tag == 0x0201) offset = U32(e + 8);
                else if (tag == 0x0202) length = U32(e + 8);
                else if (tag == 0x0103) compression = U16(e + 8);          // 6: a JPEG thumbnail (the only kind read)
            }
            if (compression != 6 || offset < 0 || length <= 4 || length > 4000000 || t + offset + length > a.Length) return false;
            thumb = new byte[length];
            Buffer.BlockCopy(a, (int)(t + offset), thumb, 0, (int)length);
            return thumb[0] == 0xFF && thumb[1] == 0xD8;
        }

        static bool ReadFully(Stream f, byte[] buf)
        {
            int got = 0;
            while (got < buf.Length)
            {
                int r = f.Read(buf, got, buf.Length - got);
                if (r <= 0) return false;
                got += r;
            }
            return true;
        }

        /// <summary>The orientation tag (0x0112) in an APP1 segment's first IFD, or 0 when there is none.</summary>
        static int ExifOrientation(byte[] a)
        {
            if (a.Length < 14 || a[0] != 'E' || a[1] != 'x' || a[2] != 'i' || a[3] != 'f' || a[4] != 0 || a[5] != 0) return 0;
            const int t = 6;                                                // the TIFF header
            bool le = a[t] == 'I' && a[t + 1] == 'I';
            if (!le && !(a[t] == 'M' && a[t + 1] == 'M')) return 0;
            int U16(long p) => p < 0 || p + 1 >= a.Length ? -1 : le ? a[p] | (a[p + 1] << 8) : (a[p] << 8) | a[p + 1];
            long U32(long p) => p < 0 || p + 3 >= a.Length ? -1
                : le ? (long)(uint)(a[p] | (a[p + 1] << 8) | (a[p + 2] << 16) | (a[p + 3] << 24))
                : (long)(uint)((a[p] << 24) | (a[p + 1] << 16) | (a[p + 2] << 8) | a[p + 3]);
            long offset = U32(t + 4);
            if (offset < 0) return 0;
            long ifd = t + offset;
            int n = U16(ifd);
            for (int i = 0; i < n && i < 512; i++)
            {
                long e = ifd + 2 + i * 12L;
                int tag = U16(e);
                if (tag < 0) return 0;
                if (tag != 0x0112) continue;
                int v = U16(e + 8);
                return v >= 1 && v <= 8 ? v : 0;
            }
            return 0;
        }

        /// <summary>RGBA pixels (rows top-down) turned as an EXIF orientation says (Pillow's ImageOps.exif_transpose):
        /// 2 mirrored, 3 half a turn, 4 upside down mirrored, 5 transposed, 6 a quarter turn clockwise, 7 transversed,
        /// 8 a quarter turn counterclockwise.</summary>
        public static byte[] Orient(byte[] rgba, int w, int h, int orientation, out int W, out int H)
        {
            W = w;
            H = h;
            if (orientation < 2 || orientation > 8) return rgba;
            if (orientation >= 5) Swap(ref W, ref H);
            var o = new byte[rgba.Length];
            for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                {
                    int sx, sy;
                    switch (orientation)
                    {
                        case 2: sx = w - 1 - x; sy = y; break;
                        case 3: sx = w - 1 - x; sy = h - 1 - y; break;
                        case 4: sx = x; sy = h - 1 - y; break;
                        case 5: sx = y; sy = x; break;
                        case 6: sx = y; sy = h - 1 - x; break;
                        case 7: sx = w - 1 - y; sy = h - 1 - x; break;
                        default: sx = w - 1 - y; sy = x; break;
                    }
                    int s = (sy * w + sx) * 4, d = (y * W + x) * 4;
                    o[d] = rgba[s];
                    o[d + 1] = rgba[s + 1];
                    o[d + 2] = rgba[s + 2];
                    o[d + 3] = rgba[s + 3];
                }
            return o;
        }
    }
}
