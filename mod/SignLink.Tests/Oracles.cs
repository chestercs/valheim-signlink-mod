using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using SignLink.Pictures;

/// <summary>The references (0.20.132, review): Resampler.Grid and TextTransform as they were before the row-streamed
/// resampler and the hand-written tag scanner, kept in the test project only (as PyMathSlow is). Program.cs compares the
/// plugin's code with them on many inputs: the outputs must stay byte for byte the same.</summary>
static class OldResampler
{
    public static int[] Grid(byte[] rgba, int w, int h, int W, int H, bool opaque)
    {
        int n = w * h;
        var alpha = new float[n];
        var lin = new float[3][];
        for (int k = 0; k < 3; k++) lin[k] = new float[n];
        for (int i = 0; i < n; i++)
        {
            float a = opaque ? 1f : (float)(rgba[i * 4 + 3] / 255f);
            alpha[i] = a;
            for (int k = 0; k < 3; k++) lin[k][i] = (float)(LinearOf[rgba[i * 4 + k]] * a);
        }
        float[] al = Resize(alpha, w, h, W, H);
        var out3 = new float[3][];
        for (int k = 0; k < 3; k++) out3[k] = Resize(lin[k], w, h, W, H);

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
                px[i] = a8 < Resampler.MinAlpha ? -1 : a8 >= Resampler.SolidAlpha ? rgb : PixelText.Pack(rgb, a8);
            }
        }
        return px;
    }

    static float ToLinear(float c) =>
        c <= 0.04045f ? (float)(c / 12.92f) : (float)Math.Pow((float)((float)(c + 0.055f) / 1.055f), 2.4f);

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

    public static float[] Resize(float[] src, int w, int h, int W, int H)
    {
        if (w == W && h == H) return (float[])src.Clone();
        Coeffs(w, W, out int kh, out int[] bh, out double[] ch);
        Coeffs(h, H, out int kv, out int[] bv, out double[] cv);
        int yFirst = bv[0], yLast = bv[(H - 1) * 2] + bv[(H - 1) * 2 + 1];
        float[] img = src;
        int iw = w, ih = h;
        if (W != w)
        {
            for (int i = 0; i < H; i++) bv[i * 2] -= yFirst;
            int th = yLast - yFirst;
            var tmp = new float[W * th];
            for (int y = 0; y < th; y++)
                for (int x = 0; x < W; x++)
                {
                    int xmin = bh[x * 2], xn = bh[x * 2 + 1];
                    double ss = 0.0;
                    int row = (y + yFirst) * iw;
                    for (int k = 0; k < xn; k++) ss += src[row + k + xmin] * ch[x * kh + k];
                    tmp[y * W + x] = (float)ss;
                }
            img = tmp;
            iw = W;
            ih = th;
        }
        if (H == h && W != w) return img;
        var outp = new float[W * H];
        for (int y = 0; y < H; y++)
        {
            int ymin = bv[y * 2], yn = bv[y * 2 + 1];
            for (int x = 0; x < iw; x++)
            {
                double ss = 0.0;
                for (int k = 0; k < yn; k++) ss += img[(k + ymin) * iw + x] * cv[y * kv + k];
                outp[y * W + x] = (float)ss;
            }
        }
        return outp;
    }

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

/// <summary>TextTransform with its regular expressions, as it was before the hand-written tag scanner (0.20.132).</summary>
static class OldTextTransform
{
    static readonly Regex Tag = new Regex("<([^<>]*)>");
    static readonly Regex TagAt = new Regex(@"\G<([^<>]*)>");

    static bool IsGlyph(char c) => !(c == '\n' || c == '\r' || c == ' ' || c == '︎' || c == '️' || char.IsLowSurrogate(c));

    static bool IsEscape(string text, int backslash) =>
        backslash + 1 < text.Length && "nrtuU".IndexOf(text[backslash + 1]) >= 0;
    static readonly HashSet<string> Lengths = new HashSet<string> { "size", "pos", "voffset", "indent", "line-height" };
    static readonly HashSet<string> Plain = new HashSet<string>
    {
        "scale", "rotate", "font", "/font", "alpha", "color", "/color", "b", "/b", "i", "/i", "u", "/u", "s", "/s",
        "sprite", "br", "/scale", "/rotate", "/alpha",
    };
    const double MaxTag = 32000;

    public static bool Supports(IList<string> texts, out string why)
    {
        foreach (string t in texts)
            if (!Check(t, out why)) return false;
        why = null;
        return true;
    }

