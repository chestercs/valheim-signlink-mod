using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace SignLink.Pictures
{
    /// <summary>A pixel picture read back from its sign texts, for quick views of presets and of what a sign holds. It
    /// inverts the column encoding (PixelText, engine.column_cmds): every line is one ■ at x = its indent and
    /// y = 2V - 2iL + e - K + (N-1)L (engine.line_cmds solved for the glyph), with K from the first and last voffsets.
    /// Texts of a stack combine, since all are relative to the same sign. Anything else (other glyphs, rotations, pos
    /// tags: Unicode art) is not a pixel picture: Read returns false. No Unity here (mod/SignLink.Tests).</summary>
    internal static class TextReadback
    {
        const char Square = '■';
        const double Asc = 1.16, Desc = -0.288, BbSum = 0.0 + -0.02 + 0.78;
        const int DefaultColor = 0x1E1A12;

        struct Dot
        {
            public double X, Y;
            public int Color;
        }

        static readonly Regex Tags = new Regex("<[^<>]*>"), Spaces = new Regex(@"\s+");

        /// <summary>The visible words of sign texts, tags dropped and spaces collapsed, at most max characters (to tell
        /// signs apart); only the start of a long text is read.</summary>
        public static string Words(IList<string> texts, int max)
        {
            var sb = new StringBuilder();
            foreach (string t in texts)
            {
                if (string.IsNullOrEmpty(t) || sb.Length >= max) continue;
                string words = Spaces.Replace(Tags.Replace(t.Length > 4000 ? t.Substring(0, 4000) : t, ""), " ").Trim();
                if (words.Length == 0) continue;
                if (sb.Length > 0) sb.Append(" / ");
                sb.Append(words);
            }
            return sb.Length > max ? sb.ToString(0, max).TrimEnd() + "..." : sb.ToString();
        }

        /// <summary>texts: one sign text each (a stack's, or one). Returns the grid (rows top-down, PixelText.Pack). asSeen:
        /// half-transparent pixels as they look where the glyphs overlap (PixelText.AsSeen), else as written.</summary>
        public static bool Read(IList<string> texts, out int w, out int h, out int[] px, bool asSeen = false)
        {
            px = null;
            if (!ReadFrames(new[] { texts }, out w, out h, out List<int[]> grids, asSeen)) return false;
            px = grids[0];
            return true;
        }

        /// <summary>Several pictures on one shared canvas (the frames of an animation, whose outlines differ: the cat's
        /// tail swings), each frame given as its sign texts.</summary>
        public static bool ReadFrames(IList<IList<string>> frames, out int w, out int h, out List<int[]> grids, bool asSeen = false) =>
            ReadFrames(frames, out w, out h, out grids, out _, asSeen);

        /// <summary>A picture wider or taller than this is not shown (no texture that size): a two-glyph text with a huge
        /// indent would read as one. More lines than MaxLines are refused before anything is allocated per line.</summary>
        public const int MaxSide = 4096, MaxLines = 2000000;

        /// <summary>The same, and the pixel pitch in wu (the picture is w x pitch / 40 metres wide).</summary>
        public static bool ReadFrames(IList<IList<string>> frames, out int w, out int h, out List<int[]> grids, out double pitchWu,
                                      bool asSeen) =>
            ReadFrames(frames, out w, out h, out grids, out pitchWu, out _, out _, asSeen);

        /// <summary>One picture's sign texts (a stack's) read back to edit it without its file (0.20.49): the pixels drawn
        /// (their box: see-through margins hold no glyph, so they are not there), the pitch as it shows, and the box's
        /// middle in the sign's own text frame (wu), where the encoder's centre and bottom (PixelText.Bands) put it, as
        /// near as its rounding lets (indents to 0.1 or 0.01 wu, PositionDecimals; heights twice that).</summary>
        public static bool ReadBox(IList<string> texts, out int w, out int h, out int[] px, out double pitchWu, out double midX,
                                   out double midY, bool asSeen)
        {
            px = null;
            midX = midY = 0;
            if (!ReadFrames(new[] { texts }, out w, out h, out List<int[]> grids, out pitchWu, out double left, out double top, asSeen))
                return false;
            px = grids[0];
            midX = left + (w - 1) * pitchWu / 2;
            midY = top - (h - 1) * pitchWu / 2;
            return true;
        }

        /// <summary>The core: also the top-left pixel's place (its dot, wu).</summary>
        static bool ReadFrames(IList<IList<string>> frames, out int w, out int h, out List<int[]> grids, out double pitchWu,
                               out double left, out double top, bool asSeen)
        {
            left = top = 0;
            pitchWu = 0;
            w = h = 0;
            grids = null;
            long lines = 0;
            foreach (IList<string> texts in frames)
                foreach (string t in texts)
                    if (!string.IsNullOrEmpty(t))
                        for (int i = t.IndexOf('\n'); i >= 0; i = t.IndexOf('\n', i + 1))
                            if (++lines > MaxLines) return false;
            var dots = new List<List<Dot>>();
            double pitch = 0;
            foreach (IList<string> texts in frames)
            {
                var mine = new List<Dot>();
                foreach (string t in texts)
                {
                    if (string.IsNullOrEmpty(t)) continue;
                    if (!ReadOne(t, mine, out double p, asSeen)) return false;
                    if (pitch == 0) pitch = p;
                    else if (Math.Abs(p - pitch) > 1e-6) return false;              // the texts disagree
                }
                dots.Add(mine);
            }
            if (pitch <= 0) return false;
            double minX = double.MaxValue, maxY = double.MinValue, maxX = double.MinValue, minY = double.MaxValue;
            foreach (List<Dot> frame in dots)
                foreach (Dot d in frame)
                {
                    minX = Math.Min(minX, d.X); maxX = Math.Max(maxX, d.X);
                    minY = Math.Min(minY, d.Y); maxY = Math.Max(maxY, d.Y);
                }
            if (minX > maxX) return false;
            double dw = Math.Round((maxX - minX) / pitch) + 1, dh = Math.Round((maxY - minY) / pitch) + 1;
            if (!(dw >= 1 && dh >= 1 && dw <= MaxSide && dh <= MaxSide && dw * dh * frames.Count <= 8000000)) return false;
            w = (int)dw;
            h = (int)dh;
            pitchWu = pitch;
            // the top-left pixel's place from every dot (its column and row known), not from the outermost ones alone:
            // the indents and voffsets carry the encoder's rounding (0.05 wu, twice that for a height), which averages out
            double sumX = 0, sumY = 0;
            long dotsN = 0;
            foreach (List<Dot> frame in dots)
                foreach (Dot d in frame)
                {
                    sumX += d.X - Math.Round((d.X - minX) / pitch) * pitch;
                    sumY += d.Y + Math.Round((maxY - d.Y) / pitch) * pitch;
                    dotsN++;
                }
            left = dotsN > 0 ? sumX / dotsN : minX;
            top = dotsN > 0 ? sumY / dotsN : maxY;
            grids = new List<int[]>();
            foreach (List<Dot> frame in dots)
            {
                var px = new int[w * h];
                for (int i = 0; i < px.Length; i++) px[i] = -1;
                foreach (Dot d in frame)
                {
                    int c = (int)Math.Round((d.X - minX) / pitch), r = (int)Math.Round((maxY - d.Y) / pitch);
                    if (c >= 0 && c < w && r >= 0 && r < h) px[r * w + c] = d.Color;
                }
                grids.Add(px);
            }
            return true;
        }

        static bool ReadOne(string text, List<Dot> dots, out double pitch, bool asSeen)
        {
            pitch = 0;
            double lh = 0, size = 0, voff = 0, indent = 0;
            int color = DefaultColor;
            var lines = new List<double[]>();                   // x (indent), V, colour
            int i = 0;
            bool glyphOnLine = false;
            while (i < text.Length)
            {
                char ch = text[i];
                if (ch == '<')
                {
                    int end = text.IndexOf('>', i);
                    if (end < 0) return false;
                    string tag = text.Substring(i + 1, end - i - 1);
                    i = end + 1;
                    if (tag.StartsWith("line-height=", StringComparison.Ordinal)) { if (!Num(tag, 12, out lh)) return false; }
                    else if (tag.StartsWith("size=", StringComparison.Ordinal)) { if (!Num(tag, 5, out size)) return false; }
                    else if (tag.StartsWith("voffset=", StringComparison.Ordinal)) { if (!Num(tag, 8, out voff)) return false; }
                    else if (tag.StartsWith("indent=", StringComparison.Ordinal)) { if (!Num(tag, 7, out indent)) return false; }
                    else if (tag.StartsWith("#", StringComparison.Ordinal)) { if (!Color(tag, out color)) return false; }
                    else if (tag.StartsWith("font=", StringComparison.Ordinal) || tag == "/font") { }
                    else return false;                                  // pos, rotate, scale, sprite ...: not ours
                    continue;
                }
                if (ch == '\n')
                {
                    if (!glyphOnLine) return false;
                    glyphOnLine = false;
                    i++;
                    continue;
                }
                if (ch != Square || glyphOnLine) return false;
                lines.Add(new[] { indent, voff, color });
                glyphOnLine = true;
                i++;
            }
            int n = lines.Count;
            if (n == 0 || size <= 0) return false;
            if (n == 1) lh = lh > 0 ? lh : 1;
            if (lh <= 0) return false;
            pitch = 2 * lh;
            double S = size, e = S * BbSum, baseK = S * Asc + S * Desc;
            double v0 = lines[0][1], vl = lines[n - 1][1];
            double K = baseK + Math.Max(v0, 0) + Math.Min(vl, 0);
            var xs = new double[n];
            var ys = new double[n];
            for (int k = 0; k < n; k++)
            {
                xs[k] = lines[k][0];
                ys[k] = 2 * lines[k][1] - 2 * k * lh + e - K + (n - 1) * lh;
            }
            // the line height is half the pixel pitch, unless the picture's pixels lie at least two rows apart in every
            // column (the encoder takes the most common step: PixelText.ColumnText); the smallest step between columns
            // says (their indents are exact to 0.005 wu, unlike heights, which carry the voffsets' rounding)
            double step = SmallestStep(xs);
            if (step > 0 && step < pitch) pitch /= Math.Max(1, Math.Round(pitch / step));
            double cover = PixelText.CoverOf(S, pitch), layers = asSeen && cover >= 1.5 ? Math.Round(cover) * Math.Round(cover) : 1;
            for (int k = 0; k < n; k++)
                dots.Add(new Dot { X = xs[k], Y = ys[k], Color = PixelText.AsSeen((int)lines[k][2], layers) });
            return true;
        }

        static bool Num(string tag, int skip, out double v) =>
            double.TryParse(tag.Substring(skip), NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint,
                            CultureInfo.InvariantCulture, out v) && !double.IsNaN(v) && !double.IsInfinity(v);

        /// <summary>The smallest gap between distinct values (positions are rounded to 0.01 wu: closer ones are one), or
        /// 0 when all are one.</summary>
        static double SmallestStep(double[] v)
        {
            var sorted = (double[])v.Clone();
            Array.Sort(sorted);
            double best = 0;
            for (int i = 1; i < sorted.Length; i++)
            {
                double d = sorted[i] - sorted[i - 1];
                if (d > 0.005 && (best == 0 || d < best)) best = d;
            }
            return best;
        }

        /// <summary>#rgb, #rgba, #rrggbb, #rrggbbaa as a grid pixel (PixelText.Pack).</summary>
        static bool Color(string tag, out int px)
        {
            px = 0;
            string hex = tag.Substring(1);
            if (hex.Length == 3 || hex.Length == 4)
            {
                var sb = new StringBuilder(8);
                foreach (char c in hex) sb.Append(c).Append(c);
                hex = sb.ToString();
            }
            if (hex.Length != 6 && hex.Length != 8) return false;
            int a = 255;
            if (!int.TryParse(hex.Substring(0, 6), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out int rgb) ||
                (hex.Length == 8 && !int.TryParse(hex.Substring(6), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out a)))
                return false;
            px = PixelText.Pack(rgb, a);
            return true;
        }
    }
}
