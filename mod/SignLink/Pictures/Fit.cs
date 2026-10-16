using System;

namespace SignLink.Pictures
{
    /// <summary>How a piece sits on its sign, compared with how it was made: its size, where its centre goes (right, up,
    /// towards you, as the sign was built), and the sign's pose (tipped over its bottom edge, turned clockwise). The texts as made have their centre at c0 (wu, the sign's own
    /// frame; 0, 0 when it is unknown: then the piece turns around the sign's centre). A sign draws in its own plane and
    /// can only turn as a whole, so a piece turns around its own centre by turning the sign and moving the texts
    /// (TextTransform): this is the arithmetic of that, apart from Unity (tested in mod/SignLink.Tests).</summary>
    internal struct Fit
    {
        /// <summary>Times the size as made.</summary>
        public double Scale;
        /// <summary>wu (1 m = 40) from where the centre sits as made, in the frame of the sign as it was built, whatever its
        /// tilt: to the right of someone who faced it, up, and towards them (Out). On the tipped sign they become its own
        /// right and up (OnSign); the sign itself never moves, so the part off its face does not show (standing, Out;
        /// lying, Up).</summary>
        public double Right, Up, Out;
        /// <summary>Degrees, clockwise as seen from the front (face up: from above).</summary>
        public double Turn;
        /// <summary>Degrees the sign tips (SignPose): 0 standing as built, 90 face up (a picture on the floor), -90 face
        /// down (read from below).</summary>
        public double Tilt;
        /// <summary>How it tips: over its bottom edge, which stays put (a sign on the floor lies on it face up; the way
        /// every pose was made before 0.20.30), or, false, about its middle, which stays put (the default since).
        /// Matters only while tilted.</summary>
        public bool Edge;

        public static Fit AsMade => new Fit { Scale = 1 };

        /// <summary>The bounds of a fit read from a sign's data, which any player's game can write (0.20.132, review): a
        /// size from a thousandth to a thousand times as made, offsets within 1 km (40000 wu), past all the rows (25-400 %,
        /// 300 m typed) and the API (300 m) make.</summary>
        public const double MinScale = 1e-3, MaxScale = 1e3, MaxOffset = 40000;

        /// <summary>A fit's numbers as kept on a sign (scale, right, up, towards you; wu): sane (finite, within the bounds)
        /// or, else, no fit: as made. NaN fails every comparison.</summary>
        public static bool Sane(double scale, double right, double up, double outWu) =>
            scale >= MinScale && scale <= MaxScale && Math.Abs(right) <= MaxOffset && Math.Abs(up) <= MaxOffset && Math.Abs(outWu) <= MaxOffset;

        /// <summary>The content's middle as made (wu), as kept on a sign: finite and within MaxOffset, else unknown.</summary>
        public static bool SanePoint(double x, double y) => Math.Abs(x) <= MaxOffset && Math.Abs(y) <= MaxOffset;

        /// <summary>As made, in this fit's pose (Turn, Tilt, Edge): what content SignLink cannot resize or move goes on with.</summary>
        public Fit PoseOnly() => new Fit { Scale = 1, Turn = Turn, Tilt = Tilt, Edge = Edge };

        /// <summary>This fit's size and place in the other's pose (Tilt, Turn, Edge): texts worked out for one pose shown in
        /// the pose the rows say now.</summary>
        public Fit WithPose(Fit other)
        {
            Fit f = this;
            f.Tilt = other.Tilt;
            f.Turn = other.Turn;
            f.Edge = other.Edge;
            return f;
        }

        /// <summary>Same size, place and pose (within what anyone could see).</summary>
        public bool Same(Fit o) => Math.Abs(Scale - o.Scale) < 1e-4 && Math.Abs(Right - o.Right) < 0.01 &&
                                   Math.Abs(Up - o.Up) < 0.01 && Math.Abs(Out - o.Out) < 0.01 && Math.Abs(Angle(Turn - o.Turn)) < 0.05 && Math.Abs(Tilt - o.Tilt) < 0.05 &&
                                   SamePivot(o);

        /// <summary>The same pivot, or no tilt on either (then it cannot show).</summary>
        public bool SamePivot(Fit o) => Edge == o.Edge || (Math.Abs(Tilt) < 0.05 && Math.Abs(o.Tilt) < 0.05);

        /// <summary>An angle in (-180, 180].</summary>
        public static double Angle(double deg)
        {
            deg %= 360;
            if (deg > 180) deg -= 360;
            if (deg <= -180) deg += 360;
            return deg;
        }

