using System;
using System.Collections.Generic;
using System.Text;

namespace SignLink.Pictures
{
    /// <summary>The text templates of 2 Content › Text (Block, Label, Symbol, Pattern): pieces of sign text placed
    /// exactly, in metres from the sign's centre (x right, y up), as lines of one sign text. Every line restates its
    /// tags; an invisible anchor line of size 1 at voffset 0 opens and closes the text, so the block's vertical
    /// centring is the constant K (TMP's Middle alignment uses the first line's ascender and the last line's descender
    /// only, docs/sign-rendering.md 7c) and a line lands where asked:
    ///   X = P + 2*sx*S*((xMin+xMax)/2 - adv/2),   Y = 2*V + S*(yMin+yMax) - K     (wu; engine.layout_lines)
    /// A glyph's drawn height is 2*S*(yMax-yMin) + 4*bold*S (faux bold grows it); its metrics come from GlyphData.
    /// No Unity here (mod/SignLink.Tests).</summary>
    internal static class Snippets
    {
        public const double WuPerM = 40;
        public const string Header = "<line-height=0>";
        /// <summary>The invisible first and last line: colour alpha 0, size 1, no offset, no scale or turn.</summary>
        public const string Anchor = "<#00000000><size=1><pos=0><voffset=0><scale=1><rotate=0>" + GlyphData.Block;
        /// <summary>Middle alignment's constant between the anchors: the first line's ascender plus the last line's
        /// descender, both the block glyph's font at size 1.</summary>
        public static readonly double K = GlyphData.Jp.A + GlyphData.Jp.D;

        /// <summary>One line of the text: what it shows, where its centre goes (wu), its size, stretch, turn and colour,
        /// and the metrics the placement needs (a label's vertical metrics are a capital's; its advance and drawn box
        /// are the whole word's).</summary>
        public sealed class Line
        {
            public string Text;
            public GlyphData.Glyph Glyph;
            public double Size, Scale = 1, Rotate, X, Y;
            public int Rgb, Alpha = 255;
            public double Adv, BoxLeft, BoxRight;
        }

        /// <summary>The block glyph (■) as the game draws it, measured in game (2026-10-17: sizes 1.5 and 4, &lt;scale&gt; 0.15
        /// to 6, lit and glowing alike): its ink is S*(BlockA*sx + BlockC) em wide and S*BlockH em tall (an em is 2*S wu),
        /// centred where the formulas put it. Part of its width does not stretch with &lt;scale&gt; (TMP's padding for the
        /// bold style), so blocks sized by the glyph's outline (0.8 em, all of it stretching) came out short when wide and
        /// wide when narrow: a QR code's bars missed each other and no phone read it. Other glyphs keep the outline
        /// model (not measured).</summary>
        const double BlockA = 0.726, BlockC = 0.089, BlockH = 0.793;
        /// <summary>The block glyph's TMP quad (the same measurement): S*(QuadA*sx + QuadC) em wide, S*QuadH em tall, its
        /// padding included. The ink is smaller than the quad less TMP's padding: the frame on a block measured by the quad
        /// stood 3 cm off a 1.7 m QR code's sides.</summary>
        const double QuadA = 0.8, QuadC = 0.099, QuadH = 0.852;

        /// <summary>The block glyph's ink from its TMP quad (any unit: the answer is in the quad's): the size and the stretch
        /// read back from the quad, the measured ink of those.</summary>
        public static void BlockInkOfQuad(double quadW, double quadH, out double w, out double h)
        {
            double s = quadH / QuadH;
            double sx = s > 0 ? Math.Max(0, (quadW / s - QuadC) / QuadA) : 0;
            w = s * (BlockA * sx + BlockC);
            h = s * BlockH;
        }
        /// <summary>The narrowest stretch a block takes: a block narrower than BlockC/BlockH of its height cannot be drawn.</summary>
        const double MinScale = 0.02;
        /// <summary>A block narrower than this stretch is drawn lying (Block): a stretch this small is near BlockC's floor.</summary>
        const double LieUnder = 0.25;

