using System.Collections.Generic;
using UnityEngine;

namespace PetyusPolisz.Kit
{
    /// <summary>A fill bar with its words beside it, in a row: what is there, what would come added (lighter), against a
    /// limit, coloured by the caller (Good, Near, Over, Info), the longest wording that fits the row. SignLink's Area and
    /// GIFs bars are two of them.</summary>
    internal static class Meter
    {
        public static readonly Color Back = new Color(0.07f, 0.065f, 0.06f, 1f);
        public static readonly Color Good = new Color(0.55f, 0.85f, 0.45f, 1f);
        public static readonly Color Near = new Color(0.98f, 0.76f, 0.36f, 1f);
        public static readonly Color Over = new Color(1f, 0.45f, 0.35f, 1f);

        public static readonly Color Info = new Color(0.70f, 0.67f, 0.60f, 1f);

        /// <summary>A bar and its words: what is there (now), with what comes added lighter (after), against limit; the
        /// longest wording that fits the row.</summary>
        public static void Bar(string label, long now, long after, long limit, Color c, params string[] wordings)
        {
            using (Widgets.Horizontal())
            {
                GUILayout.Label(label, Theme.SmallLabel, Widgets.W(52));
                using (Widgets.Vertical(Widgets.W(Widgets.ContentWidth * 0.32f)))
                {
                    Widgets.Space(7);                                       // the bar in the middle of the words' line
                    Rect r = GUILayoutUtility.GetRect(Widgets.U(60), Widgets.U(10), GUILayout.ExpandWidth(true), Widgets.H(10));
                    if (Event.current.type == EventType.Repaint)
                    {
                        Fill(r, 1f, Back);
                        Fill(r, Mathf.Clamp01(after / (float)limit), new Color(c.r, c.g, c.b, 0.45f));   // what this adds, lighter
                        Fill(r, Mathf.Clamp01(now / (float)limit), c);    // what is there either way
                    }
                }
                Widgets.Space(8);
                Color was = GUI.contentColor;
                GUI.contentColor = c;
                // the longest wording that fits the room left, in that room (0.20.98): the whole line, wider than the row, widened
                // the window past its size and past the screen's edge (the author: some element widens the window)
                float room = Widgets.ContentWidth - 30f - 52f - Widgets.ContentWidth * 0.32f - 20f;
                GUILayout.Label(Fitting(room, wordings), Theme.SmallLabel, Widgets.W(room));
                GUI.contentColor = was;
            }
        }

        static readonly GUIContent measured = new GUIContent();
        sealed class Fitted
        {
            public float Room, Scale;
            public string Got;
        }
        /// <summary>The wording each bar took, by its longest wording: two bars on one tab no longer miss each other's (1.0.2, review).</summary>
        static readonly Dictionary<string, Fitted> fitted = new Dictionary<string, Fitted>();

        /// <summary>The first of these wordings, longest first, that fits room (logical units) in the small label; the
        /// last when none does. Kept while the words and the room stay (a Layout and a Repaint a frame).</summary>
        static string Fitting(float room, params string[] wordings)
        {
            if (fitted.TryGetValue(wordings[0], out Fitted f) && Mathf.Abs(room - f.Room) < 0.5f && Widgets.Scale == f.Scale) return f.Got;
            string got = wordings[wordings.Length - 1];
            foreach (string w in wordings)
            {
                measured.text = w;
                if (Theme.SmallLabel.CalcSize(measured).x <= Widgets.U(room)) { got = w; break; }
            }
            if (fitted.Count > 64) fitted.Clear();                          // the words change with the numbers
            fitted[wordings[0]] = new Fitted { Room = room, Scale = Widgets.Scale, Got = got };
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
