using System.Reflection;
using TMPro;
using UnityEngine;

namespace SignLink.UI
{
    /// <summary>Where a text's glyphs draw, in its own local units (0.20.155, the author: are the layers really shown right
    /// for texts?): the union of its visible characters' quads, each without the SDF padding TMP lays around a glyph,
    /// and without the invisible ones (alpha 0: the anchors that keep a stack's layers in order, engine.build_planes). TMP's
    /// own textBounds counts line extents: a Label template's sign measured 1.10 x 0.73 m where its glyphs cover 0.90 x 0.40 m,
    /// and the Dragon crest 3.2 m wide off to one side where it covers 4.1 m.</summary>
    internal static class Ink
    {
        static readonly FieldInfo PaddingField = typeof(TMP_Text).GetField("m_padding", BindingFlags.Instance | BindingFlags.NonPublic);
        const char Block = '■';                                             // GlyphData.Block: every ■ comes from NotoSansJP-Thin

        /// <summary>False while the text has no laid-out glyph that shows (a sign just loaded: measured at a later look).</summary>
        public static bool Rect(TMP_Text t, out Vector2 min, out Vector2 max)
        {
            min = new Vector2(float.MaxValue, float.MaxValue);
            max = new Vector2(float.MinValue, float.MinValue);
            TMP_TextInfo info = t ? t.textInfo : null;
            if (info == null || info.characterInfo == null) return false;
            float pad = Padding(t);
            bool any = false;
            int n = Mathf.Min(info.characterCount, info.characterInfo.Length);
            for (int i = 0; i < n; i++)
            {
                if (!Quad(ref info.characterInfo[i], pad, out Vector3 bl, out Vector3 br, out Vector3 tl, out Vector3 tr)) continue;
                Add(bl, ref min, ref max);
                Add(br, ref min, ref max);
                Add(tl, ref min, ref max);
                Add(tr, ref min, ref max);
                any = true;
            }
            return any && max.x > min.x && max.y > min.y;
        }

        /// <summary>Is this point (the text's local units) on one of its shown glyphs, each glyph's quad as Rect counts it
        /// (1.1.0, the author: the cat art hid the sign behind it)? A glyph art's rectangle is mostly empty, and a click on
        /// the empty part belongs to what lies behind. Each quad by its bounding box (a turned glyph a little larger).</summary>
        public static bool Covers(TMP_Text t, Vector2 p)
        {
            TMP_TextInfo info = t ? t.textInfo : null;
            if (info == null || info.characterInfo == null) return false;
            float pad = Padding(t);
            int n = Mathf.Min(info.characterCount, info.characterInfo.Length);
            for (int i = 0; i < n; i++)
            {
                if (!Quad(ref info.characterInfo[i], pad, out Vector3 bl, out Vector3 br, out Vector3 tl, out Vector3 tr)) continue;
                float x0 = Mathf.Min(Mathf.Min(bl.x, br.x), Mathf.Min(tl.x, tr.x)), x1 = Mathf.Max(Mathf.Max(bl.x, br.x), Mathf.Max(tl.x, tr.x));
                float y0 = Mathf.Min(Mathf.Min(bl.y, br.y), Mathf.Min(tl.y, tr.y)), y1 = Mathf.Max(Mathf.Max(bl.y, br.y), Mathf.Max(tl.y, tr.y));
                float gx = (x1 - x0) * 0.15f, gy = (y1 - y0) * 0.15f;     // a picture's pixels touch: no click between two
                if (p.x >= x0 - gx && p.x <= x1 + gx && p.y >= y0 - gy && p.y <= y1 + gy) return true;
            }
            return false;
        }

        static float Padding(TMP_Text t)
        {
            try { return PaddingField != null ? (float)PaddingField.GetValue(t) : 0f; }
            catch (System.Exception) { return 0f; }
        }

        /// <summary>A shown character's quad without its SDF padding (the block's measured ink); false for one that does not
        /// show (invisible, or alpha 0).</summary>
        static bool Quad(ref TMP_CharacterInfo c, float pad, out Vector3 bl, out Vector3 br, out Vector3 tl, out Vector3 tr)
        {
            bl = br = tl = tr = Vector3.zero;
            if (!c.isVisible || c.color.a == 0) return false;
            Vector3 qbl = c.vertex_BL.position, qtl = c.vertex_TL.position, qtr = c.vertex_TR.position, qbr = c.vertex_BR.position;
            Vector3 across = qbr - qbl, up = qtl - qbl;
            float inset = pad * Mathf.Abs(c.scale);
            // the padding in the glyph's own axes (a <rotate> turns the quad); a quad smaller than its padding: its middle
            Vector3 ix = across.magnitude > 2 * inset ? across.normalized * inset : across / 2;
            Vector3 iy = up.magnitude > 2 * inset ? up.normalized * inset : up / 2;
            if (c.character == Block)                                       // the block's measured ink (1.0.2, the author: the
            {                                                               // frame stood off a QR code on every side)
                Pictures.Snippets.BlockInkOfQuad(across.magnitude, up.magnitude, out double w, out double h);
                ix = across.normalized * Mathf.Clamp((across.magnitude - (float)w) / 2, 0, across.magnitude / 2);
                iy = up.normalized * Mathf.Clamp((up.magnitude - (float)h) / 2, 0, up.magnitude / 2);
            }
            bl = qbl + ix + iy;
            br = qbr - ix + iy;
            tl = qtl + ix - iy;
            tr = qtr - ix - iy;
            return true;
        }

        static void Add(Vector3 p, ref Vector2 min, ref Vector2 max)
        {
            if (p.x < min.x) min.x = p.x;
            if (p.y < min.y) min.y = p.y;
            if (p.x > max.x) max.x = p.x;
            if (p.y > max.y) max.y = p.y;
        }
    }
}
