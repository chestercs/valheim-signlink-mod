using System;
using System.Globalization;
using System.Numerics;

namespace SignLink.Pictures
{
    /// <summary>Python's float rounding and formatting, exactly: the sign texts must come out byte for byte as the
    /// Python encoder writes them (signart/engine.py, tested in mod/SignLink.Tests). Python rounds the exact binary
    /// value half to even (round(x, n), f"{x:.nf}"); .NET's Math.Round and ToString("F") do not promise that. The
    /// exact value is scaled in 128-bit integer arithmetic for the sizes a sign text uses (no allocation: the
    /// encoder calls this a few times per pixel), BigInteger beyond. No Unity here: the test project compiles this
    /// file as it is, and compares it with its BigInteger copy on millions of values.</summary>
    internal static class PyMath
    {
        static readonly double[] Pow10 = { 1, 10, 100, 1e3, 1e4, 1e5, 1e6, 1e7, 1e8, 1e9 };
        static readonly ulong[] Pow10U = { 1, 10, 100, 1000, 10000, 100000, 1000000, 10000000, 100000000, 1000000000 };

        /// <summary>x * 10^nd rounded half to even, as an integer (sign dropped), from the exact value of x: the slow,
        /// exact way for any value.</summary>
        static BigInteger Scaled(double x, int nd)
        {
            long bits = BitConverter.DoubleToInt64Bits(x);
            int exp = (int)((bits >> 52) & 0x7FF);
            long frac = bits & 0xFFFFFFFFFFFFFL;
            if (exp == 0) exp = 1; else frac |= 1L << 52;
            exp -= 1075;                                            // |x| = frac * 2^exp
            BigInteger num = new BigInteger(frac) * BigInteger.Pow(10, nd);
            if (exp >= 0) return num << exp;
            BigInteger den = BigInteger.One << -exp;
            BigInteger q = BigInteger.DivRem(num, den, out BigInteger r);
            int half = (r << 1).CompareTo(den);
            if (half > 0 || (half == 0 && !q.IsEven)) q += 1;
            return q;
        }

        /// <summary>The same for |x| under 2^31 and nd up to 9, when the result is under 2^53 (exact as a double), with
        /// the mantissa times 10^nd as a 128-bit product shifted by the exponent and rounded half to even on the bits
        /// shifted out; false when the value is outside that (Scaled then).</summary>
        static bool ScaledFast(double x, int nd, out ulong q)
        {
            q = 0;
            if (nd < 0 || nd > 9) return false;
            long bits = BitConverter.DoubleToInt64Bits(x);
            int exp = (int)((bits >> 52) & 0x7FF);
            if (exp == 0x7FF) return false;                         // NaN or infinity
            ulong frac = (ulong)(bits & 0xFFFFFFFFFFFFFL);
            if (exp == 0) exp = 1; else frac |= 1UL << 52;
            int e = exp - 1075;                                     // |x| = frac * 2^e
            if (e > -22) return false;                              // |x| may reach 2^31
            if (frac == 0) return true;
            int s = -e;                                             // 22 .. 1074
            if (s >= 84) return true;                               // num < 2^83 <= 2^(s-1): under half a unit, so 0
            Mul64(frac, Pow10U[nd], out ulong hi, out ulong lo);   // num = frac * 10^nd, at most 83 bits
            ulong qv = s < 64 ? (lo >> s) | (hi << (64 - s)) : hi >> (s - 64);
            bool roundBit = Bit(hi, lo, s - 1), sticky = LowBits(hi, lo, s - 1);
            if (roundBit && (sticky || (qv & 1) == 1)) qv++;
            if (qv >= 1UL << 53) return false;                      // the double conversion would round: BigInteger says how
            q = qv;
            return true;
        }

        static void Mul64(ulong a, ulong b, out ulong hi, out ulong lo)
        {
            ulong a0 = a & 0xFFFFFFFF, a1 = a >> 32, b0 = b & 0xFFFFFFFF, b1 = b >> 32;
            ulong p00 = a0 * b0, p01 = a0 * b1, p10 = a1 * b0, p11 = a1 * b1;
            ulong mid = (p00 >> 32) + (p01 & 0xFFFFFFFF) + (p10 & 0xFFFFFFFF);
            lo = (p00 & 0xFFFFFFFF) | (mid << 32);
            hi = p11 + (p01 >> 32) + (p10 >> 32) + (mid >> 32);
        }

        static bool Bit(ulong hi, ulong lo, int i) => i < 64 ? ((lo >> i) & 1) == 1 : ((hi >> (i - 64)) & 1) == 1;

        /// <summary>Any of the bits below i set?</summary>
        static bool LowBits(ulong hi, ulong lo, int i)
        {
            if (i <= 0) return false;
            if (i >= 64) return lo != 0 || (hi & ((1UL << (i - 64)) - 1)) != 0;
            return (lo & ((1UL << i) - 1)) != 0;
        }

        static bool Negative(double x) => BitConverter.DoubleToInt64Bits(x) < 0;

        /// <summary>round(x, nd)</summary>
        public static double Round(double x, int nd)
        {
            if (double.IsNaN(x) || double.IsInfinity(x)) return x;
            double v = ScaledFast(x, nd, out ulong q) ? q / Pow10[nd] : (double)Scaled(x, nd) / Pow10[nd];
            return Negative(x) ? -v : v;
        }

        /// <summary>round(x): the nearest integer, ties to even.</summary>
        public static long RoundInt(double x)
        {
            long v = ScaledFast(x, 0, out ulong q) ? (long)q : (long)Scaled(x, 0);
            return Negative(x) ? -v : v;
        }

        /// <summary>f"{x:.{nd}f}"</summary>
        public static string Fixed(double x, int nd)
        {
            string digits = (ScaledFast(x, nd, out ulong q) ? q.ToString(CultureInfo.InvariantCulture)
                                                            : Scaled(x, nd).ToString(CultureInfo.InvariantCulture)).PadLeft(nd + 1, '0');
            string s = nd == 0 ? digits : digits.Substring(0, digits.Length - nd) + "." + digits.Substring(digits.Length - nd);
            return Negative(x) ? "-" + s : s;
        }

        /// <summary>engine._num: a tag number with at most nd decimals, no trailing zeros, "0." written ".".</summary>
        public static string Num(double v, int nd)
        {
            if (nd == 0) return RoundInt(v).ToString(CultureInfo.InvariantCulture);
            string s = Fixed(v, nd).TrimEnd('0').TrimEnd('.');
            if (s == "-0" || s == "") return "0";
            if (s.StartsWith("0.", StringComparison.Ordinal) || s.StartsWith("-0.", StringComparison.Ordinal)) s = s.Remove(s.IndexOf("0.", StringComparison.Ordinal), 1);
            return s;
        }

        /// <summary>A tag number as TMP reads it (TMP_Text.ConvertToFloat): float32, the n-th decimal as digit x 0.1f^n.</summary>
        public static float TmpFloat(string s)
        {
            float num = 0f, mul = 0f;
            bool frac = false;
            foreach (char ch in s.TrimStart('+', '-'))
            {
                if (ch == '.') { frac = true; mul = 0.1f; continue; }
                float d = ch - '0';
                if (frac)
                {
                    num = (float)(num + (float)(d * mul));
                    mul = (float)(mul * 0.1f);
                }
                else num = (float)((float)(num * 10f) + d);
            }
            return s.StartsWith("-", StringComparison.Ordinal) ? -num : num;
        }
    }
}
