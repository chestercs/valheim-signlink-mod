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
            float pad = 0f;
            try { if (PaddingField != null) pad = (float)PaddingField.GetValue(t); }
            catch (System.Exception) { pad = 0f; }
            bool any = false;
            int n = Mathf.Min(info.characterCount, info.characterInfo.Length);
            for (int i = 0; i < n; i++)
            {
                TMP_CharacterInfo c = info.characterInfo[i];
                if (!c.isVisible || c.color.a == 0) continue;
                Vector3 bl = c.vertex_BL.position, tl = c.vertex_TL.position, tr = c.vertex_TR.position, br = c.vertex_BR.position;
                Vector3 across = br - bl, up = tl - bl;
                float inset = pad * Mathf.Abs(c.scale);
                // the padding in the glyph's own axes (a <rotate> turns the quad); a quad smaller than its padding: its middle
                Vector3 ix = across.magnitude > 2 * inset ? across.normalized * inset : across / 2;
                Vector3 iy = up.magnitude > 2 * inset ? up.normalized * inset : up / 2;
                if (c.character == Block)                                   // the block's measured ink (1.0.2, the author: the
                {                                                           // frame stood off a QR code on every side)
                    Pictures.Snippets.BlockInkOfQuad(across.magnitude, up.magnitude, out double w, out double h);
                    ix = across.normalized * Mathf.Clamp((across.magnitude - (float)w) / 2, 0, across.magnitude / 2);
                    iy = up.normalized * Mathf.Clamp((up.magnitude - (float)h) / 2, 0, up.magnitude / 2);
                }
                Add(bl + ix + iy, ref min, ref max);
                Add(br - ix + iy, ref min, ref max);
                Add(tl + ix - iy, ref min, ref max);
                Add(tr - ix - iy, ref min, ref max);
                any = true;
            }
            return any && max.x > min.x && max.y > min.y;
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
