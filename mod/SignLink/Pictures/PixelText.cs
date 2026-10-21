using System;
using System.Collections.Generic;
using System.Text;
using PetyusPolisz.Kit;

namespace SignLink.Pictures
{
    /// <summary>Pixel pictures as sign text, the column encoding of signart/engine.py (column_cmds, line_cmds,
    /// encode_lines) for one glyph (■), one size and opaque, half-transparent or empty pixels, byte for byte
    /// (mod/SignLink.Tests).
    ///
    /// One pixel per line, column by column: &lt;indent&gt; and &lt;voffset&gt; change once per column, the line height
    /// steps down one pixel, a colour tag is written when the colour changes. About 8 characters per pixel; a sign
    /// shows at most 50 000 pixels (99 999 visible characters, a glyph and a line feed each), and in multiplayer a
    /// sign's text must stay under 512 KiB (docs/limits.md). Positions are relative to the sign the text goes on,
    /// in wu (2.5 cm). No Unity here.</summary>
    internal static class PixelText
    {
        public const string Glyph = "■";
        public const string Font = "valheim_fonts/valheim-norse";      // unlit: colours show as written
        public const int MaxPixelsPerSign = 49500;
        public const double NetworkKb = 400;                           // under Steam's 512 KiB message limit

        // ■ in NotoSansJP (the fallback font that has it): bounding box and advance in em, font ascender and
        // descender, faux-bold growth (signart/out/glyphs.json, read from the game's font assets).
        const double X0 = 0.1, Y0 = -0.02, X1 = 0.9, Y1 = 0.78, Adv = 1.0, Asc = 1.16, Desc = -0.288, Bold = 0.0203;
        const int DefaultColor = 0x1E1A12;                             // the sign's own text colour (no tag needed)

        /// <summary>TMP size of the pixel glyph: cover = n makes it n pitches wide (every point under n x n glyphs,
        /// opaque from afar); 0 = the pitch plus a hair (pixels.glyph_size).</summary>
        public static double GlyphSize(double pitch, double cover)
        {
            double w = X1 - X0;
            double ink = cover > 0 ? pitch * cover : pitch + Math.Min(0.3, 0.12 * pitch);
            return ink / (2 * w + 4 * Bold);
        }

        /// <summary>A grid pixel: 0xTTRRGGBB with TT = 255 - alpha, so an opaque colour is plain 0xRRGGBB and -1 (alpha 0)
        /// is empty.</summary>
        public static int Pack(int rgb, int alpha) => alpha <= 0 ? -1 : ((255 - Math.Min(255, alpha)) << 24) | (rgb & 0xFFFFFF);

        public static int Alpha(int px) => 255 - ((px >> 24) & 255);

        public static bool Drawn(int px) => Alpha(px) > 0;

        /// <summary>How many pitches wide a glyph of this size is (GlyphSize the other way): 2 when the pixels overlap to
        /// look solid from afar, about 1 when they only touch.</summary>
        public static double CoverOf(double size, double pitch) => size * (2 * (X1 - X0) + 4 * Bold) / pitch;

        /// <summary>Half-transparent pixels for glyphs `cover` pitches wide (GlyphSize): with cover 2 every point of the
        /// picture lies under 2 x 2 glyphs whose alphas add up, so each gets 1 - (1 - alpha)^(1/4) and the four together
        /// show the picture's own alpha. Opaque and empty pixels stay; the grid is copied when anything changes.</summary>
        public static int[] ForCover(int[] px, double cover)
        {
            if (cover < 1.5) return px;
            double layers = Math.Round(cover) * Math.Round(cover);
            int[] o = null;
            for (int i = 0; i < px.Length; i++)
            {
                int a = Alpha(px[i]);
                if (a <= 0 || a >= 255) continue;
                if (o == null) o = (int[])px.Clone();
                o[i] = Pack(px[i], Math.Max(1, (int)Math.Round(255 * (1 - Math.Pow(1 - a / 255.0, 1 / layers)))));
            }
            return o ?? px;
        }

