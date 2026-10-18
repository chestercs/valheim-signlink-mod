using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace PetyusPolisz.Kit
{
    /// <summary>Words: titles with their hotkeys at the right (Header, SubHeader, Keys), key caps, hints and fact rows.</summary>
    internal static partial class Widgets
    {
        public static void Header(string text) => GUILayout.Label(text, Theme.Header);

        /// <summary>A group's title with the hotkeys that work in it at its right (Keys, 1.0.2).</summary>
        public static void Header(string text, params string[] keys)
        {
            using (Horizontal())
            {
                GUILayout.Label(text, Theme.Header, GUILayout.ExpandWidth(false));
                KeysBeside(Theme.Header, text, keys);
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

        static readonly Dictionary<string, float> keyTops = new Dictionary<string, float>();   // by style and title, at a scale
        static float keyTopScale;

        /// <summary>Keys beside a title, level with its words (the author: they hung above the title).</summary>
        static void KeysBeside(GUIStyle title, string text, string[] caps)
        {
            GUILayout.FlexibleSpace();
            string key = title.fontSize + "|" + title.margin.top + "|" + text;  // the styles share a name
            if (!keyTops.TryGetValue(key, out float top) || keyTopScale != Scale)
            {
                if (keyTopScale != Scale) keyTops.Clear();
                keyTopScale = Scale;
                float th = title.CalcHeight(new GUIContent(text), 10000f), kh = Theme.KeyHint.CalcHeight(new GUIContent("X"), 10000f);
                keyTops[key] = top = Mathf.Max(0f, title.margin.top + (th - kh) / 2f - Theme.KeyHint.margin.top);
            }
            using (Vertical(GUILayout.ExpandWidth(false)))
            {
                GUILayout.Space(top);
                using (Horizontal())
                    foreach (string c in caps) GUILayout.Label(c, Theme.KeyHint, GUILayout.ExpandWidth(false));
            }
        }

        /// <summary>Shift + mouse wheel over a slider's row: a fine step (Number).</summary>
        public const string WheelKey = "Shift+wheel";

        public static void Hint(string text) => GUILayout.Label(text, Theme.Small);

        /// <summary>A key combination as key caps, each in a small frame, "+" between (0.20.82, the author: some small
        /// frame that we write the current hotkey into), then the rest of the row free.</summary>
        public static void KeyCaps(string[] parts)
        {
            for (int i = 0; i < parts.Length; i++)
            {
                if (i > 0) GUILayout.Label("+", Theme.SmallLabel, GUILayout.ExpandWidth(false));
                GUILayout.Label(parts[i], Theme.KeyCap, GUILayout.ExpandWidth(false));
            }
            GUILayout.FlexibleSpace();
        }

        /// <summary>A label and its value on one row (an info table); width: the value's, when the table does not span
        /// the body.</summary>
        public static void Field(string label, string value, GUIStyle style = null, float width = 0)
        {
            using (Horizontal())
            {
                GUILayout.Label(label, Theme.Small, W(110));
                GUILayout.Label(value, style ?? Theme.Label, W(width > 0 ? width : ContentWidth - 150));
            }
        }
    }
}
