using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace SignLink.Pictures
{
    /// <summary>Resizes and moves a sign text of placed glyphs (glyph art, pixel pictures) exactly, by the layout model of
    /// docs/sign-rendering.md sections 2 and 7c (engine.layout_lines). A glyph's x is its line's pen (its pos, else the
    /// indent then in force) plus terms of its size; its y is 2 V_i - 2 i L + S_i (...) - K with K = S_0 A_0 + S_L D_L +
    /// max(V_0, 0) + min(V_L, 0) - (N-1) L, where V_0 and V_L are the voffsets of the first and the last line (one line:
    /// V - S (A + D) + ...). So every length (size, pos, indent, voffset, line-height) times k scales the piece k times
    /// around the sign's centre; pos and indent plus d move it d wu right; and voffsets plus δ move it f(δ) wu up, with
    /// f(δ) = 2δ - [max(V_0 + δ, 0) - max(V_0, 0)] - [min(V_L + δ, 0) - min(V_L, 0)], which never falls and is solved
    /// for the wanted shift. A line height must stay one TMP reads and adds up exactly (PixelText.ExactLineHeight), so
    /// pixel pictures take only the sizes that keep it (Snap). Texts with tags the model does not cover are left alone
    /// (Supports is false).</summary>
    internal static class TextTransform
    {
        static readonly char[] Brackets = { '<', '>' };

        /// <summary>Where a tag starting at i ends (its '&gt;'), or -1 when none starts there: '&lt;', then anything but
        /// '&lt;' and '&gt;', then '&gt;' (the regex &lt;([^&lt;&gt;]*)&gt; anchored at i). One scan for every pass, with no
        /// Match or string per tag (0.20.132, review: five regex passes over every tag of a 40 000-glyph picture on the
        /// main thread, at every settle of the live preview). Linear: the search stops at the next '&lt;', so a text of
        /// '&lt;'s is read once (an unanchored Match per '&lt;' once made it quadratic).</summary>
        static int TagEnd(string text, int i)
        {
            if (text[i] != '<') return -1;
            int j = text.IndexOfAny(Brackets, i + 1);
            return j >= 0 && text[j] == '>' ? j : -1;
        }

        /// <summary>What a tag is, by its name (before '=', trimmed, lower case, up to a space).</summary>
        enum Kind { Other, Plain, Size, Pos, Voffset, Indent, LineHeight }

        static bool IsLength(Kind k) => k >= Kind.Size;

        static readonly string[] Names =
        {
            "size", "pos", "voffset", "indent", "line-height",
            "scale", "rotate", "font", "/font", "alpha", "color", "/color", "b", "/b", "i", "/i", "u", "/u", "s", "/s",
            "sprite", "br", "/scale", "/rotate", "/alpha",
        };
        static readonly Kind[] Kinds =
        {
            Kind.Size, Kind.Pos, Kind.Voffset, Kind.Indent, Kind.LineHeight,
            Kind.Plain, Kind.Plain, Kind.Plain, Kind.Plain, Kind.Plain, Kind.Plain, Kind.Plain, Kind.Plain, Kind.Plain, Kind.Plain,
            Kind.Plain, Kind.Plain, Kind.Plain, Kind.Plain, Kind.Plain, Kind.Plain, Kind.Plain, Kind.Plain, Kind.Plain, Kind.Plain,
        };

        /// <summary>The kind of the tag whose body is text[b0, b1): its name as the regex code read it (the part before '=',
        /// trimmed, lower case, cut at a space), compared in place: a colour (#...) or a known name makes no string.</summary>
        static Kind KindOf(string text, int b0, int b1)
        {
            int eq = text.IndexOf('=', b0, b1 - b0);
            int s = b0, e = eq < 0 ? b1 : eq;
            while (s < e && char.IsWhiteSpace(text[s])) s++;
            while (e > s && char.IsWhiteSpace(text[e - 1])) e--;
            int sp = text.IndexOf(' ', s, e - s);
            if (sp >= 0) e = sp;
            int n = e - s;
            if (n == 0) return Kind.Other;
            if (text[s] == '#') return Kind.Plain;                         // a colour: Check lets it through
            for (int k = 0; k < Names.Length; k++)
            {
                string name = Names[k];
                if (name.Length != n) continue;
                int j = 0;
                while (j < n && char.ToLowerInvariant(text[s + j]) == name[j]) j++;
                if (j == n) return Kinds[k];
            }
            return Kind.Other;
        }

        /// <summary>The name a length tag is written back with (Apply): Name's, which for these is the known name.</summary>
        static string NameOf(Kind k) => Names[(int)k - (int)Kind.Size];

        /// <summary>What counts as a glyph in Check and Apply alike: not a line break, a space, a variation selector or
        /// the second half of a surrogate pair.</summary>
        static bool IsGlyph(char c) => !(c == '\n' || c == '\r' || c == ' ' || c == '︎' || c == '️' || char.IsLowSurrogate(c));

        /// <summary>The game's text reader turns \n, \r, \t, \u and \U sequences into characters the model does not
        /// know (docs/sign-rendering.md 3).</summary>
        static bool IsEscape(string text, int backslash) =>
            backslash + 1 < text.Length && "nrtuU".IndexOf(text[backslash + 1]) >= 0;
        const double MaxTag = 32000;                         // TMP tag numbers end at 32 767 (TMP_Text.cs:5270)

        /// <summary>Can every text be resized and moved (only tags the model covers, lengths as plain numbers, several
        /// lines only with a line height)? why: the first tag or reason that stops it.</summary>
        public static bool Supports(IList<string> texts, out string why)
        {
            foreach (string t in texts)
                if (!Check(t, out why)) return false;
            why = null;
            return true;
        }

        /// <summary>One text: only tags the model covers, lengths as absolute plain numbers, an absolute size before the
        /// first glyph (without one the game sizes the text itself, auto-size 1..8, and a size or a move changes nothing
        /// or the wrong thing), several lines only with a line height. An empty text is fine.</summary>
        static bool Check(string text, out string why)
        {
            why = null;
            bool multi = false, sized = false;
            int i = 0;
            while (i < text.Length)
            {
                int end = TagEnd(text, i);                                      // a tag starts here, or a character
                if (end >= 0)
                {
                    int b0 = i + 1;
                    Kind kind = KindOf(text, b0, end);
                    i = end + 1;
                    if (kind == Kind.LineHeight) multi = true;
                    if (IsLength(kind))
                    {
                        if (!TryValue(text, b0, end, out _, out char first) || (kind == Kind.Size && (first == '+' || first == '-')))
                        {
                            why = $"<{text.Substring(b0, end - b0)}> is not an absolute plain number";
                            return false;
                        }
                        if (kind == Kind.Size) sized = true;
                    }
                    else if (kind == Kind.Other)
                    {
                        why = $"<{text.Substring(b0, end - b0)}> is not something SignLink can resize";
                        return false;
                    }
                    continue;
                }
                char c = text[i++];
                if (c == '\\' && IsEscape(text, i - 1))
                {
                    why = "a backslash escape (\\n, \\r, \\t, \\u) that the game reads as a character";
                    return false;
                }
                if (!IsGlyph(c)) continue;
                if (!sized)
                {
                    why = "plain text without a size: the game sizes it itself";
                    return false;
                }
            }
            if (!multi && (text.IndexOf('\n') >= 0 || text.Contains("<br>")))
            {
                why = "several lines without a line height";
                return false;
            }
            return true;
        }

        /// <summary>The number of the tag whose body is text[b0, b1): plain digits with a sign and a point after the '=',
        /// trimmed, finite (no exponent, no NaN or Infinity); first: its first character (a size's sign).</summary>
        static bool TryValue(string text, int b0, int b1, out double v, out char first)
        {
            int eq = text.IndexOf('=', b0, b1 - b0);
            string raw = eq < 0 ? "" : text.Substring(eq + 1, b1 - eq - 1).Trim();
            first = raw.Length > 0 ? raw[0] : '\0';
            return double.TryParse(raw, NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out v) &&
                   !double.IsNaN(v) && !double.IsInfinity(v);
        }

        /// <summary>The nearest size to k that keeps every line height exact (texts without one take k as it is), or 0
        /// when none does.</summary>
        public static double Snap(IList<string> texts, double k) => Snap(Heights(texts), k);

        /// <summary>At most this many line heights are kept (a picture has one or two): a crafted text with thousands made
        /// every Snap check them all (0.20.132, review).</summary>
        public const int MaxHeights = 64;

        /// <summary>The texts' line heights, each once, in the order met, the first MaxHeights of them (Snap's part that
        /// reads every tag: worth keeping for a big piece).</summary>
        public static List<double> Heights(IList<string> texts)
        {
            var heights = new List<double>();
            var seen = new HashSet<double>();                               // membership: a List's Contains was O(n^2)
            foreach (string t in texts)
                for (int i = t.IndexOf('<'); i >= 0 && heights.Count < MaxHeights;)
                {
                    int end = TagEnd(t, i);
                    if (end < 0)
                    {
                        i = t.IndexOf('<', i + 1);
                        continue;
                    }
                    if (KindOf(t, i + 1, end) == Kind.LineHeight && TryValue(t, i + 1, end, out double l, out _) && l > 0 && seen.Add(l))
                        heights.Add(l);
                    i = t.IndexOf('<', end + 1);
                }
            return heights;
        }

        public static double Snap(List<double> heights, double k)
        {
            if (heights.Count == 0) return k;
            double best = 0;
            foreach (int q in new[] { 16, 8, 4 })
            {
                double l0 = PyMath.RoundInt(k * heights[0] * q) / (double)q;
                if (l0 <= 0) l0 = 1.0 / q;
                double kk = l0 / heights[0];
                if (heights.TrueForAll(l => Exact(l * kk))) { best = kk; break; }
            }
            return best;
        }

        static bool Exact(double lh)
        {
            double r = Math.Round(lh * 16) / 16;
            return lh > 0 && Math.Abs(r - lh) < 1e-9 && PyMath.TmpFloat(PyMath.Num(r, 6)) == r;
        }

        /// <summary>The text k times as large around the sign's centre and then moved dx wu right and dy wu up
        /// (1 m = 40 wu). Throws when a tag number would pass what TMP reads.</summary>
        public static string Apply(string text, double k, double dx, double dy)
        {
            if (text.Length == 0 || (k == 1 && dx == 0 && dy == 0)) return text;
            if (!Check(text, out string why)) throw new ArgumentException(why);

            // the voffsets of the first and the last line's glyph, and whether the first glyph has an indent, pos and
            // voffset of its own (else one goes in front)
            bool multi = false;
            double v = 0, v0 = 0, vl = 0;
            bool seenGlyph = false, indentFirst = false, posFirst = false, voffFirst = false;
            int i = 0;
            while (i < text.Length)
            {
                int end = TagEnd(text, i);                                      // a tag starts here, or a character
                if (end >= 0)
                {
                    Kind kind = KindOf(text, i + 1, end);
                    if (kind == Kind.LineHeight) multi = true;
                    if (kind == Kind.Voffset && TryValue(text, i + 1, end, out double val, out _))
                    {
                        v = val;
                        if (!seenGlyph) voffFirst = true;
                    }
                    if (!seenGlyph && kind == Kind.Indent) indentFirst = true;
                    if (!seenGlyph && kind == Kind.Pos) posFirst = true;
                    i = end + 1;
                    continue;
                }
                char c = text[i++];
                if (!IsGlyph(c)) continue;
                if (!seenGlyph) v0 = v;
                seenGlyph = true;
                vl = v;
            }
            v0 *= k;
            vl *= k;
            double delta = dy == 0 ? 0 : PyMath.Round(Solve(v0, vl, dy), 2);

            if (!(Math.Abs(dx) <= MaxTag) || !(Math.Abs(delta) <= MaxTag))
                throw new ArgumentException("too far from the sign: its tag numbers would pass what a sign reads");
            var sb = new StringBuilder(text.Length + 48);
            if (dx != 0 && !(multi ? indentFirst : posFirst))
                sb.Append(multi ? "<indent=" : "<pos=").Append(Number(dx)).Append('>');
            if (delta != 0 && !voffFirst) sb.Append("<voffset=").Append(Number(delta)).Append('>');
            int last = 0;
            for (int at = text.IndexOf('<'); at >= 0;)
            {
                int end = TagEnd(text, at);
                if (end < 0)
                {
                    at = text.IndexOf('<', at + 1);
                    continue;
                }
                Kind kind = KindOf(text, at + 1, end);
                if (IsLength(kind) && TryValue(text, at + 1, end, out double val, out _))
                {
                    double nv = val * k;
                    if (kind == Kind.Pos || kind == Kind.Indent) nv += dx;
                    if (kind == Kind.Voffset) nv += delta;
                    if (kind == Kind.LineHeight) nv = PyMath.Round(nv, 6);
                    if (!(Math.Abs(nv) <= MaxTag))
                        throw new ArgumentException("too large or too far from the sign: its tag numbers would pass what a sign reads");
                    sb.Append(text, last, at - last).Append('<').Append(NameOf(kind)).Append('=').Append(Number(nv)).Append('>');
                    last = end + 1;
                }
                at = text.IndexOf('<', end + 1);
            }
            sb.Append(text, last, text.Length - last);
            return sb.ToString();
        }

        /// <summary>A Text put the other way (FitTexts.ForPut, the text made at the sign's centre, 0, 0): a sign's text
        /// with the fit it was put with (its stored scale, Right and Up)
        /// taken off, as it was made: moved back, then 1/k times as large. The Text source loads a sign's text so (0.20.68,
        /// the author: a chessboard put at its smallest read back as the largest): the editor always holds a text as made,
        /// whatever its size on the sign. A text SignLink cannot resize, or numbers past what a sign reads: as it is.</summary>
        public static string Unfit(string text, Fit fit)
        {
            if (string.IsNullOrEmpty(text) || !Check(text, out _)) return text ?? "";
            double k = fit.Scale > 0 ? fit.Scale : 1;
            fit.Scale = k;
            fit.Offset(0, 0, out double dx, out double dy);
            try
            {
                string back = dx == 0 && dy == 0 ? text : Apply(text, 1, -dx, -dy);   // the same move back: f(-δ) = -f(δ)
                if (Math.Abs(k - 1) >= 1e-9) back = Apply(back, 1 / k, 0, 0);
                // the move put an indent, pos or voffset in front of the first glyph when it had none: back at 0, as
                // it was without one (before any glyph, 0 is where the line starts)
                foreach (string zero in new[] { "<indent=0>", "<pos=0>", "<voffset=0>" })
                    if (back.StartsWith(zero, StringComparison.Ordinal)) back = back.Substring(zero.Length);
                return back;
            }
            catch (ArgumentException)
            {
                return text;
            }
        }

        /// <summary>The same for every text of a stack or every frame of an animation.</summary>
        public static string[] Apply(IList<string> texts, double k, double dx, double dy)
        {
            var o = new string[texts.Count];
            for (int i = 0; i < texts.Count; i++) o[i] = Apply(texts[i], k, dx, dy);
            return o;
        }

        /// <summary>The voffset change δ that moves every line dy wu up: f(δ) = dy, f as in the summary.</summary>
        static double Solve(double v0, double vl, double dy)
        {
            double F(double d) => 2 * d - (Math.Max(v0 + d, 0) - Math.Max(v0, 0)) - (Math.Min(vl + d, 0) - Math.Min(vl, 0));
            double lo = -4 * MaxTag, hi = 4 * MaxTag;
            for (int n = 0; n < 200; n++)
            {
                double mid = (lo + hi) / 2;
                if (F(mid) < dy) lo = mid;
                else hi = mid;
            }
            return (lo + hi) / 2;
        }

        static string Number(double v) => PyMath.Num(v, 4);
    }
}