        /// <summary>What a pixel looks like under `layers` glyphs of its colour (ForCover the other way, for quick views).</summary>
        public static int AsSeen(int px, double layers)
        {
            int a = Alpha(px);
            if (layers <= 1 || a <= 0 || a >= 255) return px;
            return Pack(px, Math.Min(255, (int)Math.Round(255 * (1 - Math.Pow(1 - a / 255.0, layers)))));
        }

        public sealed class Band
        {
            public int Row0, Row1, Bytes;
            public string Text;
        }

        /// <summary>The picture split into bands of whole rows, each at most maxPixels and maxKb (0 = no byte cap; a band
        /// the column encoding cannot solve is halved until it can):
        /// one text per sign, all for the same sign position (stacked copies). px: rows top-down (Pack: a colour, with
        /// its alpha when half transparent, or -1 for empty). The picture is centred centerX wu right of the sign, its bottom edge `bottom` wu above it.
        /// stale is asked before every band's text is made (null: never); true stops it with an OperationCanceledException
        /// (0.20.132, review: a count nobody waits for any more ran to its end).</summary>
        public static List<Band> Bands(int[] px, int w, int h, double pitch, double centerX, double bottom, double size,
                                       double maxKb, int maxPixels = MaxPixelsPerSign, bool glow = true, Func<bool> stale = null)
        {
            var bands = new List<Band>();
            int r0 = 0;
            while (r0 < h)
            {
                int rows = Math.Min(h - r0, maxPixels / w);
                if (rows < 1) throw new ArgumentException(Lang.F("a row of {0} pixels does not fit one sign", w));
                string text = null;
                int bytes = 0;
                while (AnyPixel(px, w, r0, r0 + rows))                          // transparent rows get no sign
                {
                    if (stale != null && stale()) throw new OperationCanceledException("no longer wanted");
                    try { text = ColumnText(px, w, h, r0, r0 + rows, pitch, centerX, bottom, size, glow); }
                    catch (ArgumentException) when (rows > 1)
                    {
                        rows = Math.Max(1, rows / 2);                           // crosses the sign's height, or too tall
                        continue;
                    }
                    bytes = Encoding.UTF8.GetByteCount(text);
                    if (maxKb <= 0 || bytes <= maxKb * 1024 || rows == 1) break;
                    rows = Math.Max(1, (int)(rows * maxKb * 1024 / bytes * 0.97));
                    text = null;
                }
                if (text != null) bands.Add(new Band { Row0 = r0, Row1 = r0 + rows, Text = text, Bytes = bytes });
                r0 += rows;
            }
            if (bands.Count == 0) throw new ArgumentException(Lang.T("the picture is fully transparent: nothing to put on a sign"));
            return bands;
        }

        /// <summary>What the bands add to the world save (SaveCost.Bytes of their texts), from the UTF-8 counts each band
        /// made already (0.20.132, review: every text was counted again, twice per placement).</summary>
        public static long SaveBytes(List<Band> bands)
        {
            long n = 0;
            foreach (Band b in bands) n += SaveCost.PerSign + b.Bytes;
            return n;
        }

        static bool AnyPixel(int[] px, int w, int r0, int r1)
        {
            for (int i = r0 * w; i < r1 * w; i++)
                if (Drawn(px[i])) return true;
            return false;
        }

        /// <summary>pixels.position_decimals: indent and voffset get 1 decimal when pixel centres fall on the 0.1 wu grid
        /// (pitch a multiple of 0.2), else 2.</summary>
        public static int PositionDecimals(double pitch) => Math.Abs(pitch * 5 - Math.Round(pitch * 5)) < 1e-9 ? 1 : 2;

