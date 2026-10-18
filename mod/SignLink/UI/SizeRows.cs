using System;
using UnityEngine;
using PetyusPolisz.Kit;

namespace SignLink.UI
{
    /// <summary>The Size rows of a source on 3 Place › Content size (Art, Text), like the picture's Width there: the
    /// piece's width and height in metres when its size as made is known, else a percentage.
    /// They show Session.Chosen.Fit.Scale and write it back (Layout only); 3 Place applies it (a Refit of the texts).
    /// Greyed when the piece cannot be resized (TextTransform). One instance per source (its own field names).</summary>
    internal sealed class SizeRows
    {
        readonly string key;
        float pct = 100f, seenPct = 100f;

        public SizeRows(string key) => this.key = key;

        /// <summary>naturalM: the piece's size as made (zero: unknown, a percentage shows); movable: it can be resized;
        /// hint: a line under the rows (how the size snaps, why it cannot change), or null; scale: the size a scale really
        /// takes (exact line heights), so Shift + wheel goes to the next one, or null.</summary>
        public void Draw(Session s, Vector2 naturalM, bool movable, string hint, Func<double, double> scale = null)
        {
            bool layout = Event.current.type == EventType.Layout;
            float chosen = (float)(s.Chosen.Fit.Scale * 100);
            if (layout && Math.Abs(chosen - seenPct) > 1e-3f) pct = seenPct = chosen;   // changed elsewhere (another sign, Undo,
            bool was = GUI.enabled;                                                     // As made): the rows take it
            using (Widgets.Vertical(Theme.Section))                         // under 3 Place's Size title
            {
                GUI.enabled = was && movable;
                pct = Widgets.SizeRows(key, pct, naturalM, scale);
                GUI.enabled = was;
            }
            if (hint != null) Widgets.Hint(hint);
            if (layout)                                                     // ... and the choice takes the rows
            {
                s.Chosen.Fit.Scale = pct / 100.0;
                seenPct = pct;
            }
        }
    }
}
