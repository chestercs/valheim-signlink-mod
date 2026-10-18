using System;

namespace PetyusPolisz.Kit
{
    /// <summary>The window's size in screen pixels (0.20.46, Width and Height apart; the author: it does not set the
    /// height ... even at Very large the mod does not reach the top and bottom of the monitor). Width is a share of the
    /// screen's width, up to all of it (0.20.50, the author: even 100%; it stopped at half the screen before); Height a share of its
    /// height, 1 from the top of the screen to the bottom. Every edge and corner resizes it too (Drag). Width 0 is automatic (0.20.47): as wide as 80 % of the screen's
    /// height, the old Large, the same on 16:9, 21:9 and 32:9 screens. Never smaller than the tabs' rows need at the text
    /// scale; a screen too narrow for them takes smaller text. No Unity here (mod/SignLink.Tests).</summary>
    internal static class WindowSizing
    {
        /// <summary>The narrowest and lowest the tabs' rows are laid out for (logical units).</summary>
        public const float MinWidth = 620f, MinHeight = 560f;

        /// <summary>The shares the Width and Height settings take (Width 0: automatic).</summary>
        public const float WidthLeast = 0.2f, WidthMost = 1f, HeightLeast = 0.4f, HeightMost = 1f;

        /// <summary>The automatic width: 80 % of the screen's height.</summary>
        public const float AutoWidthOfHeight = 0.8f;

        /// <summary>The widest the window gets: the screen.</summary>
        static float Cap(float screenW) => Math.Max(300f, screenW);

        /// <summary>The Width share the window takes: the setting, or the automatic one for 0.</summary>
        public static float WidthShare(float screenW, float screenH, float width) =>
            width > 0f ? width : AutoWidthOfHeight * screenH / Math.Max(1f, screenW);

        /// <summary>The window for a screenW x screenH screen, the Width and Height shares and the text scale: its
        /// pixels (w, h) and the scale the text gets (smaller on a screen too narrow for the rows).</summary>
        public static void Size(float screenW, float screenH, float width, float height, float scale,
                                out float w, out float h, out float s)
        {
            s = scale;
            float cap = Cap(screenW);
            w = Math.Max(Math.Min(Clamp(WidthShare(screenW, screenH, width), WidthLeast, WidthMost) * screenW, cap), MinWidth * s);
            if (w > cap)
            {
                s = Math.Max(0.5f, cap / MinWidth);                         // a screen too narrow for the rows: smaller text
                w = cap;
            }
            h = Math.Min(Math.Max(Clamp(height, HeightLeast, HeightMost) * screenH, MinHeight * s), screenH);
        }

        /// <summary>The edges a drag holds (Drag): a corner is two.</summary>
        public const int Left = 1, Right = 2, Top = 4, Bottom = 8;

        /// <summary>The window (x, y, w, h: screen pixels, from the top left) dragged by (dx, dy) at the edges held: the
        /// opposite edge stays; never past the screen, never smaller than minW x minH (the rows at the text size; rows
        /// wider than the screen get the whole screen).</summary>
        public static void Drag(float x, float y, float w, float h, float dx, float dy, int edges, float minW, float minH,
                                float screenW, float screenH, out float nx, out float ny, out float nw, out float nh)
        {
            nx = x;
            ny = y;
            nw = w;
            nh = h;
            if ((edges & Right) != 0) nw = Math.Min(Math.Max(w + dx, minW), screenW - x);
            else if ((edges & Left) != 0)
            {
                float right = x + w;
                nx = Math.Max(0f, Math.Min(x + dx, right - minW));
                nw = right - nx;
            }
            if ((edges & Bottom) != 0) nh = Math.Min(Math.Max(h + dy, minH), screenH - y);
            else if ((edges & Top) != 0)
            {
                float bottom = y + h;
                ny = Math.Max(0f, Math.Min(y + dy, bottom - minH));
                nh = bottom - ny;
            }
        }

        public static float Clamp(float v, float least, float most) => float.IsNaN(v) ? least : Math.Max(least, Math.Min(most, v));

        /// <summary>The body's least height (SignLinkWindow.BodyLeast): while steady (a control held or typed in) the height
        /// it had (last); scrolled down, never shorter than what the scroll shows, so content that got shorter at its end
        /// does not pull the scroll back (0.20.84), but never taller than it was either (0.20.111, the author: in 3 Place
        /// I can scroll down forever and empty content waits there: the scroll's own view is a little shorter than
        /// its rect, so every notch made room for the next).</summary>
        public static float BodyLeast(float scrollY, float viewHeight, float last, bool steady) =>
            Math.Max(steady ? last : 0f, scrollY > 0.5f ? Math.Min(scrollY + viewHeight, last) : 0f);
    }
}
