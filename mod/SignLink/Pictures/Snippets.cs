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
    /// No Unity here (mod/SignLink.Tests)..</summary>
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

        /// <summary>The size that draws this glyph heightWu tall.</summary>
        public static double SizeFor(GlyphData.Glyph g, double heightWu) => heightWu / (2 * (g.Y1 - g.Y0) + 4 * g.Font.Bold);

        /// <summary>How wide a glyph of this size is drawn (wu).</summary>
        public static double DrawnWidth(GlyphData.Glyph g, double size) => size * (2 * (g.X1 - g.X0) + 4 * g.Font.Bold);

        /// <summary>A coloured block w x h metres, its centre x, y metres from the sign's centre.</summary>
        public static List<Line> Block(double wM, double hM, int rgb, int alpha, double xM = 0, double yM = 0)
        {
            GlyphData.Glyph g = GlyphData.Find(GlyphData.Block);
            double size = SizeFor(g, hM * WuPerM);
            return new List<Line> { Single(g, size, wM * WuPerM / DrawnWidth(g, size), 0, rgb, alpha, xM, yM) };
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
            double cell = cellM * WuPerM, size = SizeFor(g, cell), scale = cell / DrawnWidth(g, size);
            for (int r = 0; r < rows; r++)
                for (int c = 0; c < cols; c++)
                {
                    bool b = alternate && (r + c) % 2 == 1;
                    double x = xM * WuPerM + (c - (cols - 1) / 2.0) * cell, y = yM * WuPerM + ((rows - 1) / 2.0 - r) * cell;
                    lines.Add(Single(g, size, scale, 0, b ? rgbB : rgbA, b ? alphaB : alphaA, x / WuPerM, y / WuPerM));
                }
            return lines;
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