        /// <summary>One sign text for the rows [r0, r1). glow: the unlit font path (colours as written, day and night);
        /// false: the sign's own lit material, shaded by the scene like any sign text (pixels.items font=None).</summary>
        public static string ColumnText(int[] px, int w, int h, int r0, int r1, double pitch, double centerX, double bottom,
                                        double size, bool glow = true)
        {
            int nd = PositionDecimals(pitch);
            // the glyphs in line order: column by column (left to right), top to bottom (engine.column_cmds)
            var xs = new List<double>();
            var ys = new List<double>();
            var colors = new List<int>();
            for (int c = 0; c < w; c++)
            {
                double x = centerX + (c - (w - 1) / 2.0) * pitch;
                for (int r = r0; r < r1; r++)
                {
                    int col = px[r * w + c];
                    if (!Drawn(col)) continue;
                    xs.Add(x);
                    ys.Add(bottom + pitch / 2 + (h - 1 - r) * pitch);
                    colors.Add(col);
                }
            }
            int n = xs.Count;
            if (n == 0) throw new ArgumentException("a band needs a pixel");

            // the line pitch: the most common step between neighbours of a column, read exactly by TMP. The inputs
            // repeat along a column (its x, its pixel step), so each rounding is remembered for the value it was made for.
            double bbSum = 0.0 + Y0 + Y1;
            var count = new Dictionary<double, int>();
            var firstSeen = new List<double>();
            double mx0 = double.NaN, mr0 = 0, mx1 = double.NaN, mr1 = 0, md = double.NaN, ms = 0;
            for (int i = 0; i + 1 < n; i++)
            {
                double a = ys[i] - size * bbSum, b = ys[i + 1] - size * bbSum;
                if (Memo(xs[i], nd, ref mx0, ref mr0) != Memo(xs[i + 1], nd, ref mx1, ref mr1) || !(a > b + 1e-6)) continue;
                double d = a - b;
                if (d != md)
                {
                    md = d;
                    ms = PyMath.Round(d / 2, 6);
                }
                double step = ms;
                if (count.ContainsKey(step)) count[step]++;
                else { count[step] = 1; firstSeen.Add(step); }
            }
            double best = firstSeen.Count > 0 ? firstSeen[0] : PyMath.Round(pitch / 2, 6);   // no pixel above another:
            foreach (double s in firstSeen) if (count[s] > count[best]) best = s;            // half the pixel pitch
            double lh = ExactLineHeight(best);

            // engine.line_cmds: indent per glyph, voffsets that put every line where it belongs
            double S = PyMath.Round(size, 2);
            double baseK = S * Asc + S * Desc;
            double e = S * bbSum;
            var W = new double[n];
            for (int i = 0; i < n; i++) W[i] = (ys[i] - e + 2 * i * lh - (n - 1) * lh) / 2;
            double K = baseK + 2 * W[0] + baseK;
            var vs = new double[n];
            for (int i = 0; i < n; i++) vs[i] = W[i] + K / 2;
            if (vs[0] < 0 || vs[n - 1] < 0)
            {
                for (int i = 0; i < n; i++) vs[i] = W[i] + baseK / 2;           // first line negative, last not
                if (vs[0] >= 0 || vs[n - 1] < 0)
                {
                    double KL = baseK + 2 * W[n - 1] + baseK;                    // both negative: below the sign
                    for (int i = 0; i < n; i++) vs[i] = W[i] + KL / 2;
                    if (vs[0] >= 0 || vs[n - 1] >= 0)
                        throw new ArgumentException("mixed-sign voffsets are not solvable");
                }
            }
            double dx = 2 * 1.0 * S * ((X0 + X1) / 2 - Adv / 2);
            double tol = 0.5 * (nd == 1 ? 0.1 : 0.01);
            var voff = new double[n];
            var pos = new double[n];
            double maxAbs = 0;
            double mpx = double.NaN, mpr = 0;
            for (int i = 0; i < n; i++)
            {
                voff[i] = i > 0 && Math.Abs(vs[i] - voff[i - 1]) < tol ? voff[i - 1] : PyMath.Round(vs[i], nd);
                pos[i] = Memo(xs[i] - dx - 0, nd, ref mpx, ref mpr);
                maxAbs = Math.Max(maxAbs, Math.Max(Math.Abs(voff[i]), Math.Abs(pos[i])));
            }
            if (maxAbs > 32000)
                throw new ArgumentException(Lang.T("the picture is too large for the column encoding (tag numbers past 32 767): use fewer pixels or a smaller pixel size"));

            // engine.encode_lines: only changed tags are written again
            var sb = new StringBuilder(n * 9 + 64);
            sb.Append("<line-height=").Append(PyMath.Num(lh, 6)).Append('>');
            int stColor = DefaultColor;
            double stVoff = 0, stIndent = 0;
            double mvx = double.NaN, mvr = 0, mix = double.NaN, mir = 0;
            for (int i = 0; i < n; i++)
            {
                if (i > 0) sb.Append('\n');
                else if (glow) sb.Append("<font=").Append(Font).Append('>');
                if (colors[i] != stColor)
                {
                    sb.Append('<');
                    AppendHex(sb, colors[i]);
                    sb.Append('>');
                    stColor = colors[i];
                }
                if (i == 0) sb.Append("<size=").Append(PyMath.Num(S, 2)).Append('>');
                double v = Memo(voff[i], nd, ref mvx, ref mvr);
                if (v != stVoff)
                {
                    sb.Append("<voffset=").Append(PyMath.Num(voff[i], nd)).Append('>');
                    stVoff = v;
                }
                double p = Memo(pos[i], nd, ref mix, ref mir);
                if (p != stIndent)
                {
                    sb.Append("<indent=").Append(PyMath.Num(pos[i], nd)).Append('>');
                    stIndent = p;
                }
                sb.Append(Glyph);
            }
            return sb.ToString();
        }

