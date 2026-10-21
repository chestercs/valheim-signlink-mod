using System;

namespace PetyusPolisz.Kit
{
    /// <summary>A small painter for the generated textures of a theme (1.1.0, the Nordic look from the author's Claude
    /// Design handoff): rounded boxes, frames, clipped bars, a tick and a diamond, antialiased by 4 x 4 samples a pixel.
    /// Pixels from the top left, a pixel x covering [x, x + 1); colours in straight alpha, kept premultiplied inside.
    /// Theme turns a Paint into a Texture2D. No Unity here (mod/SignLink.Tests).</summary>
    internal sealed class Paint
    {
        public readonly int Width, Height;
        readonly float[] px;                                                // premultiplied RGBA, row by row from the top
        const int Samples = 4;

        /// <summary>Which corners of a box are round: top left, top right, bottom right, bottom left.</summary>
        public const int TopLeft = 1, TopRight = 2, BottomRight = 4, BottomLeft = 8, AllCorners = 15, TopCorners = 3;

        public Paint(int width, int height)
        {
            Width = Math.Max(1, width);
            Height = Math.Max(1, height);
            px = new float[Width * Height * 4];
        }

        /// <summary>A colour in straight alpha, 0..1.</summary>
        public struct Rgba
        {
            public float R, G, B, A;

            public Rgba(float r, float g, float b, float a = 1f)
            {
                R = r;
                G = g;
                B = b;
                A = a;
            }

            /// <summary>"#RRGGBB" or "#RRGGBBAA".</summary>
            public static Rgba Hex(string hex, float a = 1f)
            {
                string h = hex.TrimStart('#');
                float At(int i) => Convert.ToInt32(h.Substring(i, 2), 16) / 255f;
                return new Rgba(At(0), At(2), At(4), h.Length >= 8 ? At(6) : a);
            }

            public Rgba WithAlpha(float a) => new Rgba(R, G, B, a);
        }

        /// <summary>A pixel as it was painted, in straight alpha.</summary>
        public Rgba Get(int x, int y)
        {
            int i = (y * Width + x) * 4;
            float a = px[i + 3];
            return a <= 0f ? new Rgba(0f, 0f, 0f, 0f) : new Rgba(px[i] / a, px[i + 1] / a, px[i + 2] / a, a);
        }

        /// <summary>Is the point inside the box from (x0, y0) to (x1, y1) with these corners rounded by r?</summary>
        public static bool InBox(double x, double y, double x0, double y0, double x1, double y1, double r, int corners = AllCorners)
        {
            if (x < x0 || x > x1 || y < y0 || y > y1) return false;
            r = Math.Max(0, Math.Min(r, Math.Min(x1 - x0, y1 - y0) / 2));
            if (r <= 0) return true;
            bool left = x < x0 + r, right = x > x1 - r, top = y < y0 + r, bottom = y > y1 - r;
            int corner = top && left ? TopLeft : top && right ? TopRight : bottom && right ? BottomRight : bottom && left ? BottomLeft : 0;
            if (corner == 0 || (corners & corner) == 0) return true;
            double cx = left ? x0 + r : x1 - r, cy = top ? y0 + r : y1 - r;
            return (x - cx) * (x - cx) + (y - cy) * (y - cy) <= r * r;
        }

        /// <summary>A rounded box filled with c, clipped to the clip box (pixels; null: none). replace: the box takes the place
        /// of what was under it (a see-through background inside a frame) instead of lying over it.</summary>
        public void Box(double x0, double y0, double x1, double y1, double r, Rgba c, int corners = AllCorners, bool replace = false,
                        double[] clip = null)
        {
            Cover(x0, y0, x1, y1, (x, y) => InBox(x, y, x0, y0, x1, y1, r, corners), c, replace, clip);
        }

        /// <summary>A frame `width` wide inside the rounded box's edge (its inner edge rounded by r - width).</summary>
        public void Frame(double x0, double y0, double x1, double y1, double r, double width, Rgba c)
        {
            double ri = Math.Max(0, r - width);
            Cover(x0, y0, x1, y1, (x, y) => InBox(x, y, x0, y0, x1, y1, r) &&
                                             !InBox(x, y, x0 + width, y0 + width, x1 - width, y1 - width, ri), c, false, null);
        }

