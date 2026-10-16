using System.Collections.Generic;
using UnityEngine;

namespace SignLink.UI
{
    /// <summary>A quick view of a pixel picture in the window: one texture, the frame due uploaded when it changes (an
    /// animation plays at its own speed), point-filtered so the pixels stay crisp, empty pixels transparent over the
    /// panel. The RGBA rows are built on a worker (Rows); the main thread only uploads. Pictures over MaxSide a side
    /// are not shown, and an animation keeps at most MaxFrames frames, sampled evenly.</summary>
    internal sealed class Thumb
    {
        public const int MaxSide = 4096, MaxFrames = 256;

        Texture2D tex;
        List<byte[]> frames;
        int shown = -1;
        float fps;
        public string Info = "";
        /// <summary>An animation shows its first frame only (the Art source's Moving off, 0.20.144).</summary>
        public bool Paused;

        public bool Has => tex != null;

        /// <summary>The quick view's texture (a picture's: its grid, bottom-up, point-filtered), or null.</summary>
        public Texture2D Texture => tex;

        /// <summary>RGBA rows bottom-up (Unity's order) of a grid (rows top-down, PixelText.Pack): any thread.</summary>
        public static byte[] Rows(int[] px, int w, int h)
        {
            var raw = new byte[w * h * 4];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    int c = px[y * w + x], o = ((h - 1 - y) * w + x) * 4;
                    if (!SignLink.Pictures.PixelText.Drawn(c)) continue;
                    raw[o] = (byte)(c >> 16);
                    raw[o + 1] = (byte)(c >> 8);
                    raw[o + 2] = (byte)c;
                    raw[o + 3] = (byte)SignLink.Pictures.PixelText.Alpha(c);
                }
            return raw;
        }

        /// <summary>Replaces the picture (main thread) with ready RGBA rows, one array per frame. Everything is made before
        /// the old picture goes, so a failure leaves the old view.</summary>
        public void Set(int w, int h, IList<byte[]> rows, float framesPerSecond)
        {
            if (w <= 0 || h <= 0 || w > MaxSide || h > MaxSide || rows == null || rows.Count == 0)
            {
                Clear();
                Info = $"no quick view: {w} x {h} pixels is too big to show";
                return;
            }
            var kept = new List<byte[]>();
            int step = (rows.Count + MaxFrames - 1) / MaxFrames;             // every step-th frame when there are too many
            for (int i = 0; i < rows.Count; i += step) kept.Add(rows[i]);
            var t = new Texture2D(w, h, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.HideAndDontSave,
            };
            t.LoadRawTextureData(kept[0]);
            bool still = kept.Count == 1;                                   // never uploaded again: no rows, no CPU copy kept
            t.Apply(false, still);                                          // (0.20.132, review; it is only drawn)
            Clear();
            tex = t;
            frames = still ? null : kept;
            shown = 0;
            fps = framesPerSecond / step;
        }

        /// <summary>Shows a ready texture (a preset's render); the thumb owns it from now on.</summary>
        public void SetTexture(Texture2D ready)
        {
            Clear();
            tex = ready;
            frames = null;
            shown = 0;
            fps = 0;
        }

        public void Clear()
        {
            if (tex) Object.Destroy(tex);
            tex = null;
            frames = null;
            shown = -1;
        }

        /// <summary>Draws it centred, as large as fits maxW x maxH (logical units; the height grows with the window),
        /// with Info under it.</summary>
        public void Draw(float maxW, float maxH)
        {
            if (!Has)
            {
                if (Info.Length > 0) Widgets.Hint(Info);
                return;
            }
            using (Widgets.Horizontal(Theme.Section))
            {
                GUILayout.FlexibleSpace();
                DrawSmall(maxW, Widgets.Tall(maxH));
                GUILayout.FlexibleSpace();
            }
            if (Info.Length > 0) Widgets.Hint(Info);
        }

        /// <summary>The quick view's box with words in its middle instead of a picture (an empty sign, one still being
        /// read; 0.20.60, the author: it is not centred): as big as a picture's would be, filled, so the place looks meant and
        /// the world behind a see-through window does not show in it.</summary>
        public static void DrawEmpty(string text, float maxW, float maxH)
        {
            GUILayout.Label(text, Theme.ViewEmpty, Widgets.W(maxW), Widgets.H(maxH));
        }

        /// <summary>The picture alone, as large as fits maxW x maxH (logical units), in the current group (the target
        /// bar's small view).</summary>
        public void DrawSmall(float maxW, float maxH)
        {
            if (!Has) return;
            if (frames != null && frames.Count > 1 && fps > 0)
            {
                int i = Paused ? 0 : (int)(Time.unscaledTime * fps) % frames.Count;
                if (i != shown && Event.current.type == EventType.Repaint)
                {
                    tex.LoadRawTextureData(frames[i]);
                    tex.Apply(false);
                    shown = i;
                }
            }
            float k = Mathf.Min(Widgets.U(maxW) / tex.width, Widgets.U(maxH) / tex.height);
            float dw = tex.width * k, dh = tex.height * k;                  // screen pixels
            // the whole maxW box is taken whatever the picture's shape, so what follows starts at one place; the picture
            // in its middle (0.20.74, the author: the picture centred, not slid to the left)
            Rect box = GUILayoutUtility.GetRect(Widgets.U(maxW), dh, GUILayout.Width(Widgets.U(maxW)), GUILayout.Height(dh));
            GUI.DrawTexture(new Rect(box.x + (box.width - dw) / 2f, box.y, dw, dh), tex, ScaleMode.ScaleToFit);
        }
    }
}
