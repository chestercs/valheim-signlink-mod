using System;
using System.Numerics;

/// <summary>The reference: PyMath as it was before the 128-bit fast path (BigInteger on the exact binary value), kept
/// in the test project only. Program.cs compares PyMath with it on millions of values: the fast path must give the
/// same doubles and strings.</summary>
static class PyMathSlow
{
    static readonly double[] Pow10 = { 1, 10, 100, 1e3, 1e4, 1e5, 1e6, 1e7, 1e8, 1e9 };

    static BigInteger Scaled(double x, int nd)
    {
        long bits = BitConverter.DoubleToInt64Bits(x);
        int exp = (int)((bits >> 52) & 0x7FF);
        long frac = bits & 0xFFFFFFFFFFFFFL;
        if (exp == 0) exp = 1; else frac |= 1L << 52;
        exp -= 1075;
        BigInteger num = new BigInteger(frac) * BigInteger.Pow(10, nd);
        if (exp >= 0) return num << exp;
        BigInteger den = BigInteger.One << -exp;
        BigInteger q = BigInteger.DivRem(num, den, out BigInteger r);
        int half = (r << 1).CompareTo(den);
        if (half > 0 || (half == 0 && !q.IsEven)) q += 1;
        return q;
    }

    static bool Negative(double x) => BitConverter.DoubleToInt64Bits(x) < 0;

    public static double Round(double x, int nd)
    {
        if (double.IsNaN(x) || double.IsInfinity(x)) return x;
        double v = (double)Scaled(x, nd) / Pow10[nd];
        return Negative(x) ? -v : v;
    }

    public static long RoundInt(double x)
    {
        long v = (long)Scaled(x, 0);
        return Negative(x) ? -v : v;
    }

    public static string Fixed(double x, int nd)
    {
        string digits = Scaled(x, nd).ToString().PadLeft(nd + 1, '0');
        string s = nd == 0 ? digits : digits.Substring(0, digits.Length - nd) + "." + digits.Substring(digits.Length - nd);
        return Negative(x) ? "-" + s : s;
    }

    public static string Num(double v, int nd)
    {
        if (nd == 0) return RoundInt(v).ToString(System.Globalization.CultureInfo.InvariantCulture);
        string s = Fixed(v, nd).TrimEnd('0').TrimEnd('.');
        if (s == "-0" || s == "") return "0";
        if (s.StartsWith("0.", StringComparison.Ordinal) || s.StartsWith("-0.", StringComparison.Ordinal)) s = s.Remove(s.IndexOf("0.", StringComparison.Ordinal), 1);
        return s;
    }
}
