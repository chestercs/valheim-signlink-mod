using System.Collections.Generic;
using UnityEngine;

namespace PetyusPolisz.Kit
{
    /// <summary>A fill bar with its words beside it, in a row: what is there, what would come added (lighter), against a
    /// limit, coloured by the caller (Good, Near, Over, Info), the longest wording that fits the row. SignLink's Area and
    /// GIFs bars are two of them.</summary>
    internal static class Meter
    {
        // the look's colours (Theme: the flat ones as they always were, a painted look's from its tokens)
        public static Color Back => Theme.MeterBack;
        public static Color Good => Theme.MeterGood;
        public static Color Near => Theme.MeterNear;
        public static Color Over => Theme.MeterOver;

        public static Color Info => Theme.MeterInfo;

        /// <summary>A bar and its words: what is there (now), with what comes added lighter (after), against limit; the
        /// longest wording that fits the row.</summary>
        public static void Bar(string label, long now, long after, long limit, Color c, params string[] wordings)
        {
            using (Widgets.Horizontal())
            {
                GUILayout.Label(label, Theme.SmallLabel, Widgets.W(52));
                using (Widgets.Vertical(Widgets.W(Widgets.ContentWidth * 0.32f)))
                {
                    bool round = Theme.MeterTrack != null;                  // a painted look: round ends
                    Widgets.Space(round ? 9 : 7);                           // the bar in the middle of the words' line
                    float bar = round ? 6 : 10;
                    Rect r = GUILayoutUtility.GetRect(Widgets.U(60), Widgets.U(bar), GUILayout.ExpandWidth(true), Widgets.H(bar));
                    if (Event.current.type == EventType.Repaint)
                    {
                        if (round)
                        {
                            Pill(r, 1f, Theme.MeterTrack, Color.white);
                            Pill(r, Mathf.Clamp01(after / (float)limit), Theme.MeterFill, new Color(c.r, c.g, c.b, 0.45f));
                            Pill(r, Mathf.Clamp01(now / (float)limit), Theme.MeterFill, c);
                        }
                        else
                        {
                            Fill(r, 1f, Back);
                            Fill(r, Mathf.Clamp01(after / (float)limit), new Color(c.r, c.g, c.b, 0.45f));   // what this adds, lighter
                            Fill(r, Mathf.Clamp01(now / (float)limit), c);    // what is there either way
                        }
                    }
                }
                Widgets.Space(8);
                Color was = GUI.contentColor;
                GUI.contentColor = c;
                // the longest wording that fits the room left, in that room (0.20.98): the whole line, wider than the row, widened
                // the window past its size and past the screen's edge (the author: some element widens the window)
                float room = Widgets.ContentWidth - 30f - 52f - Widgets.ContentWidth * 0.32f - 20f;
                GUILayout.Label(Fitting(room, wordings), Theme.MeterWords, Widgets.W(room));
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

        /// <summary>A part of the bar as a rounded piece (a nine-sliced style tinted by c), never narrower than its round ends.</summary>
        static void Pill(Rect r, float part, GUIStyle style, Color c)
        {
            if (part <= 0f) return;
            Color was = GUI.backgroundColor;
            GUI.backgroundColor = c;
            style.Draw(new Rect(r.x, r.y, Mathf.Min(r.width, Mathf.Max(r.height, r.width * part)), r.height), false, false, false, false);
            GUI.backgroundColor = was;
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
