using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace PetyusPolisz.Kit
{
    /// <summary>Small IMGUI building blocks with the window's look (Theme). Every size in the UI is written in logical
    /// units (the window as designed at 1080p) and goes through U to the screen: the text is drawn at its own pixel
    /// size (Theme.Ensure), not magnified through a matrix, so it stays crisp at 1440p and 4K.</summary>
    internal static partial class Widgets
    {
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
    }
}
