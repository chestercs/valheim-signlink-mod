using System;
using System.Collections.Generic;
using System.Globalization;
using SignLink.Pictures;
using UnityEngine;

namespace SignLink.UI
{
    /// <summary>The template strip of the Text source (Block · Label · Symbol · Pattern) and the picked template's
    /// rows: sizes in metres, a colour (swatches and a hex field) with an opacity, and Insert, which hands the lines
    /// (Snippets) to the source; the live preview shows the result (ContentPreview). Simple on purpose: no
    /// layers, no drag on the sign; the text is the truth, and 3 Place moves it as a whole.</summary>
    internal sealed class TemplateRows
    {
        static readonly string[] Names = { "Block", "Label", "Symbol", "Pattern" };
        /// <summary>White, the sign's own dark brown, red, orange, yellow, green, cyan, blue, purple, pink, brown, grey.</summary>
        static readonly int[] Swatches = { 0xFFFFFF, 0x1E1A12, 0xD92B2B, 0xF08A24, 0xF2D43D, 0x4CAF50, 0x2BB5C9, 0x2F6FD9,
                                           0x8E44AD, 0xE86FA6, 0x8B5A2B, 0x8A8A8A };
        const int MaxCells = 16;

        /// <summary>Palette buttons on a row: as many as the body is wide (12 overflowed the small window).</summary>
        static int PerRow => Math.Max(4, (int)((Widgets.ContentWidth - Widgets.LabelW - 10) / 44));

        sealed class Colour
        {
            public int Rgb;
            public float Alpha = 100f;
            public string Hex;                                              // the field's buffer while it is being typed
        }

        int picked = -1;
        float wM = 1f, hM = 0.5f;                                           // Block
        string label = "Skål";                                              // Label
        float letterM = 0.3f;
        int group;                                                          // Symbol
        string symbol = "★";
        float symbolM = 0.5f, rotate;
        bool mirror;
        int patternGlyph, cols = 4, rows = 4;                               // Pattern
        float cellCm = 25f;
        bool alternate = true;
        readonly Colour a = new Colour { Rgb = 0xD92B2B }, b = new Colour { Rgb = 0xF2D43D };