        /// <summary>PyMath.Round(x, nd), remembered for the last x (the encoder's inputs repeat along a column).</summary>
        static double Memo(double x, int nd, ref double lastX, ref double lastR)
        {
            if (x != lastX || double.IsNaN(lastX))
            {
                lastX = x;
                lastR = PyMath.Round(x, nd);
            }
            return lastR;
        }

        /// <summary>engine.exact_line_height: a line pitch TMP reads exactly and adds up exactly in float32 over tens of
        /// thousands of lines (0.8 drifted and kinked the Starry Night); multiples of 1/4, or of 1/8 and 1/16 when
        /// exact.</summary>
        public static double ExactLineHeight(double step)
        {
            foreach (int q in new[] { 4, 8, 16 })
            {
                double lh = PyMath.RoundInt(step * q) / (double)q;
                if (lh > 0 && Math.Abs(lh - step) < 1e-6 && PyMath.TmpFloat(PyMath.Num(lh, 6)) == lh) return lh;
            }
            foreach (int q in new[] { 4, 2, 1 })
            {
                double lh = Math.Max(1.0 / q, PyMath.RoundInt(step * q) / (double)q);
                if (PyMath.TmpFloat(PyMath.Num(lh, 6)) == lh) return lh;
            }
            return Math.Max(1, PyMath.RoundInt(step));
        }

        /// <summary>engine.short_hex: #rgb when exact, else #rrggbb; a half-transparent pixel adds its alpha (#rgba,
        /// #rrggbbaa).</summary>
        public static string ShortHex(int px)
        {
            var sb = new StringBuilder(10);
            AppendHex(sb, px);
            return sb.ToString();
        }

        /// <summary>ShortHex appended into the text being built (no strings per colour change).</summary>
        public static void AppendHex(StringBuilder sb, int px)
        {
            int r = (px >> 16) & 255, g = (px >> 8) & 255, b = px & 255, a = Alpha(px);
            bool alpha = a < 255;
            sb.Append('#');
            if (r % 17 == 0 && g % 17 == 0 && b % 17 == 0 && (!alpha || a % 17 == 0))
            {
                sb.Append(HexDigit(r / 17)).Append(HexDigit(g / 17)).Append(HexDigit(b / 17));
                if (alpha) sb.Append(HexDigit(a / 17));
                return;
            }
            sb.Append(HexDigit(r >> 4)).Append(HexDigit(r & 15)).Append(HexDigit(g >> 4)).Append(HexDigit(g & 15))
              .Append(HexDigit(b >> 4)).Append(HexDigit(b & 15));
            if (alpha) sb.Append(HexDigit(a >> 4)).Append(HexDigit(a & 15));
        }

        static char HexDigit(int v) => (char)(v < 10 ? '0' + v : 'a' + v - 10);
    }
}