        static bool Measured(GlyphData.Glyph g) => g.Ch == GlyphData.Block;

        /// <summary>How tall a glyph of this size is drawn (wu).</summary>
        public static double InkHeight(GlyphData.Glyph g, double size) =>
            Measured(g) ? 2 * size * BlockH : size * (2 * (g.Y1 - g.Y0) + 4 * g.Font.Bold);

        /// <summary>How wide a glyph of this size and stretch is drawn (wu).</summary>
        public static double InkWidth(GlyphData.Glyph g, double size, double scale = 1) =>
            Measured(g) ? 2 * size * (BlockA * Math.Abs(scale) + BlockC) : size * (2 * (g.X1 - g.X0) + 4 * g.Font.Bold) * Math.Abs(scale);

        /// <summary>The size that draws this glyph heightWu tall.</summary>
        public static double SizeFor(GlyphData.Glyph g, double heightWu) => heightWu / InkHeight(g, 1);

        /// <summary>The stretch that draws a glyph of this size widthWu wide.</summary>
        public static double ScaleFor(GlyphData.Glyph g, double size, double widthWu) =>
            Measured(g) ? Math.Max(MinScale, (widthWu / (2 * size) - BlockC) / BlockA) : widthWu / InkWidth(g, size);

        /// <summary>A coloured block w x h metres, its centre x, y metres from the sign's centre.</summary>
        public static List<Line> Block(double wM, double hM, int rgb, int alpha, double xM = 0, double yM = 0)
        {
            GlyphData.Glyph g = GlyphData.Find(GlyphData.Block);
            double size = SizeFor(g, hM * WuPerM), scale = ScaleFor(g, size, wM * WuPerM);
            if (scale < LieUnder)                                           // too narrow to stand: lying, turned a quarter, the
            {                                                               // stretch along its height (1.0.2, review: a 5 cm wide
                size = SizeFor(g, wM * WuPerM);                             // 1 m bar came out 13 cm wide)
                return new List<Line> { Single(g, size, ScaleFor(g, size, hM * WuPerM), 90, rgb, alpha, xM, yM) };
            }
            return new List<Line> { Single(g, size, scale, 0, rgb, alpha, xM, yM) };
        }

        /// <summary>One glyph of the palette, drawn hM tall, mirrored and turned as asked (degrees, anticlockwise as TMP
        /// turns); nothing when the fonts lack it.</summary>
        public static List<Line> Symbol(string ch, double hM, int rgb, int alpha, bool mirror, double rotate, double xM = 0, double yM = 0)
        {
            GlyphData.Glyph g = GlyphData.Find(ch);
            if (g == null) return new List<Line>();
            return new List<Line> { Single(g, SizeFor(g, hM * WuPerM), mirror ? -1 : 1, rotate, rgb, alpha, xM, yM) };
        }

        /// <summary>One line of text whose capitals are letterHM tall, its capitals' centre at x, y. Letters the fonts
        /// lack are counted as 0.3 em wide (a space 0.25).</summary>
        public static List<Line> Label(string text, double letterHM, int rgb, int alpha, double xM = 0, double yM = 0)
        {
            if (string.IsNullOrEmpty(text)) return new List<Line>();
            GlyphData.Glyph cap = GlyphData.Find("H");
            double adv = 0, left = 0, right = 0;
            bool first = true;
            foreach (string ch in CodePoints(text))
            {
                GlyphData.Glyph g = GlyphData.Find(ch);
                double a = g != null ? g.Adv : ch == " " ? 0.25 : 0.3;
                if (first) left = g != null ? g.X0 : 0;
                right = adv + (g != null ? g.X1 : a);
                adv += a;
                first = false;
            }
            return new List<Line>
            {
                new Line { Text = text, Glyph = cap, Size = SizeFor(cap, letterHM * WuPerM), Rgb = rgb, Alpha = alpha,
                           X = xM * WuPerM, Y = yM * WuPerM, Adv = adv, BoxLeft = left, BoxRight = right },
            };
        }

