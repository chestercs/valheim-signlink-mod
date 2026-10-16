using System;

namespace SignLink.Pictures
{
    /// <summary>Picture to pixel grid as signart/image_import.py does it: the largest size with the picture's aspect
    /// under a pixel budget (fit), a gamma-correct Lanczos downscale with premultiplied alpha (Pillow's resampling,
    /// float32 like numpy), and 24-bit colours with 8-bit alpha. No Unity here (mod/SignLink.Tests compares it with Python).</summary>
    internal static class Resampler
    {
        /// <summary>image_import.fit: the largest W x H with the aspect w:h and W*H &lt;= maxPixels.</summary>
        public static void Fit(int w, int h, long maxPixels, out int W, out int H)
        {
            W = (int)Math.Sqrt((double)(maxPixels * w) / h);
            if (W < 1) W = 1;
            long rows = maxPixels / W;
            H = (int)Math.Min(rows, Math.Max(1, PyMath.RoundInt((double)((long)W * h) / w)));
        }

        /// <summary>A small copy for a thumbnail: the picture shrunk by a whole factor with a box average, so its longer
        /// side is at most max pixels (a picture that small is copied as it is). RGBA rows top-down in and out. Nothing
        /// shared with Grid: this is for the lists, not for a sign.</summary>
        public static byte[] Box(byte[] rgba, int w, int h, int max, out int W, out int H)
        {
            int k = Math.Max(1, (int)Math.Ceiling(Math.Max(w, h) / (double)max));
            W = Math.Max(1, w / k);
            H = Math.Max(1, h / k);
            if (k == 1 && W == w && H == h) return (byte[])rgba.Clone();
            var o = new byte[W * H * 4];
            for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                    for (int c = 0; c < 4; c++)
                    {
                        long sum = 0;
                        for (int yy = 0; yy < k; yy++)                      // a side shorter than k (a 120:1 banner)
                            for (int xx = 0; xx < k; xx++)                  // repeats its last row or column
                                sum += rgba[(Math.Min(h - 1, y * k + yy) * w + Math.Min(w - 1, x * k + xx)) * 4 + c];
                        o[(y * W + x) * 4 + c] = (byte)((sum + k * k / 2) / (k * k));
                    }
            return o;
        }

        /// <summary>Alpha (of 255) under MinAlpha is empty and from SolidAlpha on opaque: Lanczos rings a little at hard
        /// edges, and neither shows on a sign (image_import.grid).</summary>
        public const int MinAlpha = 8, SolidAlpha = 248;

        /// <summary>rgba: w x h pixels, rows top-down. Returns W x H pixels, rows top-down (PixelText.Pack): the colour,
        /// with its alpha where the downscaled alpha is between MinAlpha and SolidAlpha, -1 under MinAlpha; opaque:
        /// alpha ignored, as Pillow's convert("RGB"). One channel at a time (alpha, then R, G, B), each source row made
        /// from the bytes when the horizontal pass reads it (0.20.132, review: four full-size float planes were 16 bytes a
        /// source pixel, about 190 MB for a 12 MP photo): the same floats added in the same order, so the very same
        /// pixels. stale is asked once a row (null: never); true stops it with an OperationCanceledException.</summary>
        public static int[] Grid(byte[] rgba, int w, int h, int W, int H, bool opaque, Func<bool> stale = null)
        {
            var pass = new Passes(w, h, W, H);                          // the coefficients and buffers, for every channel
            float[] al = opaque || OneAlpha(rgba, w * h)                // one alpha everywhere: one row stands for all
                ? pass.Flat(opaque ? 1f : (float)(rgba[3] / 255f), stale)
                : pass.Resize((y, row) =>
                {
                    byte[] src = rgba;
                    for (int x = 0, n = row.Length, i = y * n * 4 + 3; x < n; x++, i += 4) row[x] = (float)(src[i] / 255f);
                }, stale);
            var out3 = new float[3][];
            for (int k = 0; k < 3; k++)
            {
                int c = k;
                out3[k] = pass.Resize((y, row) =>
                {
                    byte[] src = rgba;
                    float[] lin = LinearOf;
                    bool solid = opaque;
                    for (int x = 0, n = row.Length, i = y * n * 4; x < n; x++, i += 4)
                    {
                        float a = solid ? 1f : (float)(src[i + 3] / 255f);   // as the planes had it
                        row[x] = (float)(lin[src[i + c]] * a);
                    }
                }, stale);
            }

            var px = new int[W * H];
            for (int i = 0; i < W * H; i++)
            {
                float a = Math.Min(1f, Math.Max(0f, al[i]));
                int rgb = 0;
                for (int k = 0; k < 3; k++)
                {
                    float c = a > 1e-6f ? (float)(out3[k][i] / Math.Max(a, 1e-6f)) : 0f;
                    float s = ToSrgb(c);
                    double q = Math.Round((double)(float)(s * 255f), MidpointRounding.ToEven);
                    rgb = (rgb << 8) | (int)Math.Min(255, Math.Max(0, q));
                }
                if (opaque) px[i] = rgb;
                else
                {
                    int a8 = (int)Math.Round((double)(float)(a * 255f), MidpointRounding.ToEven);
                    px[i] = a8 < MinAlpha ? -1 : a8 >= SolidAlpha ? rgb : PixelText.Pack(rgb, a8);
                }
            }
            return px;
        }

