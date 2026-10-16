using System.Globalization;
using System.Text;
using UnityEngine;

namespace SignLink.Api
{
    /// <summary>The few JSON writers the API needs (no library: nothing to bundle).</summary>
    internal static class Json
    {
        public static string F(double v) =>
            double.IsNaN(v) || double.IsInfinity(v) ? "null" : v.ToString("0.###", CultureInfo.InvariantCulture);

        public static string Vec(Vector3 v) => "[" + F(v.x) + "," + F(v.y) + "," + F(v.z) + "]";

        public static string Str(string s)
        {
            if (s == null) return "null";
            var sb = new StringBuilder(s.Length + 2).Append('"');
            foreach (char c in s)
            {
                switch (c)
                {
                    case '"': sb.Append("\\\""); break;
                    case '\\': sb.Append("\\\\"); break;
                    case '\n': sb.Append("\\n"); break;
                    case '\r': sb.Append("\\r"); break;
                    case '\t': sb.Append("\\t"); break;
                    default:
                        if (c < 0x20) sb.Append("\\u").Append(((int)c).ToString("x4"));
                        else sb.Append(c);
                        break;
                }
            }
            return sb.Append('"').ToString();
        }
    }
}