        /// <summary>A grid of one glyph, cols x rows cells of cellM metres, centred at x, y; colour B on every other cell
        /// like a chessboard when alternate, else A everywhere. Nothing when the fonts lack the glyph.</summary>
        public static List<Line> Pattern(string ch, int cols, int rows, double cellM, int rgbA, int alphaA, int rgbB, int alphaB,
                                         bool alternate, double xM = 0, double yM = 0)
        {
            var lines = new List<Line>();
            GlyphData.Glyph g = GlyphData.Find(ch);
            if (g == null || cols < 1 || rows < 1 || cellM <= 0) return lines;
            double cell = cellM * WuPerM, size = SizeFor(g, cell), scale = ScaleFor(g, size, cell);
            for (int r = 0; r < rows; r++)
                for (int c = 0; c < cols; c++)
                {
                    bool b = alternate && (r + c) % 2 == 1;
                    double x = xM * WuPerM + (c - (cols - 1) / 2.0) * cell, y = yM * WuPerM + ((rows - 1) / 2.0 - r) * cell;
                    lines.Add(Single(g, size, scale, 0, b ? rgbB : rgbA, b ? alphaB : alphaA, x / WuPerM, y / WuPerM));
                }
            return lines;
        }

        /// <summary>A QR code (QrCode.Modules), sideM metres wide with its quiet zone of `quiet` modules, centred at x, y: a
        /// light square, then the dark modules over it as rectangles (each grown down and right as far as it stays dark,
        /// so a code is a few hundred lines, not one a module), every rectangle a hair larger than its modules so no seam
        /// shows between two of them. One sign text draws in order, so the dark ones lie over the light one.</summary>
        public static List<Line> Qr(bool[,] modules, double sideM, int darkRgb, int lightRgb, int quiet = 4, double xM = 0, double yM = 0)
        {
            var lines = new List<Line>();
            int n = modules.GetLength(0);
            if (n < 1 || sideM <= 0) return lines;
            double m = sideM / (n + 2 * quiet), half = n / 2.0, over = m * 0.04;
            lines.AddRange(Block(sideM, sideM, lightRgb, 255, xM, yM));
            var used = new bool[n, n];
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    if (!modules[y, x] || used[y, x]) continue;
                    int w = 1, h = 1;
                    while (x + w < n && modules[y, x + w] && !used[y, x + w]) w++;
                    // at most five times as tall as wide: a block narrower than about a ninth of its height cannot be
                    // drawn (BlockC), so a long column goes in parts
                    while (y + h < n && h < 5 * w && DarkRun(modules, used, y + h, x, w)) h++;
                    for (int dy = 0; dy < h; dy++)
                        for (int dx = 0; dx < w; dx++) used[y + dy, x + dx] = true;
                    lines.AddRange(Block(w * m + over, h * m + over, darkRgb, 255, xM + (x + w / 2.0 - half) * m, yM + (half - y - h / 2.0) * m));
                }
            return lines;
        }

        static bool DarkRun(bool[,] modules, bool[,] used, int y, int x, int w)
        {
            for (int i = x; i < x + w; i++)
                if (!modules[y, i] || used[y, i]) return false;
            return true;
        }

        static Line Single(GlyphData.Glyph g, double size, double scale, double rotate, int rgb, int alpha, double xM, double yM) =>
            new Line { Text = g.Ch, Glyph = g, Size = size, Scale = scale, Rotate = rotate, Rgb = rgb, Alpha = alpha,
                       X = xM * WuPerM, Y = yM * WuPerM, Adv = g.Adv, BoxLeft = g.X0, BoxRight = g.X1 };

