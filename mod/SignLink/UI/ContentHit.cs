using System.Collections.Generic;
using SignLink.Signs;
using TMPro;
using UnityEngine;

namespace SignLink.UI
{
    /// <summary>Where a ray meets what the loaded signs show (1.1.0, the author: Pick in view did nothing on a picture): the
    /// nearest sign whose glyphs' rectangle (Ink) the ray crosses before `reach`. A picture need not sit on its board (its
    /// content moved up, a sign tilted into the ground, a board under a floor), and only the board has a collider, so a
    /// click on the picture itself found no sign. A moving GIF counts as its still (the screen hides the text widget, whose
    /// laid-out glyphs stay). The rectangles are kept per sign until its text changes (Ink.Rect walks every glyph), and
    /// TMP's own mesh bounds rule out first the signs the ray misses by far.</summary>
    internal static class ContentHit
    {
        sealed class Kept
        {
            public string Text;
            public bool Measured;
            public float TriedAt = -100f;
            public Vector2 Lo, Hi;
        }

        static readonly Dictionary<Sign, Kept> kept = new Dictionary<Sign, Kept>();
        static int seen;

        /// <summary>Drops the measures (the window closed): the signs and texts they hold are not kept alive while the player
        /// travels with the window shut (1.1.0, review: Prune runs only with a pick).</summary>
        public static void Forget() => kept.Clear();

        /// <summary>A text not laid out yet (just loaded) is measured again at most this often.</summary>
        const float RetryEvery = 1f;

        /// <summary>The sign whose shown content the ray from origin along direction (a unit vector) meets first, nearer than
        /// reach metres; distance: how far along. Null: none.</summary>
        public static Sign Nearest(Vector3 origin, Vector3 direction, float reach, out float distance)
        {
            SignFinder.Prune(kept, ref seen);
            Sign best = null;
            distance = reach;
            float now = Time.unscaledTime;
            foreach (Sign s in SignFinder.Loaded())
            {
                TMP_Text t = s ? s.m_textWidget : null;
                if (!t || !t.gameObject.activeInHierarchy || string.IsNullOrEmpty(t.text)) continue;
                // a moving GIF: its screen (ScreenPlayer's RawImage under the text) is what shows; the text it hides was
                // often never laid out, so its ink cannot be measured (the author: a GIF hardly took a click)
                if (t.transform.Find("SignLinkScreen") is RectTransform screen && screen.gameObject.activeInHierarchy)
                {
                    if (Meets(screen, origin, direction, distance, out float at, out Vector3 q) && screen.rect.Contains(q))
                    {
                        best = s;
                        distance = at;
                    }
                    continue;
                }
                if (!Meets(t.transform, origin, direction, distance, out float along, out Vector3 p)) continue;
                Bounds mesh = t.bounds;                                     // every glyph's quad: more than the ink, a quick no;
                bool inMesh = mesh.extents.x <= 0f ||                       // none kept (a text a screen hides): its ink decides
                              Mathf.Abs(p.x - mesh.center.x) <= mesh.extents.x && Mathf.Abs(p.y - mesh.center.y) <= mesh.extents.y;
                if (!inMesh) continue;
                if (!kept.TryGetValue(s, out Kept k)) kept[s] = k = new Kept();
                if (!ReferenceEquals(k.Text, t.text) || (!k.Measured && now - k.TriedAt >= RetryEvery))
                {
                    k.Text = t.text;
                    k.TriedAt = now;
                    k.Measured = Ink.Rect(t, out k.Lo, out k.Hi);
                }
                if (!k.Measured || p.x < k.Lo.x || p.x > k.Hi.x || p.y < k.Lo.y || p.y > k.Hi.y) continue;
                if (!Ink.Covers(t, p)) continue;                            // the empty part of an art: what lies behind takes it
                best = s;
                distance = along;
            }
            return best;
        }

        /// <summary>Where the ray meets the plane z = 0 of this transform, nearer than reach metres: how far along (direction is
        /// a unit vector) and the point in its local units.</summary>
        static bool Meets(Transform tr, Vector3 origin, Vector3 direction, float reach, out float along, out Vector3 point)
        {
            Vector3 a = tr.InverseTransformPoint(origin), b = tr.InverseTransformPoint(origin + direction);
            float dz = b.z - a.z;
            along = 0f;
            point = Vector3.zero;
            if (Mathf.Abs(dz) < 1e-6f) return false;                         // the ray runs along the plane
            along = -a.z / dz;
            if (along <= 0f || along >= reach) return false;
            point = a + (b - a) * along;
            return true;
        }
    }
}
