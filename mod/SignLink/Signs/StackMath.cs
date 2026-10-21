using System;
using System.Globalization;

namespace SignLink.Signs
{
    /// <summary>The arithmetic of a stack apart from Unity (mod/SignLink.Tests checks it).</summary>
    internal static class StackMath
    {
        /// <summary>The indices of the run of neighbours (sorted positions along the normal) that holds `me`, with no
        /// gap over maxGap between consecutive ones: copies stand 0.3-0.5 mm behind each other, a sign placed by hand
        /// at least 4.45 cm from any other, so the run is one stack and a hand-placed sign never joins it.</summary>
        public static int[] Chain(float[] along, int me, float maxGap)
        {
            int first = me, last = me;
            while (first > 0 && along[first] - along[first - 1] <= maxGap) first--;
            while (last + 1 < along.Length && along[last + 1] - along[last] <= maxGap) last++;
            var o = new int[last - first + 1];
            for (int i = 0; i < o.Length; i++) o[i] = first + i;
            return o;
        }

        /// <summary>A pose kept on a stack's first sign (any player's game can write it): finite, the tilt -90..90°, the
        /// turn within a turn, the shift along the normal within maxShift metres; else none is kept (0.20.132, review).</summary>
        public static bool SanePose(float tilt, float turn, float shift, float maxShift) =>
            Math.Abs(tilt) <= 90.01f && Math.Abs(turn) <= 360f && Math.Abs(shift) <= maxShift;

        /// <summary>A base (the pose as built) kept on a stack's first sign: within maxM metres of the sign (dx, dy, dz:
        /// the base less where the sign stands) and a unit rotation; else none is kept, or a forged one would carry the
        /// stack off with its next pose (0.20.132, review). NaN fails every comparison, so it is none too.</summary>
        public static bool SaneBase(float dx, float dy, float dz, float qx, float qy, float qz, float qw, float maxM)
        {
            double d2 = (double)dx * dx + (double)dy * dy + (double)dz * dz;
            double q2 = (double)qx * qx + (double)qy * qy + (double)qz * qz + (double)qw * qw;
            return d2 <= (double)maxM * maxM && Math.Abs(q2 - 1) <= 0.01;
        }
    }
}
