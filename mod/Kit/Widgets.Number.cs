using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace PetyusPolisz.Kit
{
    /// <summary>Number rows: a label, a typed field and a slider, Shift + wheel a fine step (FineStep); the size rows.</summary>
    internal static partial class Widgets
    {
        static readonly Dictionary<string, string> buffers = new Dictionary<string, string>(), typedFrom = new Dictionary<string, string>();
        static readonly Dictionary<string, float> returned = new Dictionary<string, float>();

        /// <summary>A selectable list row (amber when selected); true when clicked while not selected.</summary>
        /// <summary>A piece's size: Width and Height (same shape) in metres when its size as made is known (naturalM), else
        /// a percentage; returns the percentage, 25..400 (the Size rows of a source and 3 Place's content rows). scale: the
        /// size a scale really takes (a pixel piece's exact line heights), so Shift + wheel goes to the next one (0.20.98).</summary>
        public static float SizeRows(string key, float pct, Vector2 naturalM, Func<double, double> scale = null)
        {
            float was = pct;
            float got = SizeRowsRaw(key, pct, naturalM, scale, out bool dragged);
            if (scale == null) return got;
            // the slider and a typed size go to the sizes the piece really takes too, not only Shift + wheel, so the row does
            // not jump back after the put (0.20.149, review); a drag to the size nearest the mouse (FineStep.Settle)
            return FineStep.Settle(was, got, p => (float)(scale(p / 100.0) * 100), dragged, 5f, 25f, 400f);
        }

        /// <summary>dragged: a row's slider moved by the mouse in this event (Slid, read after each row).</summary>
        static float SizeRowsRaw(string key, float pct, Vector2 naturalM, Func<double, double> scale, out bool dragged)
        {
            if (!(naturalM.x > 0 && naturalM.y > 0))
            {
                float size = Number(key + ".size", "Size", pct, 25f, 400f, 5f, "%", "0.#", snap: scale == null ? null : (Func<float, float>)(p => (float)(scale(p / 100.0) * 100)));
                dragged = Slid != 0;
                return size;
            }
            float stepW = Mathf.Clamp(naturalM.x / 20f, 0.01f, 0.05f), stepH = Mathf.Clamp(naturalM.y / 20f, 0.01f, 0.05f);   // about 5 % a step (0.20.149)
            float w0 = pct / 100f * naturalM.x;
            float wide = Number(key + ".width", "Width", w0, naturalM.x / 4, naturalM.x * 4, stepW, "m",
                                snap: scale == null ? null : (Func<float, float>)(w => (float)(scale(w / naturalM.x) * naturalM.x)));
            dragged = Slid != 0;
            if (Math.Abs(wide - w0) > 1e-4f) pct = Mathf.Clamp(wide / naturalM.x * 100f, 25f, 400f);
            float h0 = pct / 100f * naturalM.y;
            float h = Number(key + ".height", "Height (same shape)", h0, naturalM.y / 4, naturalM.y * 4, stepH, "m",
                             snap: scale == null ? null : (Func<float, float>)(v => (float)(scale(v / naturalM.y) * naturalM.y)));
            dragged |= Slid != 0;
            if (Math.Abs(h - h0) > 1e-4f) pct = Mathf.Clamp(h / naturalM.y * 100f, 25f, 400f);
            return pct;
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
    }
}
