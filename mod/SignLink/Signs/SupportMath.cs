using System;
using System.Collections.Generic;

namespace SignLink.Signs
{
    /// <summary>The support a piece gets from the pieces around it, weighed as the game weighs it (WearNTear.UpdateSupport,
    /// client/WearNTear.cs:862-990): each supporting piece passes on its own support less a loss over the distance between
    /// the centres of mass (sideways, downwards, or between them by the angle), and two supports more than 100° apart
    /// around it hold it with their average. A piece under its material's least support wears and breaks (UpdateWear).
    /// Move the sign weighed only whether anything was there (0.20.106, the author: I kept pressing until the
    /// signpost broke at random). No Unity here (mod/SignLink.Tests).</summary>
    internal static class SupportMath
    {
        /// <summary>One piece found around it: its support, the distance as the game counts it (to its centre of mass, or
        /// to its position when that is nearer, plus 0.1 m), and its support point less our centre of mass.</summary>
        internal struct Supporter
        {
            public double Support, Distance;
            public double Px, Py, Pz;
        }

        /// <summary>The support these give, within the material's most (terrain or a thing that is not a piece gives the
        /// most at once: the caller's).</summary>
        public static double Support(IList<Supporter> found, double maxSupport, double horizontalLoss, double verticalLoss)
        {
            double best = 0;
            var under = new List<Supporter>();
            var values = new List<double>();
            foreach (Supporter s in found)
            {
                best = Math.Max(best, s.Support - horizontalLoss * s.Distance * s.Support);
                if (!(s.Py < 0.05)) continue;                               // over its centre of mass: sideways only
                double len = Math.Sqrt(s.Px * s.Px + s.Py * s.Py + s.Pz * s.Pz);
                double ny = len > 1e-12 ? s.Py / len : 0;
                if (ny < 0)
                {
                    double t = Math.Max(0, Math.Min(1, Math.Acos(1 - Math.Abs(ny)) / (Math.PI / 2)));
                    double loss = horizontalLoss + (verticalLoss - horizontalLoss) * t;
                    best = Math.Max(best, s.Support - loss * s.Distance * s.Support);
                }
                under.Add(s);
                values.Add(s.Support - verticalLoss * s.Distance * s.Support);
            }
            for (int l = 0; l < under.Count - 1; l++)
                for (int m = l + 1; m < under.Count; m++)
                {
                    double avg = (values[l] + values[m]) * 0.5;
                    if (avg <= best) continue;
                    if (Angle(under[l].Px, under[l].Pz, under[m].Px, under[m].Pz) >= 100) best = avg;
                }
            return Math.Min(best, maxSupport);
        }

        /// <summary>Is a step refused: what holds it after is under its least (5 % to spare, the game's support moves with
        /// what is around), and not more than before (a step towards better support stays allowed)?</summary>
        public static bool Refuse(double now, double after, double least) => after < least * 1.05 && after < now - 0.01;

        /// <summary>The angle between two flat directions in degrees (Unity's Vector3.Angle: 0 for a zero one).</summary>
        static double Angle(double ax, double az, double bx, double bz)
        {
            double la = Math.Sqrt(ax * ax + az * az), lb = Math.Sqrt(bx * bx + bz * bz);
            if (la * lb < 1e-15) return 0;
            double c = Math.Max(-1, Math.Min(1, (ax * bx + az * bz) / (la * lb)));
            return Math.Acos(c) * 180 / Math.PI;
        }
    }
}
