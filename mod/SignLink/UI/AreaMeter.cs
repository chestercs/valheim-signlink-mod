using SignLink.Pictures;
using SignLink.Signs;
using UnityEngine;

namespace SignLink.UI
{
    /// <summary>The multiplayer area limit at a glance (author, 2026-10-11: an indicator in the mod that shows the area
    /// limit, say as 250kb/600kb): a bar of what SignLink's signs within SaveCost.AreaRadiusM of the
    /// selected sign hold, every player's (SignArea), against SaveCost.AreaLimit, with the picked content in place of the
    /// sign's own when that changes it, and its words with how many SignLink signs that is (0.20.67, the author: not 58
    /// signs, rather how many SignLink signs there are), in the footers of 2 Content and 3 Place. Only in a
    /// shared world with a sign selected; single player has no limit.</summary>
    internal static class AreaMeter
    {
        static readonly Color Back = new Color(0.07f, 0.065f, 0.06f, 1f);
        static readonly Color Used = new Color(0.55f, 0.85f, 0.45f, 1f);
        static readonly Color Near = new Color(0.98f, 0.76f, 0.36f, 1f);
        static readonly Color Over = new Color(1f, 0.45f, 0.35f, 1f);

        /// <summary>One row in a footer, over the button that puts it on (0.20.62, the author: make it part of the footer
        /// with the place button instead of up top), or under it (underButton: 2 Content's, 0.20.72): the bar and its words,
        /// coloured as full as the area is. newBytes: what the sign would hold with the picked content (SaveCost.Bytes),
        /// or -1 to show the area as it is.</summary>
        public static void Draw(Session s, long newBytes, bool underButton = false)
        {
            if (!SignText.Shared() || !s.HasTarget) return;
            if (underButton) Widgets.Space(8);                              // room from the button over it
            long now = s.AreaNow;                                           // SignLink's signs only, everyone's (0.20.67)
            long after = newBytes >= 0 ? s.AreaOthers + newBytes : now;     // what SignLink puts on counts
            long shown = System.Math.Max(now, after);
            Color c = shown > SaveCost.AreaLimit ? Over : shown > SaveCost.AreaLimit * 3 / 4 ? Near : Used;
            using (Widgets.Horizontal())
            {
                GUILayout.Label("Area", Theme.SmallLabel, Widgets.W(52));
                using (Widgets.Vertical(Widgets.W(Widgets.ContentWidth * 0.32f)))
                {
                    Widgets.Space(7);                                       // the bar in the middle of the words' line
                    Rect r = GUILayoutUtility.GetRect(Widgets.U(60), Widgets.U(10), GUILayout.ExpandWidth(true), Widgets.H(10));
                    if (Event.current.type == EventType.Repaint)
                    {
                        Fill(r, 1f, Back);
                        float a = Mathf.Clamp01(Mathf.Min(now, after) / (float)SaveCost.AreaLimit);
                        float b = Mathf.Clamp01(shown / (float)SaveCost.AreaLimit);
                        Fill(r, b, new Color(c.r, c.g, c.b, 0.45f));        // what this adds, lighter
                        Fill(r, a, c);                                      // what is there either way
                    }
                }
                Widgets.Space(8);
                Color was = GUI.contentColor;
                GUI.contentColor = c;
                // what the area will hold with the pick first, what it holds already after it (0.20.74, the author: "598kb/600
                // within 100m - already 299kb", so swap what we print)
                // the longest wording that fits the room left, in that room (0.20.98): the whole line, wider than the row, widened
                // the window past its size and past the screen's edge (the author: some element widens the window)
                string total = $"{SaveCost.Label(after)} / {SaveCost.Label(SaveCost.AreaLimit)}";
                string already = after != now ? $" · already {SaveCost.Label(now)}" : "";
                string signs = $" · {s.AreaCount} SignLink sign{(s.AreaCount == 1 ? "" : "s")}";   // a stack once (0.20.67)
                float room = Widgets.ContentWidth - 30f - 52f - Widgets.ContentWidth * 0.32f - 20f;
                GUILayout.Label(Fitting(room, total + $" within {SaveCost.AreaRadiusM} m" + already + signs, total + already + signs,
                                        total + already, total), Theme.SmallLabel, Widgets.W(room));
                GUI.contentColor = was;
            }
            if (!underButton) Widgets.Space(10);                            // room before the button under it (0.20.71, the
        }                                                                   // author: padding between the button and the indicator)

        static readonly GUIContent measured = new GUIContent();
        static string fitFor, fitGot;
        static float fitRoom, fitScale;

        /// <summary>The first of these wordings, longest first, that fits room (logical units) in the small label; the
        /// last when none does. Kept while the words and the room stay (a Layout and a Repaint a frame).</summary>
        static string Fitting(float room, params string[] wordings)
        {
            if (wordings[0] == fitFor && Mathf.Abs(room - fitRoom) < 0.5f && Widgets.Scale == fitScale) return fitGot;
            string got = wordings[wordings.Length - 1];
            foreach (string w in wordings)
            {
                measured.text = w;
                if (Theme.SmallLabel.CalcSize(measured).x <= Widgets.U(room)) { got = w; break; }
            }
            fitFor = wordings[0];
            fitRoom = room;
            fitScale = Widgets.Scale;
            fitGot = got;
            return got;
        }

        static void Fill(Rect r, float part, Color c)
        {
            if (part <= 0f) return;
            Color was = GUI.color;
            GUI.color = c;
            GUI.DrawTexture(new Rect(r.x, r.y, r.width * part, r.height), Texture2D.whiteTexture);
            GUI.color = was;
        }
    }
}