        /// <summary>A line through the points, `width` wide with round ends and joints (a tick).</summary>
        public void Line(double[] xy, double width, Rgba c)
        {
            double half = width / 2, x0 = double.MaxValue, y0 = double.MaxValue, x1 = double.MinValue, y1 = double.MinValue;
            for (int i = 0; i < xy.Length; i += 2)
            {
                x0 = Math.Min(x0, xy[i] - half);
                x1 = Math.Max(x1, xy[i] + half);
                y0 = Math.Min(y0, xy[i + 1] - half);
                y1 = Math.Max(y1, xy[i + 1] + half);
            }
            Cover(x0, y0, x1, y1, (x, y) =>
            {
                for (int i = 0; i + 3 < xy.Length; i += 2)
                    if (SegmentDistance(x, y, xy[i], xy[i + 1], xy[i + 2], xy[i + 3]) <= half) return true;
                return false;
            }, c, false, null);
        }

        /// <summary>A square standing on a corner: |x - cx| + |y - cy| at most half.</summary>
        public void Diamond(double cx, double cy, double half, Rgba c) =>
            Cover(cx - half, cy - half, cx + half, cy + half, (x, y) => Math.Abs(x - cx) + Math.Abs(y - cy) <= half, c, false, null);

        /// <summary>A triangle through three points (a group heading's arrow, 1.1.0).</summary>
        public void Triangle(double ax, double ay, double bx, double by, double cx, double cy, Rgba c) =>
            Cover(Math.Min(ax, Math.Min(bx, cx)), Math.Min(ay, Math.Min(by, cy)), Math.Max(ax, Math.Max(bx, cx)), Math.Max(ay, Math.Max(by, cy)),
                  (x, y) =>
                  {
                      double d1 = Side(x, y, ax, ay, bx, by), d2 = Side(x, y, bx, by, cx, cy), d3 = Side(x, y, cx, cy, ax, ay);
                      return !((d1 < 0 || d2 < 0 || d3 < 0) && (d1 > 0 || d2 > 0 || d3 > 0));   // all on one side
                  }, c, false, null);

        static double Side(double x, double y, double ax, double ay, double bx, double by) => (x - bx) * (ay - by) - (ax - bx) * (y - by);

        static double SegmentDistance(double x, double y, double ax, double ay, double bx, double by)
        {
            double dx = bx - ax, dy = by - ay, len = dx * dx + dy * dy;
            double t = len <= 0 ? 0 : Math.Max(0, Math.Min(1, ((x - ax) * dx + (y - ay) * dy) / len));
            double ex = ax + t * dx - x, ey = ay + t * dy - y;
            return Math.Sqrt(ex * ex + ey * ey);
        }

        /// <summary>Paints c over the pixels of the bounds, each by the share of its samples the shape holds.</summary>
        void Cover(double bx0, double by0, double bx1, double by1, Func<double, double, bool> inside, Rgba c, bool replace, double[] clip)
        {
            if (clip != null)
            {
                bx0 = Math.Max(bx0, clip[0]);
                by0 = Math.Max(by0, clip[1]);
                bx1 = Math.Min(bx1, clip[2]);
                by1 = Math.Min(by1, clip[3]);
            }
            int xa = Math.Max(0, (int)Math.Floor(bx0)), xb = Math.Min(Width, (int)Math.Ceiling(bx1));
            int ya = Math.Max(0, (int)Math.Floor(by0)), yb = Math.Min(Height, (int)Math.Ceiling(by1));
            for (int y = ya; y < yb; y++)
                for (int x = xa; x < xb; x++)
                {
                    int hit = 0;
                    for (int sy = 0; sy < Samples; sy++)
                        for (int sx = 0; sx < Samples; sx++)
                        {
                            double fx = x + (sx + 0.5) / Samples, fy = y + (sy + 0.5) / Samples;
                            if (clip != null && (fx < clip[0] || fx > clip[2] || fy < clip[1] || fy > clip[3])) continue;
                            if (inside(fx, fy)) hit++;
                        }
                    if (hit > 0) Put(x, y, c, hit / (float)(Samples * Samples), replace);
                }
        }

        void Put(int x, int y, Rgba c, float cover, bool replace)
        {
            int i = (y * Width + x) * 4;
            if (replace)                                                    // towards c by the cover
            {
                px[i] += (c.R * c.A - px[i]) * cover;
                px[i + 1] += (c.G * c.A - px[i + 1]) * cover;
                px[i + 2] += (c.B * c.A - px[i + 2]) * cover;
                px[i + 3] += (c.A - px[i + 3]) * cover;
                return;
            }
            float a = c.A * cover, keep = 1f - a;                           // over
            px[i] = c.R * a + px[i] * keep;
            px[i + 1] = c.G * a + px[i + 1] * keep;
            px[i + 2] = c.B * a + px[i + 2] * keep;
            px[i + 3] = a + px[i + 3] * keep;
        }
    }
}