        /// <summary>A point in the frame of a sign turned deg clockwise, from the frame as built: the sign's turn undone
        /// (x right, y up, as seen from the front).</summary>
        public static void Unturn(double x, double y, double deg, out double ox, out double oy)
        {
            if (deg == 0)
            {
                ox = x;
                oy = y;
                return;
            }
            double a = deg * Math.PI / 180, c = Math.Cos(a), s = Math.Sin(a);
            ox = x * c - y * s;
            oy = x * s + y * c;
        }

        /// <summary>Where a point of the turned sign's frame shows in the frame as built: the sign's clockwise turn.</summary>
        public static void TurnPoint(double x, double y, double deg, out double ox, out double oy) => Unturn(x, y, -deg, out ox, out oy);

        /// <summary>Right, Up and Out on the tipped sign (wu): its own right, its own up (along its face: up when it
        /// stands, away from you face up, towards you face down), and the part along its normal (where its text faces),
        /// which a picture cannot take: the nearest point on the sign's face to the place asked for.</summary>
        public void OnSign(out double right, out double up, out double normal)
        {
            double a = Tilt * Math.PI / 180, c = Math.Cos(a), s = Math.Sin(a);
            right = Right;
            up = Up * c - Out * s;
            normal = Up * s + Out * c;
        }

        /// <summary>How far to move the texts as made, after scaling them by Scale, in the turned sign's own frame
        /// (TextTransform.Apply(texts, Scale, dx, dy)): their centre goes where Right and Up say on the sign (OnSign), and
        /// turning the sign turns them around it.</summary>
        public void Offset(double c0x, double c0y, out double dx, out double dy)
        {
            OnSign(out double right, out double up, out _);
            double px = Scale * c0x + right, py = Scale * c0y + up;      // where the centre shows, in the frame as built
            Unturn(px, py, Turn, out double qx, out double qy);           // ... in the turned sign's own frame
            dx = qx - Scale * c0x;
            dy = qy - Scale * c0y;
        }

        /// <summary>The same for a piece put centred (0.20.38): its middle c0 as made counts as the sign's centre, so
        /// Right and Up are from the middle of the sign and 0, 0 puts it there, as a picture (made centred) is. The texts
        /// as made, scaled by Scale and moved by (dx, dy), show exactly what a copy made at the middle would with
        /// Offset(0, 0); the sign then keeps 0, 0 as their centre as made (SignFit.Put).</summary>
        public void OffsetCentred(double c0x, double c0y, out double dx, out double dy)
        {
            Offset(0, 0, out double qx, out double qy);
            dx = qx - Scale * c0x;
            dy = qy - Scale * c0y;
        }

        /// <summary>Where the content's middle shows on the turned sign, in its own frame (wu): c0 as made, scaled and
        /// moved by Offset. What SignFit.Read reads back and where the ghost frame draws.</summary>
        public void Centre(double c0x, double c0y, out double x, out double y)
        {
            Offset(c0x, c0y, out double dx, out double dy);
            x = Scale * c0x + dx;
            y = Scale * c0y + dy;
        }

        /// <summary>Centre the other way: where the middle of content that shows at (x, y) with this fit sat as made (a
        /// text that already carries its fit, the sign's own in the editor, 0.20.40).</summary>
        public void MadeCentre(double x, double y, out double c0x, out double c0y)
        {
            TurnPoint(x, y, Turn, out double px, out double py);           // back in the frame as built
            OnSign(out double right, out double up, out _);
            c0x = (px - right) / Scale;
            c0y = (py - up) / Scale;
        }

        /// <summary>This fit for a sign in another pose (turn and tilt in degrees, its pivot): the content made with its
        /// middle at c0 shows where it did on the sign's face (Centre), its size kept, and so is how far it stands out
        /// of the face (OnSign's normal); its place in the frame as built follows. What Paste writes when the target sign
        /// stands otherwise than the copied one (0.20.49).</summary>
        public static Fit Repose(Fit f, double turn, double tilt, bool edge, double c0x, double c0y)
        {
            f.Centre(c0x, c0y, out double x, out double y);
            f.OnSign(out _, out _, out double normal);
            var g = new Fit { Scale = f.Scale, Turn = turn, Tilt = tilt, Edge = edge };
            TurnPoint(x, y, turn, out double px, out double py);            // the middle in the frame as built
            double right = px - f.Scale * c0x, up = py - f.Scale * c0y;      // ... where Right and the face's up put it
            double a = tilt * Math.PI / 180, c = Math.Cos(a), s = Math.Sin(a);
            g.Right = right;
            g.Up = up * c + normal * s;                                     // OnSign the other way: up along the face and the
            g.Out = -up * s + normal * c;                                   // normal back to Up and Out
            return g;
        }

