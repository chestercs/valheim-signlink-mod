using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace PetyusPolisz.Kit
{
    /// <summary>Words: titles with their hotkeys at the right (Header, SubHeader, Keys), key caps, hints and fact rows.</summary>
    internal static partial class Widgets
    {
        public static void Header(string text)
        {
            GUILayout.Label(Theme.HeaderText(text), Theme.Header);
            HeaderMark();
        }

        /// <summary>The look's diamond before the header just laid out (in its left padding, in the middle of its line).</summary>
        static void HeaderMark()
        {
            Texture2D m = Theme.HeaderMark;
            if (m == null || Event.current.type != EventType.Repaint) return;
            Rect r = GUILayoutUtility.GetLastRect();
            GUI.DrawTexture(new Rect(Mathf.Round(r.x + U(1)), Mathf.Round(r.y + (r.height - m.height) / 2f), m.width, m.height), m);
        }

        /// <summary>A group's title with the hotkeys that work in it at its right (Keys, 1.0.2).</summary>
        public static void Header(string text, params string[] keys)
        {
            using (Horizontal())
            {
                string shown = Theme.HeaderText(text);
                GUILayout.Label(shown, Theme.Header, GUILayout.ExpandWidth(false));
                HeaderMark();
                KeysBeside(Theme.Header, shown, keys);
            }
        }

        /// <summary>A part's title inside a group (0.20.180: 3 Place's two groups, each in two parts); first: no air above;
        /// keys: the hotkeys that work in it, at its right (Keys).</summary>
        public static void SubHeader(string text, bool first = false, params string[] keys)
        {
            if (!first) Space(8);
            if (keys == null || keys.Length == 0)
            {
                GUILayout.Label(text, Theme.SubHeader);
                return;
            }
            using (Horizontal())
            {
                GUILayout.Label(text, Theme.SubHeader, GUILayout.ExpandWidth(false));
                KeysBeside(Theme.SubHeader, text, keys);
            }
        }

        /// <summary>Hotkeys at the right of the row being laid out, each in a small dim frame (1.0.2, the author: where a
        /// hotkey works, say so where it is seen, out of the way).</summary>
        public static void Keys(params string[] caps)
        {
            GUILayout.FlexibleSpace();
            foreach (string c in caps) GUILayout.Label(c, Theme.KeyHint, GUILayout.ExpandWidth(false));
        }

        static readonly GUIContent OneLine = new GUIContent("X");

        /// <summary>Hotkeys right where the row has got to (nothing pushes them right), level with the middle of a control
        /// drawn in `beside` (1.1.0: ↑ ↓ beside the button that opens a list): in a column as tall as that control, centred
        /// by flexible space (a measured space left them a few pixels high: the space and the margins do not add up as
        /// measured). A column stretched to the row's height stretched the whole bar instead (the author's picture).</summary>
        public static void KeysHere(GUIStyle beside, params string[] caps)
        {
            float bh = beside.CalcHeight(OneLine, 10000f);
            if (Theme.InTrack(beside)) bh += Theme.Track.padding.vertical;   // drawn in a track (Toggle, TabButton): its padding too
            using (Vertical(GUILayout.ExpandWidth(false), GUILayout.Height(beside.margin.top + bh)))
            {
                GUILayout.Space(beside.margin.top);
                GUILayout.FlexibleSpace();
                using (Horizontal())
                    foreach (string c in caps) GUILayout.Label(c, Theme.KeyHint, GUILayout.ExpandWidth(false));
                GUILayout.FlexibleSpace();
            }
        }

        /// <summary>The keys in a column moved down by top (KeysBeside).</summary>
        static void KeysAt(float top, string[] caps)
        {
            using (Vertical(GUILayout.ExpandWidth(false)))
            {
                GUILayout.Space(top);
                using (Horizontal())
                    foreach (string c in caps) GUILayout.Label(c, Theme.KeyHint, GUILayout.ExpandWidth(false));
            }
        }

        // each title's keys' top, by its style and words, for the styles built now (Theme.Ensure makes new ones) at a scale:
        // no key string made on every event (1.1.0, review)
        static readonly Dictionary<GUIStyle, Dictionary<string, float>> keyTops = new Dictionary<GUIStyle, Dictionary<string, float>>();
        static float keyTopScale;
        static GUIStyle keyTopsFor;

        /// <summary>Keys beside a title, level with its words (the author: they hung above the title).</summary>
        static void KeysBeside(GUIStyle title, string text, string[] caps)
        {
            GUILayout.FlexibleSpace();
            if (keyTopScale != Scale || !ReferenceEquals(keyTopsFor, Theme.KeyHint))
            {
                keyTops.Clear();
                keyTopScale = Scale;
                keyTopsFor = Theme.KeyHint;
            }
            if (!keyTops.TryGetValue(title, out Dictionary<string, float> tops)) keyTops[title] = tops = new Dictionary<string, float>();
            if (!tops.TryGetValue(text, out float top))
            {
                float th = title.CalcHeight(new GUIContent(text), 10000f), kh = Theme.KeyHint.CalcHeight(OneLine, 10000f);
                tops[text] = top = Mathf.Max(0f, title.margin.top + (th - kh) / 2f - Theme.KeyHint.margin.top);
            }
            KeysAt(top, caps);
        }

        /// <summary>Shift + mouse wheel over a slider's row: a fine step (Number).</summary>
        public const string WheelKey = "Shift+wheel";

        public static void Hint(string text) => GUILayout.Label(text, Theme.Small);

        /// <summary>A key combination as key caps that are buttons (Theme.KeyButton: the hotkey, clicked to change it; 1.1.0,
        /// the author: no Change key button); true when one was clicked. The rest of the row is the caller's.</summary>
        public static bool KeyCapButtons(string[] parts)
        {
            bool hit = false;
            for (int i = 0; i < parts.Length; i++)
            {
                if (i > 0) GUILayout.Label("+", Theme.SmallLabel, GUILayout.ExpandWidth(false));
                if (GUILayout.Button(parts[i], Theme.KeyButton, GUILayout.ExpandWidth(false))) hit = true;
            }
            return hit;
        }

        /// <summary>A label and its value on one row (an info table); width: the value's, when the table does not span
        /// the body; labelW: a table's own label column, for labels longer than the usual.</summary>
        public static void Field(string label, string value, GUIStyle style = null, float width = 0, float labelW = 110)
        {
            using (Horizontal())
            {
                GUILayout.Label(label, Theme.Small, W(labelW));
                GUILayout.Label(value, style ?? Theme.Label, W(width > 0 ? width : ContentWidth - 150));
            }
        }
    }
}