    static bool Check(string text, out string why)
    {
        why = null;
        bool multi = false, sized = false;
        int i = 0;
        while (i < text.Length)
        {
            Match m = text[i] == '<' ? TagAt.Match(text, i) : null;
            if (m != null && m.Success)
            {
                string body = m.Groups[1].Value, name = Name(body);
                i = m.Index + m.Length;
                if (name == "line-height") multi = true;
                if (Lengths.Contains(name))
                {
                    string raw = body.Substring(body.IndexOf('=') + 1).Trim();
                    if (!TryValue(body, out _) || (name == "size" && (raw.StartsWith("+", StringComparison.Ordinal) || raw.StartsWith("-", StringComparison.Ordinal))))
                    {
                        why = $"<{body}> is not an absolute plain number";
                        return false;
                    }
                    if (name == "size") sized = true;
                }
                else if (!Plain.Contains(name) && !name.StartsWith("#", StringComparison.Ordinal))
                {
                    why = $"<{body}> is not something SignLink can resize";
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

    static string Name(string body)
    {
        int eq = body.IndexOf('=');
        string n = (eq < 0 ? body : body.Substring(0, eq)).Trim().ToLowerInvariant();
        int sp = n.IndexOf(' ');
        return sp < 0 ? n : n.Substring(0, sp);
    }

    static bool TryValue(string body, out double v)
    {
        int eq = body.IndexOf('=');
        return double.TryParse(eq < 0 ? "" : body.Substring(eq + 1).Trim(),
                               NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out v) &&
               !double.IsNaN(v) && !double.IsInfinity(v);
    }

    public static double Snap(IList<string> texts, double k) => Snap(Heights(texts), k);

    public static List<double> Heights(IList<string> texts)
    {
        var heights = new List<double>();
        foreach (string t in texts)
            foreach (Match m in Tag.Matches(t))
            {
                string body = m.Groups[1].Value;
                if (Name(body) == "line-height" && TryValue(body, out double l) && l > 0 && !heights.Contains(l)) heights.Add(l);
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

    public static string Apply(string text, double k, double dx, double dy)
    {
        if (text.Length == 0 || (k == 1 && dx == 0 && dy == 0)) return text;
        if (!Check(text, out string why)) throw new ArgumentException(why);
        bool multi = false;
        double v = 0, v0 = 0, vl = 0;
        bool seenGlyph = false, indentFirst = false, posFirst = false, voffFirst = false;
        int i = 0;
        while (i < text.Length)
        {
            Match m = text[i] == '<' ? TagAt.Match(text, i) : null;
            if (m != null && m.Success)
            {
                string body = m.Groups[1].Value, name = Name(body);
                if (name == "line-height") multi = true;
                if (name == "voffset" && TryValue(body, out double val))
                {
                    v = val;
                    if (!seenGlyph) voffFirst = true;
                }
                if (!seenGlyph && name == "indent") indentFirst = true;
                if (!seenGlyph && name == "pos") posFirst = true;
                i = m.Index + m.Length;
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
        foreach (Match m in Tag.Matches(text))
        {
            string body = m.Groups[1].Value, name = Name(body);
            if (!Lengths.Contains(name) || !TryValue(body, out double val)) continue;
            double nv = val * k;
            if (name == "pos" || name == "indent") nv += dx;
            if (name == "voffset") nv += delta;
            if (name == "line-height") nv = PyMath.Round(nv, 6);
            if (!(Math.Abs(nv) <= MaxTag))
                throw new ArgumentException("too large or too far from the sign: its tag numbers would pass what a sign reads");
            sb.Append(text, last, m.Index - last).Append('<').Append(name).Append('=').Append(Number(nv)).Append('>');
            last = m.Index + m.Length;
        }
        sb.Append(text, last, text.Length - last);
        return sb.ToString();
    }

    public static string Unfit(string text, Fit fit)
    {
        if (string.IsNullOrEmpty(text) || !Check(text, out _)) return text ?? "";
        double k = fit.Scale > 0 ? fit.Scale : 1;
        fit.Scale = k;
        fit.Offset(0, 0, out double dx, out double dy);
        try
        {
            string back = dx == 0 && dy == 0 ? text : Apply(text, 1, -dx, -dy);
            if (Math.Abs(k - 1) >= 1e-9) back = Apply(back, 1 / k, 0, 0);
            foreach (string zero in new[] { "<indent=0>", "<pos=0>", "<voffset=0>" })
                if (back.StartsWith(zero, StringComparison.Ordinal)) back = back.Substring(zero.Length);
            return back;
        }
        catch (ArgumentException)
        {
            return text;
        }
    }

    public static string[] Apply(IList<string> texts, double k, double dx, double dy)
    {
        var o = new string[texts.Count];
        for (int i = 0; i < texts.Count; i++) o[i] = Apply(texts[i], k, dx, dy);
        return o;
    }

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