        static float ToLinear(float c) =>
            c <= 0.04045f ? (float)(c / 12.92f) : (float)Math.Pow((float)((float)(c + 0.055f) / 1.055f), 2.4f);

        /// <summary>ToLinear of every byte value, by the very expression the per-pixel code used (a million pow calls
        /// per picture were a tenth of the encode).</summary>
        static readonly float[] LinearOf = BuildLinear();

        static float[] BuildLinear()
        {
            var t = new float[256];
            for (int b = 0; b < 256; b++) t[b] = ToLinear((float)(b / 255f));
            return t;
        }

        static float ToSrgb(float c)
        {
            c = Math.Min(1f, Math.Max(0f, c));
            return c <= 0.0031308f ? (float)(c * 12.92f)
                : (float)((float)(1.055f * (float)Math.Pow(c, (float)(1 / 2.4))) - 0.055f);
        }

        /// <summary>Every pixel's alpha byte is the first one's (a PNG with an alpha channel it does not use).</summary>
        static bool OneAlpha(byte[] rgba, int n)
        {
            byte a = rgba[3];
            for (int i = 1; i < n; i++)
                if (rgba[i * 4 + 3] != a) return false;
            return true;
        }

        /// <summary>Stops a resize whose result nobody waits for any more (stale: Grid's).</summary>
        static void Ask(Func<bool> stale)
        {
            if (stale != null && stale()) throw new OperationCanceledException("no longer wanted");
        }

        /// <summary>Pillow's ImagingResample for float images with the Lanczos filter: separable, horizontal pass
        /// first, coefficients and sums in double, every pass stored as float32. The same size returns a copy.</summary>
        public static float[] Resize(float[] src, int w, int h, int W, int H, Func<bool> stale = null) =>
            new Passes(w, h, W, H).Resize((y, row) => Array.Copy(src, y * w, row, 0, w), stale);

        /// <summary>Pillow's two passes for one size change, the coefficients made once for every channel (0.20.132). A
        /// channel's source rows come from a filler (row y, w floats) when the horizontal pass reads them: no full-size
        /// plane, only the horizontal pass's rows (W wide, those the vertical pass reads), or the plane itself when the
        /// width does not change (the vertical pass reads the source then). Those buffers serve one channel after
        /// another; what a resize returns is always an array of its own.</summary>
        sealed class Passes
        {
            readonly int w, h, W, H, kh, kv, yFirst, th;
            readonly int[] bh, bv;
            readonly double[] ch, cv;
            readonly float[] row;
            float[] between;

            public Passes(int w, int h, int W, int H)
            {
                this.w = w;
                this.h = h;
                this.W = W;
                this.H = H;
                row = new float[w];
                if (w == W && h == H) return;                           // a copy: no coefficients
                Coeffs(w, W, out kh, out bh, out ch);
                Coeffs(h, H, out kv, out bv, out cv);
                if (W == w) return;
                yFirst = bv[0];
                int yLast = bv[(H - 1) * 2] + bv[(H - 1) * 2 + 1];
                for (int i = 0; i < H; i++) bv[i * 2] -= yFirst;       // the horizontal pass keeps only the rows read
                th = yLast - yFirst;
            }

