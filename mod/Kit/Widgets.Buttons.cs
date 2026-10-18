using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace PetyusPolisz.Kit
{
    /// <summary>Buttons and switches: fitted buttons, toolbars and tabs, the leading (Next) button, foldouts, switches, rows of
    /// buttons.</summary>
    internal static partial class Widgets
    {
        /// <summary>GUILayout.Button whose label always shows whole (0.20.50): laid out as GUILayout lays it out, drawn
        /// with Theme.Fitted for the width it got (less padding, then a smaller font) when the label is wider.</summary>
        public static bool Button(string text, GUIStyle style, params GUILayoutOption[] options)
        {
            label.text = text;
            Rect r = GUILayoutUtility.GetRect(label, style, options);
            return GUI.Button(r, label, Theme.Fitted(style, label, r.width));
        }

        /// <summary>A button as wide as its row lets it be, never wider (0.20.98): without a width GUILayout gives a button
        /// at least its label's width, so a long label ("Apply and tilt it to 12.5°", "3 Place ▸ ...") widened the whole
        /// window past its size and past the screen's edge, and the rows moved as the label changed (the author: some
        /// element widens the window unexpectedly). The label is fitted to the width it gets (Button).</summary>
        public static GUILayoutOption[] Wide(float height) => new[] { H(height), GUILayout.MinWidth(U(60)), GUILayout.ExpandWidth(true) };

        /// <summary>One label at a time, as GUILayout's own buttons share one (0.20.70, review: a new GUIContent for every
        /// button in every event).</summary>
        static readonly GUIContent label = new GUIContent();

        /// <summary>A toolbar's labels, kept with its array of texts (most are static) and refreshed when a text changes.</summary>
        static readonly System.Runtime.CompilerServices.ConditionalWeakTable<string[], GUIContent[]> labels =
            new System.Runtime.CompilerServices.ConditionalWeakTable<string[], GUIContent[]>();

        static GUIContent[] Labels(string[] texts)
        {
            GUIContent[] cs = labels.GetValue(texts, t => new GUIContent[t.Length]);
            for (int i = 0; i < texts.Length; i++)
                if (cs[i] == null) cs[i] = new GUIContent(texts[i]);
                else if (!ReferenceEquals(cs[i].text, texts[i])) cs[i].text = texts[i];
            return cs;
        }

        /// <summary>GUILayout.Toolbar the same way: one fitted style for every button, for the widest label in a button's
        /// share of the width.</summary>
        public static int Toolbar(int selected, string[] texts, GUIStyle style, params GUILayoutOption[] options)
        {
            Rect r = Strip(texts, style, options, out GUIContent[] cs, out float each, out float gap, out GUIStyle fitted);
            if (texts.Length == 0) return selected;
            return GUI.Toolbar(r, selected, cs, fitted);
        }

        /// <summary>The window's tab strip: a Toolbar to look at, with the tabs that cannot open now greyed (0.20.73, the
        /// author: until a sign is picked, the 2 Content and 3 Place tabs are grey and disabled). Returns the
        /// tab clicked, else selected.</summary>
        public static int Tabs(int selected, string[] texts, bool[] usable, GUIStyle style, params GUILayoutOption[] options)
        {
            Rect r = Strip(texts, style, options, out GUIContent[] cs, out float each, out float gap, out GUIStyle fitted);
            bool was = GUI.enabled;
            int got = selected;
            for (int i = 0; i < texts.Length; i++)
            {
                GUI.enabled = was && (usable == null || i >= usable.Length || usable[i]);
                if (GUI.Toggle(new Rect(r.x + i * (each + gap), r.y, each, r.height), selected == i, cs[i], fitted) && selected != i) got = i;
            }
            GUI.enabled = was;
            return got;
        }

        /// <summary>A row of equal buttons as GUILayout.Toolbar lays it out (the widest label times the count), and the
        /// style fitted for the widest label in a button's share of it.</summary>
        static Rect Strip(string[] texts, GUIStyle style, GUILayoutOption[] options, out GUIContent[] cs, out float each,
                          out float gap, out GUIStyle fitted)
        {
            cs = Labels(texts);
            float most = 0f, h = 0f;
            int widest = 0;
            for (int i = 0; i < texts.Length; i++)
            {
                Vector2 sz = style.CalcSize(cs[i]);
                if (sz.x > most)
                {
                    most = sz.x;
                    widest = i;
                }
                h = Mathf.Max(h, sz.y);
            }
            gap = Mathf.Max(style.margin.left, style.margin.right);         // as GUILayout.Toolbar: the widest button times the count
            Rect r = GUILayoutUtility.GetRect(most * texts.Length + gap * Math.Max(0, texts.Length - 1), h, style, options);
            each = texts.Length == 0 ? 0 : (r.width - gap * (texts.Length - 1)) / texts.Length;
            fitted = texts.Length == 0 ? style : Theme.Fitted(style, cs[widest], each);
            return r;
        }

        /// <summary>A settings row: the label in the label column, then a strip of choices (chips unless style says
        /// otherwise) at most `most` units wide, the rest of the row free. Returns the choice clicked, else selected; the
        /// caller applies it at a later Layout.</summary>
        public static int Choice(string rowLabel, int selected, string[] names, float most = 0f, GUIStyle style = null)
        {
            using (Horizontal())
            {
                GUILayout.Label(rowLabel, Theme.Label, W(LabelW));
                int pick = Toolbar(selected, names, style ?? Theme.Chip, W(most > 0f ? Mathf.Min(most, RestW) : RestW));
                GUILayout.FlexibleSpace();
                return pick;
            }
        }

        static readonly string[] OnOffNames = { "On", "Off" };

        /// <summary>A settings row with On and Off; returns the value clicked (else the one given).</summary>
        public static bool OnOff(string rowLabel, bool on, GUIStyle style = null) =>
            Choice(rowLabel, on ? 0 : 1, OnOffNames, 200f, style) == 0;

        /// <summary>GUILayout.Toggle drawn as a button (Theme.Tab) the same way.</summary>
        public static bool Toggle(bool value, string text, GUIStyle style, params GUILayoutOption[] options)
        {
            label.text = text;
            Rect r = GUILayoutUtility.GetRect(label, style, options);
            return GUI.Toggle(r, value, label, Theme.Fitted(style, label, r.width));
        }

        /// <summary>A button that leads on (2 Content ▸, 3 Place ▸, the verb that puts it on): bright amber with a slow
        /// glow while it can be pressed, the plain button while greyed (0.20.48, the author: more inviting,
        /// brighter).</summary>
        public static bool NextButton(string label, float height = 36f, float width = 0f, bool less = false)
        {
            GUILayoutOption[] size = width > 0f ? new[] { H(height), W(width) } : Wide(height);
            if (!GUI.enabled) return Button(label, less ? Theme.ButtonLess : Theme.Button, size);
            Color was = GUI.backgroundColor;
            GUI.backgroundColor = Glow();
            bool hit = Button(label, less ? Theme.NextLess : Theme.Next, size);
            GUI.backgroundColor = was;
            return hit;
        }

        /// <summary>The slow glow of a button in the way-on yellow, as a background tint.</summary>
        static Color Glow()
        {
            float glow = 0.84f + 0.16f * (0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 2.4f));
            return new Color(glow, glow, glow, 1f);
        }

        /// <summary>A button as small as a chip (Theme.SmallButton) in the way-on yellow with NextButton's glow while it can
        /// be pressed, the plain small button while greyed (1.0.2, the author: the text editor's Insert, yellow and well
        /// seen whenever it can be pressed).</summary>
        public static bool SmallGoButton(string label, params GUILayoutOption[] options)
        {
            if (!GUI.enabled) return Button(label, Theme.SmallButton, options);
            Color was = GUI.backgroundColor;
            GUI.backgroundColor = Glow();
            bool hit = Button(label, Theme.SmallGo, options);
            GUI.backgroundColor = was;
            return hit;
        }
        public static GUILayoutOption MinW(float x) => GUILayout.MinWidth(U(x));
        public static void Space(float x) => GUILayout.Space(U(x));

        static readonly Dictionary<string, bool> folds = new Dictionary<string, bool>();
        static readonly HashSet<string> flips = new HashSet<string>();

        /// <summary>A section's title that opens and closes it (0.20.59: 3 Place's sections): ▾ open, ▸ closed, `open` at
        /// first, remembered while the game runs. A click flips it at the next Layout, so a frame never draws two ways. A
        /// section with nothing usable in it shows closed, its title grey, and opens as it was once usable again (0.20.77,
        /// the author: when I work with a text, say, Content positioning and Content size are disabled; then this group
        /// should be closed).</summary>
        public static bool Foldout(string key, string title, bool open = true, bool usable = true)
        {
            if (folds.TryGetValue(key, out bool o)) open = o;
            if (Event.current.type == EventType.Layout && flips.Remove(key)) folds[key] = open = !open;
            bool shown = open && usable, was = GUI.enabled;
            GUI.enabled = was && usable;
            if (Button((shown ? "▾  " : "▸  ") + title, Theme.Fold)) flips.Add(key);
            GUI.enabled = was;
            return shown;
        }

        /// <summary>A thin amber line across the row (a separation, like the one under the window's title).</summary>
        public static void Rule()
        {
            float t = Mathf.Max(1f, U(1.5f));
            Rect r = GUILayoutUtility.GetRect(U(10), t, GUILayout.ExpandWidth(true), GUILayout.Height(t));
            if (Event.current.type != EventType.Repaint) return;
            Color was = GUI.color;
            GUI.color = new Color(0.98f, 0.76f, 0.36f, 0.45f);
            GUI.DrawTexture(r, Texture2D.whiteTexture);
            GUI.color = was;
        }

        /// <summary>A switch with its words beside it, wrapping when the window is narrow (0.20.74): the box is the toggle,
        /// the words a label that flips it too. A toggle with its words inside stays one line and runs out of the window.</summary>
        public static bool Check(bool value, string text)
        {
            bool v = value;
            using (Horizontal())
            {
                if (GUILayout.Toggle(value, GUIContent.none, Theme.Toggle, W(30)) != value) v = !value;
                if (GUILayout.Button(text, Theme.CheckText)) v = !value;
            }
            return v;
        }

        /// <summary>A row with a label and buttons on the right (each as wide as its text needs); returns the index of
        /// the button pressed, or -1.</summary>
        public static int Row(string label, params string[] buttons)
        {
            int hit = -1;
            var widths = new float[buttons.Length];                         // screen pixels
            float all = 0;
            for (int i = 0; i < buttons.Length; i++)
            {
                widths[i] = Math.Max(U(70), Theme.Button.CalcSize(new GUIContent(buttons[i])).x + U(16));
                all += widths[i] + U(4);
            }
            using (Horizontal(Theme.Section))
            {
                GUILayout.Label(label, Theme.Label, GUILayout.Width(U(ContentWidth - 40) - all));
                for (int i = 0; i < buttons.Length; i++)
                    if (GUILayout.Button(buttons[i], Theme.Button, GUILayout.Width(widths[i]))) hit = i;
            }
            return hit;
        }
    }
}
