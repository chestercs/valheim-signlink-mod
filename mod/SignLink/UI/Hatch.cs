using System;
using System.Collections.Generic;

namespace SignLink.UI
{
    /// <summary>The content highlight's fill as diagonal stripes, its look from behind since 0.20.112-0.20.113 (0.20.76, the
    /// author: the full amber veil over the content was too bright to see the picture by; the striped look of before was
    /// better): stripes at 45° as the viewer sees
    /// them, `pitch` metres apart and `width` wide, clipped to the content's w x h rectangle, given in the frame quad's
    /// unit square (x / w, y / h: the quad is scaled to w x h). Convex polygons, counterclockwise, as x, y pairs. No
    /// Unity here (mod/SignLink.Tests).</summary>
    internal static class Hatch
    {
        /// <summary>No more stripes than this: a large picture gets wider ones (each is a few triangles).</summary>
        public const int MostStripes = 400;

        public static List<double[]> Stripes(double w, double h, double pitch, double width)
        {
            var bands = new List<double[]>();
            if (!(w > 0) || !(h > 0) || !(pitch > 0) || !(width > 0)) return bands;
            double reach = (w + h) / 2, step = pitch * Math.Sqrt(2);         // x + y runs over [-reach, reach]
            double grow = Math.Max(1, 2 * reach / step / MostStripes);
            step *= grow;
            double band = Math.Min(width * grow, pitch * grow) * Math.Sqrt(2);
            var rect = new List<double[]>
            {
                new[] { -w / 2, -h / 2 }, new[] { w / 2, -h / 2 }, new[] { w / 2, h / 2 }, new[] { -w / 2, h / 2 },
            };
            for (double d = Math.Floor(-reach / step) * step; d < reach; d += step)
            {
                List<double[]> p = Clip(Clip(rect, d, true), d + band, false);
                if (p.Count < 3) continue;
                var flat = new double[p.Count * 2];
                for (int i = 0; i < p.Count; i++)
                {
                    flat[2 * i] = p[i][0] / w;
                    flat[2 * i + 1] = p[i][1] / h;
                }
                if (Area(flat) > 1e-9) bands.Add(flat);
            }
            return bands;
        }

        /// <summary>The polygon's part with x + y at least d (above) or at most d (Sutherland-Hodgman, one edge).</summary>
        static List<double[]> Clip(List<double[]> poly, double d, bool above)
        {
            var o = new List<double[]>();
            for (int i = 0; i < poly.Count; i++)
            {
                double[] a = poly[i], b = poly[(i + 1) % poly.Count];
                double fa = a[0] + a[1] - d, fb = b[0] + b[1] - d;
                bool ina = above ? fa >= 0 : fa <= 0, inb = above ? fb >= 0 : fb <= 0;
                if (ina) o.Add(a);
                if (ina != inb)
                {
                    double t = fa / (fa - fb);
                    o.Add(new[] { a[0] + (b[0] - a[0]) * t, a[1] + (b[1] - a[1]) * t });
                }
            }
            return o;
        }

        static double Area(double[] p)
        {
            double a = 0;
            for (int i = 0; i < p.Length; i += 2)
                a += p[i] * p[(i + 3) % p.Length] - p[(i + 2) % p.Length] * p[i + 1];
            return a / 2;
        }
    }
}