        /// <summary>Draws the strip and the picked template's rows. can: a sign is selected and editable. Returns the
        /// lines to insert when Insert was clicked, else null.</summary>
        public List<Snippets.Line> Draw(SignLinkWindow w, bool can)
        {
            using (Widgets.Horizontal())
            {
                GUILayout.Label("Templates", Theme.Label, Widgets.W(Widgets.LabelW));
                float each = Widgets.RestW / Names.Length;
                for (int i = 0; i < Names.Length; i++)                      // toggles, so the lit one closes on a click
                {
                    bool on = Widgets.Toggle(picked == i, Names[i], Theme.Chip, Widgets.W(each));   // small (0.20.85)
                    int pick = i;
                    if (on != (picked == i)) w.Later(() => picked = picked == pick ? -1 : pick);   // the rows change: at a Layout
                }
            }
            if (picked < 0)
            {
                Widgets.Hint("A ready text in a click: a coloured block to paint a wall, a label, a symbol, a repeating pattern. The sign shows it as you go.");
                return null;
            }
            List<Snippets.Line> insert = null;
            using (Widgets.Vertical(Theme.Section))
            {
                switch (picked)
                {
                    case 0:
                        wM = Widgets.Number("tpl.w", "Width", wM, 0.05f, 30f, 0.05f, "m");
                        hM = Widgets.Number("tpl.h", "Height", hM, 0.05f, 30f, 0.05f, "m");
                        ColourRows(w, "Colour", a, "tpl.a");
                        break;
                    case 1:
                        using (Widgets.Horizontal())
                        {
                            GUILayout.Label("Text", Theme.Label, Widgets.W(Widgets.LabelW));
                            label = GUILayout.TextField(label, 200, Theme.Field, Widgets.W(Widgets.RestW));
                        }
                        letterM = Widgets.Number("tpl.letter", "Letter height", letterM, 0.05f, 10f, 0.05f, "m");
                        ColourRows(w, "Colour", a, "tpl.a");
                        break;
                    case 2:
                        SymbolRows(w);
                        break;
                    case 3:
                        using (Widgets.Horizontal())
                        {
                            GUILayout.Label("Glyph", Theme.Label, Widgets.W(Widgets.LabelW));
                            int g = Widgets.Toolbar(patternGlyph, GlyphData.PatternGlyphs, Theme.Chip, Widgets.W(Widgets.RestW));
                            if (g != patternGlyph) w.Later(() => patternGlyph = g);
                        }
                        cols = (int)Widgets.Number("tpl.cols", "Columns", cols, 1f, MaxCells, 1f, "", "0");
                        rows = (int)Widgets.Number("tpl.rows", "Rows", rows, 1f, MaxCells, 1f, "", "0");
                        cellCm = Widgets.Number("tpl.cell", "Cell", cellCm, 2f, 200f, 1f, "cm", "0");
                        ColourRows(w, "Colour A", a, "tpl.a");
                        ColourRows(w, "Colour B", b, "tpl.b");
                        alternate = Widgets.Check(alternate, "Alternate the two colours like a chessboard");
                        break;
                }
                using (Widgets.Horizontal())
                {
                    GUILayout.Label("", Theme.Label, Widgets.W(Widgets.LabelW));
                    bool known = picked != 2 || GlyphData.Find(symbol) != null;
                    bool was = GUI.enabled;                                 // the caller's state comes back (the text box below)
                    GUI.enabled = was && can && known;
                    if (Widgets.Button("Insert", Theme.SmallButton, Widgets.W(110))) insert = Lines();
                    GUI.enabled = was;
                    GUILayout.Label(Describe(), Theme.Small);
                }
            }
            return insert;
        }

        void SymbolRows(SignLinkWindow w)
        {
            using (Widgets.Horizontal())
            {
                GUILayout.Label("Palette", Theme.Label, Widgets.W(Widgets.LabelW));
                int g = Widgets.Toolbar(group, GlyphData.GroupNames, Theme.Chip, Widgets.W(Widgets.RestW));
                if (g != group) w.Later(() => group = g);
            }
            string[] glyphs = GlyphData.Groups[Mathf.Clamp(group, 0, GlyphData.Groups.Length - 1)];
            for (int i = 0; i < glyphs.Length; i += PerRow)
                using (Widgets.Horizontal())
                {
                    GUILayout.Label("", Theme.Label, Widgets.W(Widgets.LabelW));
                    for (int j = i; j < Math.Min(glyphs.Length, i + PerRow); j++)
                    {
                        string ch = glyphs[j];
                        if (Widgets.Toggle(symbol == ch, ch, Theme.Chip, Widgets.W(40), Widgets.H(30)) && symbol != ch) w.Later(() => symbol = ch);
                    }
                }
            GlyphData.Glyph found = GlyphData.Find(symbol);
            Widgets.Hint(found != null ? $"{symbol}  {found.Name}" : "Pick a symbol above.");
            symbolM = Widgets.Number("tpl.symbol", "Size", symbolM, 0.05f, 30f, 0.05f, "m");
            rotate = Widgets.Number("tpl.rotate", "Rotate", rotate, -180f, 180f, 15f, "°", "0");
            mirror = Widgets.Check(mirror, "Mirror (flip left to right)");
            ColourRows(w, "Colour", a, "tpl.a");
        }

