using System.Collections.Generic;
using UnityEngine;

namespace SignLink.UI
{
    /// <summary>While the live preview shows the result (ContentPreview, 0.20.129), or 2 Content's pick is a picture or a
    /// piece other than the one the selected sign holds (Change.HidesHeld), the sign's stack shows nothing on this
    /// computer: its texts switched off and a moving GIF's screen hidden (ScreenPlayer.Veil). Only the preview, or the new
    /// pick's frame, shows (0.20.95, the author: do not show the existing picture while editing once I pick new content).
    /// Anything else (nothing new, another sign, the window closed) shows the sign as it is again. The signs' text
    /// canvases are switched off (0.20.130; 0.20.95-0.20.128 emptied the texts and built the saved text anew to show it,
    /// about a second for a big picture; 0.20.129 switched the texts off, whose meshes TMP builds again when they come
    /// back on): a canvas keeps its meshes, hides a GIF's screen with it, and the game never touches it. The Session
    /// reads the saved texts, so this changes no choice.</summary>
    internal sealed class HeldVeil
    {
        List<Sign> veiled;
        /// <summary>The veiled signs' text canvases, found when the veil starts (0.20.132, review: looked for up the tree
        /// for every sign of the stack every Layout while the preview showed).</summary>
        readonly List<Canvas> canvases = new List<Canvas>();

        /// <summary>Layout events only, after the source's Tick; nothing changes while a put runs (busy: it writes the
        /// stack, and the session reads the new content after it).</summary>
        public void Follow(Session s, bool busy, bool previewing)
        {
            if (busy) return;
            bool want = s.HasTarget && (previewing || Change.HidesHeld(s.Held, s.Chosen));
            List<Sign> stack = want ? (s.Stack.Count > 0 ? s.Stack : new List<Sign> { s.Target }) : null;
            if (veiled != null && stack != null && Same(veiled, stack))
            {
                Off();                                                      // a screen stopped meanwhile switches its texts on
                return;
            }
            End();
            if (stack == null) return;
            veiled = new List<Sign>(stack);
            foreach (Sign o in veiled) canvases.Add(CanvasOf(o));
            Off();
        }

        void Off()
        {
            foreach (Canvas c in canvases)
                if (c && c.enabled) c.enabled = false;
            Plugin.Screens.Veil(veiled, true);                              // one restored meanwhile is hidden too (and uploads nothing)
        }

        static Canvas CanvasOf(Sign o) => o && o.m_textWidget ? o.m_textWidget.GetComponentInParent<Canvas>() : null;

        /// <summary>The sign shows as it is again.</summary>
        public void End()
        {
            if (veiled == null) return;
            foreach (Canvas c in canvases)
                if (c && !c.enabled) c.enabled = true;
            Plugin.Screens.Veil(veiled, false);
            canvases.Clear();
            veiled = null;
        }

        static bool Same(List<Sign> a, List<Sign> b)
        {
            if (a.Count != b.Count) return false;
            for (int i = 0; i < a.Count; i++)
                if (!ReferenceEquals(a[i], b[i])) return false;
            return true;
        }
    }
}