        /// <summary>The pos and voffset that put the line's centre where it asks (the formulas inverted).</summary>
        public static void Tags(Line l, out double pos, out double voffset)
        {
            pos = l.X - 2 * l.Scale * l.Size * ((l.BoxLeft + l.BoxRight) / 2 - l.Adv / 2);
            voffset = (l.Y - l.Size * (l.Glyph.Y0 + l.Glyph.Y1) + K) / 2;
        }

        /// <summary>The reference model: where a one-glyph line with these tags lands between the anchors (wu).</summary>
        public static void Model(GlyphData.Glyph g, double pos, double voffset, double size, double scale, out double x, out double y)
        {
            x = pos + 2 * scale * size * ((g.X0 + g.X1) / 2 - g.Adv / 2);
            y = 2 * voffset + size * (g.Y0 + g.Y1) - K;
        }

        /// <summary>The line as sign text: every tag restated (TMP carries them across lines), the colour short when
        /// exact, alpha when under 255.</summary>
        public static string Render(Line l)
        {
            Tags(l, out double pos, out double voffset);
            var sb = new StringBuilder(64);
            sb.Append("<pos=").Append(PyMath.Num(pos, 2)).Append("><voffset=").Append(PyMath.Num(voffset, 2))
              .Append("><size=").Append(PyMath.Num(l.Size, 3)).Append("><scale=").Append(PyMath.Num(l.Scale, 3))
              .Append("><rotate=").Append(PyMath.Num(l.Rotate, 1)).Append("><");
            PixelText.AppendHex(sb, PixelText.Pack(l.Rgb, Math.Max(1, Math.Min(255, l.Alpha))));
            return sb.Append('>').Append(l.Text).ToString();
        }

        /// <summary>The whole text for these lines: the header, the anchors and the lines between them; "" for none.</summary>
        public static string Compose(IList<Line> lines)
        {
            if (lines == null || lines.Count == 0) return "";
            var sb = new StringBuilder(Header).Append(Anchor).Append('\n');
            foreach (Line l in lines) sb.Append(Render(l)).Append('\n');
            return sb.Append(Anchor).ToString();
        }

        /// <summary>The text with these lines added: an empty text gets the structure; a text with the structure takes
        /// them as whole lines where the caret is (never before the first anchor or after the last); any other text
        /// gets them appended on their own lines (the sign shows what that gives).</summary>
        public static string Insert(string text, IList<Line> lines, int caret)
        {
            if (lines == null || lines.Count == 0) return text ?? "";
            if (string.IsNullOrEmpty(text)) return Compose(lines);
            var body = new StringBuilder();
            foreach (Line l in lines)
            {
                if (body.Length > 0) body.Append('\n');
                body.Append(Render(l));
            }
            string head = Header + Anchor + "\n";
            if (text.StartsWith(head, StringComparison.Ordinal) && text.EndsWith("\n" + Anchor, StringComparison.Ordinal))
            {
                int last = text.Length - Anchor.Length;                      // where the last anchor's line starts
                int at = Math.Min(Math.Max(caret, head.Length), last);
                int lineStart = at <= 0 ? 0 : text.LastIndexOf('\n', at - 1) + 1;
                lineStart = Math.Min(Math.Max(lineStart, head.Length), last);
                return text.Substring(0, lineStart) + body + "\n" + text.Substring(lineStart);
            }
            return text + "\n" + body;
        }

        /// <summary>The text's code points as strings (an emoji is two chars).</summary>
        public static IEnumerable<string> CodePoints(string s)
        {
            for (int i = 0; i < s.Length; i++)
            {
                if (char.IsHighSurrogate(s[i]) && i + 1 < s.Length && char.IsLowSurrogate(s[i + 1]))
                {
                    yield return s.Substring(i, 2);
                    i++;
                }
                else yield return s.Substring(i, 1);
            }
        }
    }
}