        /// <summary>The swatches, a hex field and the opacity of one colour.</summary>
        static void ColourRows(SignLinkWindow w, string label, Colour c, string key)
        {
            using (Widgets.Horizontal())
            {
                GUILayout.Label(label, Theme.Label, Widgets.W(Widgets.LabelW));
                float side = Mathf.Min(26f, (Widgets.RestW - 100f) / Swatches.Length);   // the hex field fits beside them
                foreach (int rgb in Swatches)
                {
                    Rect r = GUILayoutUtility.GetRect(Widgets.U(side), Widgets.U(26), Widgets.W(side), Widgets.H(26));
                    if (Event.current.type == EventType.Repaint)
                    {
                        Color was = GUI.color;
                        if (c.Rgb == rgb)
                        {
                            GUI.color = Color.white;
                            GUI.DrawTexture(new Rect(r.x - 2, r.y - 2, r.width + 4, r.height + 4), Texture2D.whiteTexture);
                        }
                        GUI.color = Of(rgb);
                        GUI.DrawTexture(new Rect(r.x + 1, r.y + 1, r.width - 2, r.height - 2), Texture2D.whiteTexture);
                        GUI.color = was;
                    }
                    if (GUI.Button(r, GUIContent.none, GUIStyle.none)) w.Later(() => { c.Rgb = rgb; c.Hex = null; });
                }
                string shown = c.Hex ?? Hex(c.Rgb);
                string typed = GUILayout.TextField(shown, 7, Theme.Field, Widgets.W(90));
                if (typed != shown)
                {
                    c.Hex = typed;
                    if (TryHex(typed, out int v)) c.Rgb = v;
                }
            }
            c.Alpha = Widgets.Number(key + ".alpha", "Opacity", c.Alpha, 10f, 100f, 5f, "%", "0");
        }

        static Color Of(int rgb) => new Color(((rgb >> 16) & 255) / 255f, ((rgb >> 8) & 255) / 255f, (rgb & 255) / 255f, 1f);

        static string Hex(int rgb) => "#" + rgb.ToString("x6", CultureInfo.InvariantCulture);

        static bool TryHex(string s, out int rgb)
        {
            rgb = 0;
            s = (s ?? "").Trim().TrimStart('#');
            if (s.Length == 3) s = "" + s[0] + s[0] + s[1] + s[1] + s[2] + s[2];
            return s.Length == 6 && int.TryParse(s, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out rgb);
        }

        static int A(Colour c) => Mathf.Clamp(Mathf.RoundToInt(c.Alpha * 2.55f), 1, 255);

        List<Snippets.Line> Lines()
        {
            switch (picked)
            {
                case 0: return Snippets.Block(wM, hM, a.Rgb, A(a));
                case 1: return Snippets.Label(label, letterM, a.Rgb, A(a));
                case 2: return Snippets.Symbol(symbol, symbolM, a.Rgb, A(a), mirror, rotate);
                case 3: return Snippets.Pattern(GlyphData.PatternGlyphs[Mathf.Clamp(patternGlyph, 0, GlyphData.PatternGlyphs.Length - 1)],
                                                cols, rows, cellCm / 100f, a.Rgb, A(a), b.Rgb, A(b), alternate);
                default: return null;
            }
        }

        string Describe()
        {
            string op = a.Alpha < 99.5f ? $", {a.Alpha:0} %" : "";
            switch (picked)
            {
                case 0: return $"a {wM:0.##} x {hM:0.##} m block, {Hex(a.Rgb)}{op}, in the sign's middle";
                case 1: return $"'{label}' with {letterM * 100:0} cm capitals, {Hex(a.Rgb)}{op}, in the middle";
                case 2: return $"{symbol} {symbolM * 100:0} cm tall, {Hex(a.Rgb)}{op}{(rotate != 0 ? $", rotated {rotate:0}°" : "")}{(mirror ? ", mirrored" : "")}";
                case 3: return $"{cols} x {rows} cells of {cellCm:0} cm ({cols * cellCm / 100:0.##} x {rows * cellCm / 100:0.##} m), {Hex(a.Rgb)}" +
                               (alternate ? $" and {Hex(b.Rgb)}" : "") + op;
                default: return "";
            }
        }
    }
}
