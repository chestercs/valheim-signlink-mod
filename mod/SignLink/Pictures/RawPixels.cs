using System;

namespace SignLink.Pictures
{
    /// <summary>The bytes of a decoded texture (Unity's GetRawTextureData: rows bottom-up, in the texture's own format)
    /// to RGBA rows top-down, as the picture code reads them, halved on the way with the 2 x 2 box Grid uses: a photo's
    /// buffer is made from its texture in one pass, with no second full-size copy (a 24 MP photo was about 290 MB of
    /// allocations in one frame). No Unity here (mod/SignLink.Tests).</summary>
    internal static class RawPixels
    {
        /// <summary>Texture formats read directly; anything else goes through Unity's GetPixels32 (as Rgba32).</summary>
        public enum Format { Rgba32, Rgb24, Argb32, Bgra32 }

        public static int BytesPerPixel(Format f) => f == Format.Rgb24 ? 3 : 4;

        /// <summary>How many times Grid would halve a w x h source before resampling it to the largest plan: while the
        /// halved one still has at least `limit` pixels (PictureImporter.Grid's rule, with plan.Pixels = MaxPixels).
        /// Every plan has fewer pixels, so these halvings always happen; doing them first keeps a photo small.</summary>
        public static int Halvings(int w, int h, long limit)
        {
            int n = 0;
            while ((long)(w / 2) * (h / 2) >= limit)
            {
                w /= 2;
                h /= 2;
                n++;
            }
            return n;
        }

        /// <summary>RGBA rows top-down from raw texture bytes, halved `halvings` times (the first halving reads the raw
        /// bytes directly, the rest go through Halve): the very bytes Halve gives from the unhalved rows.</summary>
        public static byte[] ToRgba(byte[] raw, Format f, int w, int h, int halvings, out int W, out int H)
        {
            int bpp = BytesPerPixel(f);
            if (raw == null || w <= 0 || h <= 0 || (long)w * h * bpp > raw.Length) throw new ArgumentException("the texture's bytes are short");
            int r = 0, g = 1, b = 2, a = 3;                                 // where each channel sits in a pixel
            switch (f)
            {
                case Format.Rgb24: a = -1; break;
                case Format.Argb32: a = 0; r = 1; g = 2; b = 3; break;
                case Format.Bgra32: b = 0; g = 1; r = 2; a = 3; break;
            }
            byte[] o;
            if (halvings > 0)
            {
                W = w / 2;
                H = h / 2;
                if (W == 0 || H == 0) throw new ArgumentException("too small to halve");
                o = new byte[W * H * 4];
                for (int y = 0; y < H; y++)
                {
                    // the output's rows 2y and 2y + 1 (top-down) are the raw rows h - 1 - 2y and h - 2 - 2y (bottom-up);
                    // an odd height drops the bottom raw row, as Halve drops the last top-down row
                    int r0 = (h - 1 - 2 * y) * w * bpp, r1 = (h - 2 - 2 * y) * w * bpp;
                    for (int x = 0; x < W; x++)
                    {
                        int p0 = r0 + 2 * x * bpp, p1 = p0 + bpp, p2 = r1 + 2 * x * bpp, p3 = p2 + bpp, d = (y * W + x) * 4;
                        o[d] = (byte)((raw[p0 + r] + raw[p1 + r] + raw[p2 + r] + raw[p3 + r] + 2) / 4);
                        o[d + 1] = (byte)((raw[p0 + g] + raw[p1 + g] + raw[p2 + g] + raw[p3 + g] + 2) / 4);
                        o[d + 2] = (byte)((raw[p0 + b] + raw[p1 + b] + raw[p2 + b] + raw[p3 + b] + 2) / 4);
                        o[d + 3] = a < 0 ? (byte)255 : (byte)((raw[p0 + a] + raw[p1 + a] + raw[p2 + a] + raw[p3 + a] + 2) / 4);
                    }
                }
                halvings--;
            }
            else
            {
                W = w;
                H = h;
                o = new byte[W * H * 4];
                for (int y = 0; y < H; y++)
                {
                    int s = (h - 1 - y) * w * bpp;
                    for (int x = 0; x < W; x++, s += bpp)
                    {
                        int d = (y * W + x) * 4;
                        o[d] = raw[s + r];
                        o[d + 1] = raw[s + g];
                        o[d + 2] = raw[s + b];
                        o[d + 3] = a < 0 ? (byte)255 : raw[s + a];
                    }
                }
            }
            while (halvings-- > 0) o = Halve(o, ref W, ref H);
            return o;
        }

        /// <summary>Half the size with a 2 x 2 box (rounded), the odd edge dropped (PictureImporter.Grid's halving of
        /// photos far larger than their plan).</summary>
        public static byte[] Halve(byte[] rgba, ref int w, ref int h)
        {
            int W = w / 2, H = h / 2;
            var o = new byte[W * H * 4];
            for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                    for (int k = 0; k < 4; k++)
                    {
                        int a = ((2 * y) * w + 2 * x) * 4 + k, b = a + w * 4;
                        o[(y * W + x) * 4 + k] = (byte)((rgba[a] + rgba[a + 4] + rgba[b] + rgba[b + 4] + 2) / 4);
                    }
            w = W;
            h = H;
            return o;
        }
    }
}
