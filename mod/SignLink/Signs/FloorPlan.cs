using System;

namespace SignLink.Signs
{
    /// <summary>The numbers of a sign tipped over its bottom edge and lying on the floor (SignPose), apart from Unity so
    /// the tests can check them.
    /// The sign prefab's text is TextZ in front of the board's centre and the board is 2 x BoardHalf thick (its collider
    /// 2 x ColliderHalf), so a sign laid face up with its text Lift over the floor has its board and collider under the
    /// floor's surface, and so do the copies stacked on it, Step apart, for as many signs as a picture needs (about 21 at
    /// the 1 M pixel cap).</summary>
    internal static class FloorPlan
    {
        /// <summary>The root sign's text over the floor's collider: a wood floor's planks lie 1.6 cm under its collider,
        /// a stone floor's top 1.3 mm over it (prefabs wood_floor, stone_floor_2x2).</summary>
        public const double Lift = 0.008;
        /// <summary>Copies apart along the normal (up): closer than a standing stack's 0.5 mm, so their boards stay down.</summary>
        public const double Step = 0.0003;
        public const double TextZ = 0.07, BoardHalf = 0.05, ColliderHalf = 0.0445;
        /// <summary>Where a sign's centre moves when it tips over its bottom edge by tiltDeg (+ face up, - face down), in
        /// its frame as built (forward: where it faced, up): the edge is the line `foot` under its centre (where it rests
        /// on a floor) and Lift behind its text (textZ in front of the centre), and stays where it is. Face up, the text
        /// lies Lift over that floor, the board under it; face down, Lift under that line, the board over it.</summary>
        public static void Tip(double tiltDeg, double foot, double textZ, out double forward, out double up)
        {
            double a = tiltDeg * Math.PI / 180, c = Math.Cos(a), s = Math.Sin(a), z0 = textZ - Lift;
            forward = z0 - z0 * c - foot * s;                       // the edge (z0, -foot) less the edge tipped
            up = -foot - z0 * s + foot * c;
        }

        /// <summary>Where a sign's centre moves when it tips by tiltDeg about the middle of its text's plane (textZ in front
        /// of its centre), in its frame as built: that middle stays where it is, so the picture does too, whatever the
        /// tilt; the board turns around it (0.20.83, the author: face up and face down put the sign at other heights, when
        /// the board's middle was the pivot and the text 7 cm in front of it went 7 cm over it and 7 cm under it).</summary>
        public static void FaceTip(double tiltDeg, double textZ, out double forward, out double up)
        {
            double a = tiltDeg * Math.PI / 180;
            forward = textZ - textZ * Math.Cos(a);                  // the text's middle (0, textZ) less the same tipped
            up = -textZ * Math.Sin(a);
        }

        /// <summary>How far the k-th sign's board top is over the floor's collider (k = 0: the root; negative: under it).</summary>
        public static double BoardTop(int k) => Lift - TextZ + BoardHalf + k * Step;

        /// <summary>The same for the k-th sign's collider: under the floor's, nobody trips on it or hovers it.</summary>
        public static double ColliderTop(int k) => Lift - TextZ + ColliderHalf + k * Step;
    }
}
