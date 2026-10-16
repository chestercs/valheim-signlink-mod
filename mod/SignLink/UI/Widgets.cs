using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace SignLink.UI
{
    /// <summary>Small IMGUI building blocks with the window's look (Theme). Every size in the UI is written in logical
    /// units (the window as designed at 1080p) and goes through U to the screen: the text is drawn at its own pixel
    /// size (Theme.Ensure), not magnified through a matrix, so it stays crisp at 1440p and 4K.</summary>
    internal static class Widgets
    {
        static readonly Dictionary<string, string> buffers = new Dictionary<string, string>(), typedFrom = new Dictionary<string, string>();
        static readonly Dictionary<string, float> returned = new Dictionary<string, float>();

        /// <summary>Screen pixels per logical unit: the window's text scale, set by the window every Layout.</summary>
        public static float Scale = 1f;

        public static float U(float x) => x * Scale;
        public static GUILayoutOption W(float x) => Option(widths, x, true);
        public static GUILayoutOption H(float x) => Option(heights, x, false);

        /// <summary>A width or height option per logical size at the scale it was made for (0.20.132, review: two objects
        /// for every row's W and H in every event); IMGUI only reads an option, so one serves every row. The sizes worked
        /// out from the window's own (ContentWidth - 266) change while it is resized, so a table that grows past
        /// MostOptions starts again.</summary>
        static GUILayoutOption Option(Dictionary<float, GUILayoutOption> cache, float x, bool width)
        {
            if (optionsScale != Scale)
            {
                widths.Clear();
                heights.Clear();
                optionsScale = Scale;
            }
            if (cache.TryGetValue(x, out GUILayoutOption o)) return o;
            if (cache.Count >= MostOptions) cache.Clear();
            return cache[x] = width ? GUILayout.Width(U(x)) : GUILayout.Height(U(x));
        }

        static readonly Dictionary<float, GUILayoutOption> widths = new Dictionary<float, GUILayoutOption>(), heights = new Dictionary<float, GUILayoutOption>();
        static float optionsScale = float.NaN;
        const int MostOptions = 512;

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
            float glow = 0.84f + 0.16f * (0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 2.4f));
            GUI.backgroundColor = new Color(glow, glow, glow, 1f);
            bool hit = Button(label, less ? Theme.NextLess : Theme.Next, size);
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

        /// <summary>The width a tab's body has (window minus padding and the vertical scrollbar) in logical units, set
        /// by the window every frame: rows size their labels to it, so nothing ever needs a horizontal scrollbar.</summary>
        public static float ContentWidth = 556f;

        /// <summary>How much taller than the smallest window it is (1 .. 1.6), set by the window: lists and quick views
        /// grow with it.</summary>
        public static float HeightFactor = 1f;

        public static float Tall(float height) => height * HeightFactor;

        /// <summary>Keyboard focus leaves the field at the next Layout event with no mouse button held (hotControl 0).
        /// Never GUI.FocusControl inside the window function during a mouse event: Unity's window system then drops
        /// the mouse-up before it reaches the window, and every button goes dead (0.20.0 on Valheim 1.0.17: a click set
        /// the button pressed and nothing fired).</summary>
        public static void DropFocus() => focusDropWanted = true;

        static bool focusDropWanted;

        /// <summary>Layout events only (SignLinkWindow.OnGUI).</summary>
        public static void ApplyFocusDrop()
        {
            if (!focusDropWanted || GUIUtility.hotControl != 0) return;
            focusDropWanted = false;
            GUIUtility.keyboardControl = 0;
        }

        /// <summary>The label column of a settings row and what the rest of the row gets: 190 units, narrower in a narrow
        /// window (0.20.74, the author: in a narrow window things do not wrap and run off the screen), so
        /// a row never runs out of the window (it has no sideways scroll).</summary>
        public static float LabelW => Mathf.Clamp(ContentWidth * 0.34f, 110f, 190f);
        public static float RestW => ContentWidth - LabelW - 10f;

        /// <summary>A window too narrow for a label, a value and buttons on one row: rows that would run out go on two.</summary>
        public static bool Narrow => ContentWidth < 560f;

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

        /// <summary>A layout group that closes itself at the end of its using block, also when a call inside it throws:
        /// an unclosed group mislays the whole window for the rest of the frame.</summary>
        public readonly struct Scope : IDisposable
        {
            readonly int kind;                                              // 0 horizontal, 1 vertical, 2 scroll view

            public Scope(int kind) => this.kind = kind;

            public void Dispose()
            {
                try
                {
                    if (kind == 0) GUILayout.EndHorizontal();
                    else if (kind == 1) GUILayout.EndVertical();
                    else GUILayout.EndScrollView();
                }
                catch (Exception) { }                                       // closing after ExitGUI: nothing to close
            }
        }

        public static Scope Horizontal(params GUILayoutOption[] opts)
        {
            GUILayout.BeginHorizontal(opts);
            return new Scope(0);
        }

        public static Scope Horizontal(GUIStyle style, params GUILayoutOption[] opts)
        {
            GUILayout.BeginHorizontal(style, opts);
            return new Scope(0);
        }

        public static Scope Vertical(params GUILayoutOption[] opts)
        {
            GUILayout.BeginVertical(opts);
            return new Scope(1);
        }

        public static Scope Vertical(GUIStyle style, params GUILayoutOption[] opts)
        {
            GUILayout.BeginVertical(style, opts);
            return new Scope(1);
        }

        /// <summary>While the window's body draws: the part of it in view, in its scroll's coordinates (null elsewhere). A
        /// row scrolled out of view is not under the mouse, though the mouse lies inside its rect (Number's Shift + wheel).</summary>
        public static Rect? BodyView;

        /// <summary>A vertical-only scroll view (no horizontal bar, ever) of this logical height, as a scope.</summary>
        public static Scope Scroll(ref Vector2 pos, float height)
        {
            pos = GUILayout.BeginScrollView(pos, false, false, GUIStyle.none, GUI.skin.verticalScrollbar, Theme.Section, H(Tall(height)));
            return new Scope(2);
        }

        public static void Header(string text) => GUILayout.Label(text, Theme.Header);

        /// <summary>A part's title inside a group (0.20.180: 3 Place's two groups, each in two parts); first: no air above.</summary>
        public static void SubHeader(string text, bool first = false)
        {
            if (!first) Space(8);
            GUILayout.Label(text, Theme.SubHeader);
        }

        /// <summary>A selectable list row (amber when selected); true when clicked while not selected.</summary>
        /// <summary>A piece's size: Width and Height (same shape) in metres when its size as made is known (naturalM), else
        /// a percentage; returns the percentage, 25..400 (the Size rows of a source and 3 Place's content rows). scale: the
        /// size a scale really takes (a pixel piece's exact line heights), so Shift + wheel goes to the next one (0.20.98).</summary>
        public static float SizeRows(string key, float pct, Vector2 naturalM, Func<double, double> scale = null)
        {
            float was = pct;
            float got = SizeRowsRaw(key, pct, naturalM, scale);
            if (scale == null || Math.Abs(got - was) < 1e-4f) return got;
            // the slider and a typed size go to the sizes the piece really takes too, not only Shift + wheel, so the row does
            // not jump back after the put (0.20.149, review); a snap that lands where the row is or against the drag: the next
            // size the drag's way
            Func<float, float> snap = p => (float)(scale(p / 100.0) * 100);
            float here = snap(was), to = snap(got);
            if (!(here > 0) || !(to > 0)) return got;                   // no exact sizes (several line heights): as asked
            int dir = Math.Sign(got - was);
            if (Math.Sign(to - here) != dir) to = FineStep.Notch(here, -dir, 5f, "0.#", 25f, 400f, snap);
            return Mathf.Clamp(to, 25f, 400f);
        }

        static float SizeRowsRaw(string key, float pct, Vector2 naturalM, Func<double, double> scale)
        {
            if (!(naturalM.x > 0 && naturalM.y > 0))
                return Number(key + ".size", "Size", pct, 25f, 400f, 5f, "%", "0.#", snap: scale == null ? null : (Func<float, float>)(p => (float)(scale(p / 100.0) * 100)));
            float stepW = Mathf.Clamp(naturalM.x / 20f, 0.01f, 0.05f), stepH = Mathf.Clamp(naturalM.y / 20f, 0.01f, 0.05f);   // about 5 % a step (0.20.149)
            float w0 = pct / 100f * naturalM.x;
            float wide = Number(key + ".width", "Width", w0, naturalM.x / 4, naturalM.x * 4, stepW, "m",
                                snap: scale == null ? null : (Func<float, float>)(w => (float)(scale(w / naturalM.x) * naturalM.x)));
            if (Math.Abs(wide - w0) > 1e-4f) pct = Mathf.Clamp(wide / naturalM.x * 100f, 25f, 400f);
            float h0 = pct / 100f * naturalM.y;
            float h = Number(key + ".height", "Height (same shape)", h0, naturalM.y / 4, naturalM.y * 4, stepH, "m",
                             snap: scale == null ? null : (Func<float, float>)(v => (float)(scale(v / naturalM.y) * naturalM.y)));
            if (Math.Abs(h - h0) > 1e-4f) pct = Mathf.Clamp(h / naturalM.y * 100f, 25f, 400f);
            return pct;
        }

        /// <summary>A list row with a thumbnail (a 56 x 44 box; empty, with boxText in it, while there is none) and the
        /// label; true when clicked while not selected.</summary>
        public static bool PictureItem(bool selected, Texture2D thumb, string label, float width, string boxText = "")
        {
            bool hit;
            using (Horizontal())
            {
                Rect box = GUILayoutUtility.GetRect(U(56), U(44), W(56), H(44));
                if (thumb) GUI.DrawTexture(box, thumb, ScaleMode.ScaleToFit);
                else
                {
                    GUI.Box(box, GUIContent.none, Theme.Section);
                    if (boxText.Length > 0) GUI.Label(box, boxText, Theme.BoxText);
                }
                hit = GUILayout.Toggle(selected, label, Theme.Item, W(width - 62), H(44)) && !selected;
            }
            return hit;
        }

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

        /// <summary>A list with nothing in it (0.20.71, the author: the "none nearby" texts under Playing bigger and
        /// readable, so one can tell this would be a list if it had items): a box in the list's place, its
        /// words in the middle at the labels' size.</summary>
        public static void EmptyList(string text)
        {
            using (Vertical(Theme.Section))
                GUILayout.Label(text, Theme.ListEmpty);
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

        /// <summary>Which way the last Number moved by a Shift + wheel notch (+1 up, -1 down), 0 when it did not: read right
        /// after its call (a picture's fine Width steps, 0.20.122).</summary>
        public static int Notched;

        /// <summary>Which way the last Number's slider was moved by the mouse (+1 right, -1 left), 0 when it was not: read
        /// right after its call by a row that snaps its value to steps (a picture's Resolution and Width, 0.20.175, the author:
        /// a slow drag flickered left and right). The mouse's own way, not the value's against the snapped one: the
        /// snapped value stands ahead of or behind the mouse, so that way turned at every move.</summary>
        public static int Slid;
        static readonly GUIContent SliderSize = new GUIContent("mmmm");     // GUILayout.HorizontalSlider's own measure

        /// <summary>Half-typed numbers dropped (the window opens: 0.20.189, review: a "3" typed in Width and Esc went on as
        /// 3 m at the next opening, an Undo lit at once).</summary>
        public static void ForgetTyping()
        {
            buffers.Clear();
            typedFrom.Clear();
        }
        static string slidKey;
        static float slidRaw;
        static int slidHot;

        /// <summary>A number with a text field (type an exact value; comma or point) and a slider over [min, max] that
        /// snaps to `step`. A typed value may go to +-typedLimit when that is wider than the slider.</summary>
        public static float Number(string key, string label, float value, float min, float max, float step, string unit,
                                   string format = "0.##", float typedLimit = 0f, Func<float, float> snap = null)
        {
            Notched = 0;
            Slid = 0;
            if (float.IsNaN(value) || float.IsInfinity(value)) value = min;
            using (Horizontal())
            {
                GUILayout.Label(label, Theme.Label, W(LabelW));
                // A typed number goes on when it is done: Enter, or the field left (a click elsewhere, Tab). Until then the row
                // keeps its value (0.20.149, the author and a review: digits went on one at a time, "135" turned the sign 1°, 13°,
                // 135°, "812" planned a picture at 8, 81, 812, "1000" wrote 10, 100 and 200 to the config). So the row's value
                // changing while a number is typed comes from elsewhere (a button, Undo changes, another sign): that wins, and
                // the half-typed number goes (it stayed, and Enter put it back).
                string shown = value.ToString(format, CultureInfo.InvariantCulture);
                bool editing = GUI.GetNameOfFocusedControl() == key;
                bool typing = buffers.TryGetValue(key, out string b);
                if (typing && typedFrom.TryGetValue(key, out string from) && from != shown)
                {
                    buffers.Remove(key);                                    // changed elsewhere meanwhile: the new value shows
                    typedFrom.Remove(key);
                    if (editing) DropFocus();
                    editing = typing = false;
                }
                bool enter = editing && Event.current.type == EventType.KeyDown &&
                             (Event.current.keyCode == KeyCode.Return || Event.current.keyCode == KeyCode.KeypadEnter);
                if (typing && (enter || !editing))                          // done: Enter, or the field left
                {
                    if (b != shown && float.TryParse(b.Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out float v) &&
                        !float.IsNaN(v) && !float.IsInfinity(v))
                    {
                        value = typedLimit > 0 ? Mathf.Clamp(v, -typedLimit, typedLimit) : Mathf.Clamp(v, min, max);
                        // to what the row shows: a typed 12.34 on a row of tenths is 12.3 (0.20.149, the author's test)
                        if (float.TryParse(value.ToString(format, CultureInfo.InvariantCulture), NumberStyles.Float, CultureInfo.InvariantCulture, out float r)) value = r;
                    }
                    buffers.Remove(key);
                    typedFrom.Remove(key);
                    typing = false;
                    shown = value.ToString(format, CultureInfo.InvariantCulture);
                    if (enter) DropFocus();
                }
                string text = typing ? b : shown;
                GUI.SetNextControlName(key);
                string typed = GUILayout.TextField(text, Theme.Field, W(80));
                if (typed != text)
                {
                    if (!typedFrom.ContainsKey(key)) typedFrom[key] = shown;
                    buffers[key] = typed;
                }
                GUILayout.Label(unit, Theme.Small, W(unit.Length > 3 ? 70 : 34));   // "px wide" on one line (0.20.149)
                // a row that snaps to steps (a picture's Resolution, Width) draws its thumb under the mouse while it is dragged,
                // the number showing the step it takes (0.20.182, the author: between 712, 640 and 581 px it catches and sticks
                // there): the thumb goes to that step when let go
                if (slidKey == key && slidHot != 0 && GUIUtility.hotControl != slidHot) slidHot = 0;   // let go (0.20.189, review)
                bool dragged = snap != null && slidKey == key && slidHot != 0 && GUIUtility.hotControl == slidHot;
                float onSlider = Mathf.Clamp(dragged ? slidRaw : value, min, max);
                // read before the slider: it uses its mouse events (Event.Use: Used after it), so a drag read after it
                // never showed and the snapped rows still flickered (0.20.176, the author: it still jumps back and forth)
                EventType slideEvent = Event.current.type;
                Vector2 mouse = Event.current.mousePosition;
                // its place taken first: GetLastRect after a used event is a dummy (0.20.178: no press was seen, no slider
                // moved by the mouse); laid out as GUILayout.HorizontalSlider lays it out
                Rect place = GUILayoutUtility.GetRect(SliderSize, Theme.Slider, MinW(60));   // narrow windows too (0.20.74)
                float s = GUI.HorizontalSlider(place, onSlider, min, max, Theme.Slider, Theme.Thumb);
                bool pressed = slideEvent == EventType.MouseDown && place.Contains(mouse);
                if (pressed || slideEvent == EventType.MouseDrag && slidKey == key)
                {
                    // the way the mouse went: on a press from the thumb, in a drag from where it was at the last move
                    if (Math.Abs(s - onSlider) > step / 4) Slid = Math.Sign(pressed ? s - onSlider : s - slidRaw);
                    if (pressed || Slid != 0) slidRaw = s;
                    if (pressed)
                    {
                        slidKey = key;
                        slidHot = GUIUtility.hotControl;                    // the slider took the mouse
                    }
                }
                if (Slid != 0)
                {
                    value = Mathf.Clamp(Mathf.Round(s / step) * step, min, max);
                    buffers.Remove(key);                                    // the slider's value, not a half-typed one (0.20.149)
                    typedFrom.Remove(key);
                    if (editing) DropFocus();
                }
            }
            Event e = Event.current;                                        // Shift + wheel over the row: a fine step a notch
            if (e.type == EventType.ScrollWheel && e.shift && GUI.enabled && // (FineStep, 0.20.93); the plain wheel scrolls as before
                GUILayoutUtility.GetLastRect().Contains(e.mousePosition) && (BodyView == null || BodyView.Value.Contains(e.mousePosition)))
            {
                float wheel = e.delta.y != 0 ? e.delta.y : e.delta.x;
                value = FineStep.Notch(value, wheel, step, format, typedLimit > 0 ? -typedLimit : min, typedLimit > 0 ? typedLimit : max, snap);
                Notched = wheel < 0 ? 1 : wheel > 0 ? -1 : 0;
                e.Use();                                                    // the body under it does not scroll
                buffers.Remove(key);                                        // the notch, not a half-typed number
                typedFrom.Remove(key);
                if (GUI.GetNameOfFocusedControl() == key) DropFocus();
            }
            if (returned.TryGetValue(key, out float before) && Math.Abs(before - value) > 1e-6f) ChangedAt = Time.unscaledTime;
            returned[key] = value;
            return value;
        }

        /// <summary>When a Number row last changed (a key, the slider, the wheel): a setting written to the config waits until
        /// the rows have been still for a moment (0.20.149, review: Shift + wheel wrote the file at every notch).</summary>
        public static float ChangedAt = -10f;

        public static bool StillFor(float seconds) => Time.unscaledTime - ChangedAt > seconds;

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

        public static string Distance(Sign s)
        {
            Player p = Player.m_localPlayer;
            return p && s ? $"{Vector3.Distance(p.transform.position, s.transform.position):0} m away" : "";
        }
    }
}