            public float[] Resize(Action<int, float[]> fill, Func<bool> stale)
            {
                if (w == W && h == H)
                {
                    var copy = new float[w * h];
                    for (int y = 0; y < h; y++)
                    {
                        Ask(stale);
                        fill(y, row);
                        Array.Copy(row, 0, copy, y * w, w);
                    }
                    return copy;
                }
                float[] img;
                if (W != w)
                {
                    bool last = H == h;                                 // no vertical pass: these rows are the result
                    img = last ? new float[W * th] : between ?? (between = new float[W * th]);
                    for (int y = 0; y < th; y++)
                    {
                        Ask(stale);
                        fill(y + yFirst, row);
                        Horizontal(img, y * W);
                    }
                    if (last) return img;
                }
                else
                {
                    img = between ?? (between = new float[w * h]);      // the width stays: the vertical pass reads the plane
                    for (int y = 0; y < h; y++)
                    {
                        Ask(stale);
                        fill(y, row);
                        Array.Copy(row, 0, img, y * w, w);
                    }
                }
                return Vertical(img, false, stale);
            }

            /// <summary>A channel with one value everywhere (opaque alpha): every source row is the same, and so is every
            /// row of the horizontal pass, made once; the vertical pass reads that row for each row it adds up (the very
            /// floats and sums the plane gave).</summary>
            public float[] Flat(float v, Func<bool> stale)
            {
                if (w == W && h == H)
                {
                    var all = new float[w * h];
                    for (int i = 0; i < all.Length; i++) all[i] = v;
                    return all;
                }
                var one = new float[W];
                if (W != w)
                {
                    for (int x = 0; x < w; x++) row[x] = v;
                    Horizontal(one, 0);
                    if (H == h)
                    {
                        var rows = new float[W * h];
                        for (int y = 0; y < h; y++) Array.Copy(one, 0, rows, y * W, W);
                        return rows;
                    }
                }
                else
                    for (int x = 0; x < w; x++) one[x] = v;
                return Vertical(one, true, stale);
            }

            /// <summary>The horizontal pass of the row buffer into o from `at` (W values).</summary>
            void Horizontal(float[] o, int at)
            {
                float[] src = row;
                for (int x = 0; x < W; x++)
                {
                    int xmin = bh[x * 2], xn = bh[x * 2 + 1];
                    double ss = 0.0;
                    for (int k = 0; k < xn; k++) ss += src[k + xmin] * ch[x * kh + k];
                    o[at + x] = (float)ss;
                }
            }

            /// <summary>The vertical pass over rows W wide (same: one row that stands for every row): W x H.</summary>
            float[] Vertical(float[] img, bool same, Func<bool> stale)
            {
                var outp = new float[W * H];
                for (int y = 0; y < H; y++)
                {
                    Ask(stale);
                    int ymin = bv[y * 2], yn = bv[y * 2 + 1];
                    for (int x = 0; x < W; x++)
                    {
                        double ss = 0.0;
                        if (same)
                            for (int k = 0; k < yn; k++) ss += img[x] * cv[y * kv + k];
                        else
                            for (int k = 0; k < yn; k++) ss += img[(k + ymin) * W + x] * cv[y * kv + k];
                        outp[y * W + x] = (float)ss;
                    }
                }
                return outp;
            }
        }

        /// <summary>Pillow's precompute_coeffs (Resample.c).</summary>
        static void Coeffs(int inSize, int outSize, out int ksize, out int[] bounds, out double[] kk)
        {
            double scale = (double)inSize / outSize, filterscale = Math.Max(1.0, scale);
            double support = 3.0 * filterscale;
            ksize = (int)Math.Ceiling(support) * 2 + 1;
            bounds = new int[outSize * 2];
            kk = new double[outSize * ksize];
            for (int xx = 0; xx < outSize; xx++)
            {
                double center = (xx + 0.5) * scale, ww = 0.0, ss = 1.0 / filterscale;
                int xmin = (int)(center - support + 0.5);
                if (xmin < 0) xmin = 0;
                int xmax = (int)(center + support + 0.5);
                if (xmax > inSize) xmax = inSize;
                xmax -= xmin;
                for (int x = 0; x < xmax; x++)
                {
                    double v = Lanczos((x + xmin - center + 0.5) * ss);
                    kk[xx * ksize + x] = v;
                    ww += v;
                }
                for (int x = 0; x < xmax; x++)
                    if (ww != 0.0) kk[xx * ksize + x] /= ww;
                bounds[xx * 2] = xmin;
                bounds[xx * 2 + 1] = xmax;
            }
        }

        static double Sinc(double x)
        {
            if (x == 0.0) return 1.0;
            x *= Math.PI;
            return Math.Sin(x) / x;
        }

        static double Lanczos(double x) => -3.0 <= x && x < 3.0 ? Sinc(x) * Sinc(x / 3) : 0.0;
    }
}
