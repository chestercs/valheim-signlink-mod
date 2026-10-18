using SignLink.Pictures;
using SignLink.Signs;
using UnityEngine;
using PetyusPolisz.Kit;

namespace SignLink.UI
{
    /// <summary>The multiplayer area limit at a glance (author, 2026-10-11: an indicator in the mod that shows the area
    /// limit, say as 250kb/600kb): a bar of what SignLink's signs within SaveCost.AreaRadiusM of the
    /// selected sign hold, every player's (SignArea), against SaveCost.AreaLimit, with the picked content in place of the
    /// sign's own when that changes it, and its words with how many SignLink signs that is (0.20.67, the author: not 58
    /// signs, rather how many SignLink signs there are), in the footers of 2 Content and 3 Place, with a sign selected; in single player for
    /// information (no limit there).</summary>
    internal static class AreaMeter
    {

        /// <summary>One row in a footer, over the button that puts it on (0.20.62, the author: make it part of the footer
        /// with the place button instead of up top), or under it (underButton: 2 Content's, 0.20.72): the bar and its words,
        /// coloured as full as the area is. newBytes: what the sign would hold with the picked content (SaveCost.Bytes),
        /// or -1 to show the area as it is. Single player too (1.0.2, the author: an indicator there as well): measured
        /// against the server limit, in grey and amber only, nothing refused.</summary>
        public static void Draw(Session s, long newBytes, bool underButton = false)
        {
            if (!s.HasTarget) return;
            bool shared = SignText.Shared();
            if (underButton) Widgets.Space(8);                              // room from the button over it
            long now = s.AreaNow;                                           // SignLink's signs only, everyone's (0.20.67)
            long after = newBytes >= 0 ? s.AreaOthers + newBytes : now;     // what SignLink puts on counts
            long shown = System.Math.Max(now, after);
            Color c = shown > SaveCost.AreaLimit ? (shared ? Meter.Over : Meter.Near) : !shared ? Meter.Info : shown > SaveCost.AreaLimit * 3 / 4 ? Meter.Near : Meter.Good;
            // what the area will hold with the pick first, what it holds already after it (0.20.74, the author: "598kb/600
            // within 100m - already 299kb", so swap what we print)
            string total = $"{SaveCost.Label(after)} / {SaveCost.Label(SaveCost.AreaLimit)}" + (shared ? "" : " on a server");
            string already = after != now ? $" · already {SaveCost.Label(now)}" : "";
            string signs = $" · {s.AreaCount} SignLink sign{(s.AreaCount == 1 ? "" : "s")}";   // a stack once (0.20.67)
            Meter.Bar("Area", System.Math.Min(now, after), shown, SaveCost.AreaLimit, c,
                total + $" within {SaveCost.AreaRadiusM} m" + already + signs, total + already + signs, total + already, total);
            if (!underButton) Widgets.Space(10);                            // room before the button under it (0.20.71, the
        }                                                                   // author: padding between the button and the indicator)

        /// <summary>The frames the moving GIFs hold on this computer against what they may (ScreenPlayer.Budget: the one hard
        /// limit in single player too, 1.0.2), with adding: what a GIF about to go on would add (0: as it is).</summary>
        public static void Gifs(long adding)
        {
            long budget = Animation.ScreenPlayer.Budget, now = budget - Plugin.Screens.Room, after = now + System.Math.Max(0, adding);
            Color c = after > budget ? Meter.Over : after > budget * 3 / 4 ? Meter.Near : Meter.Good;
            string total = $"{after >> 20} / {budget >> 20} MB";
            string already = after != now ? $" · already {now >> 20} MB" : "";
            Meter.Bar("GIFs", now, after, budget, c, total + " of moving GIF frames here" + already, total + already, total);
        }

    }
}