        /// <summary>This fit in another pose (turn and tilt in degrees, its pivot) as the 3 Place rows make it: Right, Up
        /// along the face and the part off it stay, so the content stays where the rows show it on the face (FitPanel does
        /// the same when the Tilt row moves). Reset to default (3 Place › Changes) stands a tilted sign up with it (0.20.66).</summary>
        public Fit Posed(double turn, double tilt, bool edge)
        {
            OnSign(out double right, out double up, out double normal);
            double a = tilt * Math.PI / 180, c = Math.Cos(a), s = Math.Sin(a);
            return new Fit { Scale = Scale, Right = right, Up = up * c + normal * s, Out = -up * s + normal * c, Turn = turn, Tilt = tilt, Edge = edge };
        }

        /// <summary>This fit laid in another tilt by a pose button (3 Place › Sign rotations, 0.20.107): a button that turns
        /// the face round (more than a quarter: face up and face down) keeps the content where it is in the world, Right,
        /// Up and Out as built (the author: pressing Face down on this setup makes the picture jump over and land just
        /// as far from me on the other side; the Up row changes sign, as Up runs towards you face down); a quarter turn
        /// or less keeps it where it sits on the face (Posed: what stood above the middle lies beyond it).</summary>
        public Fit Laid(double tilt, bool edge)
        {
            if (Math.Cos((tilt - Tilt) * Math.PI / 180) >= -1e-9) return Posed(Turn, tilt, edge);
            Fit f = this;
            f.Tilt = tilt;
            f.Edge = edge;
            f.OnSign(out _, out double up, out _);                          // the part off the new face cannot show: it goes,
            double a = tilt * Math.PI / 180;                                // so it cannot come back on a later tilt (0.20.159,
            f.Up = up * Math.Cos(a);                                        // review: a path of tilts left 3.8 m of it hidden)
            f.Out = -up * Math.Sin(a);
            return f;
        }

        /// <summary>"lying face up", "tilted back 30°", "lying face down", "tilted forward 15°", or "" (standing).</summary>
        public static string TiltWords(double tilt) =>
            Math.Abs(tilt) < 0.05 ? "" : tilt >= 89.95 ? "lying face up" : tilt <= -89.95 ? "lying face down"
            : tilt > 0 ? $"tilted back {tilt:0.#}°" : $"tilted forward {-tilt:0.#}°";

        /// <summary>", at 150 %, 1 m right, lying face up, turned 30°, glowing": what differs from as made, and the light
        /// (empty: nothing). For status lines.</summary>
        public string Describe(GlowMode light = GlowMode.AsMade)
        {
            var parts = new System.Collections.Generic.List<string>();
            if (Math.Abs(Scale - 1) > 1e-4) parts.Add($"at {Scale * 100:0.#} %");
            if (Math.Abs(Right) > 0.01) parts.Add(Right > 0 ? $"{Right / 40:0.##} m right" : $"{-Right / 40:0.##} m left");
            if (Math.Abs(Up) > 0.01) parts.Add(Up > 0 ? $"{Up / 40:0.##} m up" : $"{-Up / 40:0.##} m down");
            if (Math.Abs(Out) > 0.01) parts.Add(Out > 0 ? $"{Out / 40:0.##} m towards you" : $"{-Out / 40:0.##} m away");
            string tilt = TiltWords(Tilt);
            if (tilt.Length > 0) parts.Add(Edge ? tilt + " over its edge" : tilt);
            if (Math.Abs(Angle(Turn)) > 0.05) parts.Add($"rotated {Angle(Turn):0.#}°");     // the Rotate row (0.20.149: "turned" is Turn's)
            if (light == GlowMode.Glowing) parts.Add("glowing");
            if (light == GlowMode.Lit) parts.Add("lit by the scene");
            return parts.Count == 0 ? "" : ", " + string.Join(", ", parts.ToArray());
        }

        /// <summary>From texts that `from` put on a sign to the texts `to` puts there: scale by k, then move by dx, dy
        /// (TextTransform.Apply on what the sign holds).</summary>
        public static void Between(Fit from, Fit to, double c0x, double c0y, out double k, out double dx, out double dy)
        {
            k = to.Scale / from.Scale;
            from.Offset(c0x, c0y, out double fx, out double fy);
            to.Offset(c0x, c0y, out double tx, out double ty);
            dx = tx - k * fx;
            dy = ty - k * fy;
        }
    }
}
