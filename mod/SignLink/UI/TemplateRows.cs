using System;
using System.Collections.Generic;
using System.Globalization;
using SignLink.Pictures;
using UnityEngine;
using PetyusPolisz.Kit;

namespace SignLink.UI
{
    /// <summary>The template strip of the Text source (Block · Label · Symbol · Pattern · QR) and the picked template's
    /// rows: sizes in metres, a colour (swatches and a hex field) with an opacity, and what Insert puts in. Insert is the
    /// editor's (TextSource, over the box: Problem says while it is greyed, Lines gives the lines, Snippets); the live
    /// preview shows the result (ContentPreview). Simple on purpose: no layers, no drag on the sign; the text is the
    /// truth, and 3 Place moves it as a whole.</summary>
    internal sealed class TemplateRows
    {
        static readonly string[] Names = { "Block", "Label", "Symbol", "Pattern", "QR" };
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
        string qrText = "";                                                 // QR (2026-10-16, the author: a QR code
        float qrM = 1f;                                                     // generator in the content menu, with
        int qrLevel = 1;                                                    // Unicode squares)
        readonly Colour qrDark = new Colour { Rgb = 0x000000 }, qrLight = new Colour { Rgb = 0xFFFFFF };
        static readonly string[] Levels = { "L  7 %", "M  15 %", "Q  25 %", "H  30 %" };

        /// <summary>The QR code as last made, kept while its text, level, size and colours stay (a code is made once, not
        /// at every GUI event): its lines, how many characters they take on the sign, or why there is none.</summary>
        string qrFor;
        List<Snippets.Line> qrLines;
        int qrChars;
        string qrWhy, qrWhat = "";

        void MakeQr()
        {
            string key = $"{qrText}|{qrLevel}|{qrM}|{qrDark.Rgb}|{qrLight.Rgb}";
            if (key == qrFor) return;
            qrFor = key;
            qrLines = null;
            qrWhy = null;
            if (qrText.Length == 0)
            {
                qrWhy = "Type a link";
                return;
            }
            try
            {
                QrCode q = QrCode.Encode(qrText, (QrCode.Ecc)qrLevel);
                qrLines = Snippets.Qr(q.Modules, qrM, qrDark.Rgb, qrLight.Rgb);
                qrChars = Snippets.Compose(qrLines).Length;
                qrWhat = $"{q.Size} x {q.Size}, version {q.Version}, {qrChars / 1000.0:0.#} k characters";
            }
            catch (ArgumentException e)
            {
                qrWhy = e.Message;
            }
        }

        /// <summary>Draws the strip and the picked template's rows, with what Insert puts in under them (Describe), or
        /// why it cannot yet (Problem, in red). room: what is left of the text box (a QR code must fit it).</summary>
        public void Draw(SignLinkWindow w, int room = int.MaxValue)
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
            if (picked < 0) return;
            using (Widgets.Vertical(Theme.Section))
            {
                using (Widgets.Horizontal()) Widgets.Keys(Widgets.WheelKey);   // its sliders' fine step (1.0.2: keys shown)
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
                        {
                            int g = Widgets.Choice("Glyph", patternGlyph, GlyphData.PatternGlyphs);
                            if (g != patternGlyph) w.Later(() => patternGlyph = g);
                        }
                        cols = (int)Widgets.Number("tpl.cols", "Columns", cols, 1f, MaxCells, 1f, "", "0");
                        rows = (int)Widgets.Number("tpl.rows", "Rows", rows, 1f, MaxCells, 1f, "", "0");
                        cellCm = Widgets.Number("tpl.cell", "Cell", cellCm, 2f, 200f, 1f, "cm", "0");
                        ColourRows(w, "Colour A", a, "tpl.a");
                        ColourRows(w, "Colour B", b, "tpl.b");
                        alternate = Widgets.Check(alternate, "Chessboard");
                        break;
                    case 4:
                        QrRows(w);
                        break;
                }
                string why = Problem(room);
                using (Widgets.Horizontal())
                {
                    GUILayout.Label("", Theme.Label, Widgets.W(Widgets.LabelW));
                    GUILayout.Label(why ?? Describe(), why != null ? Theme.Warn : Theme.Small);
                }
            }
        }

        /// <summary>Why nothing can go in now, or null: no template picked, no symbol of that name, or the QR code's
        /// problem (QrProblem).</summary>
        public string Problem(int room)
        {
            if (picked < 0) return "Pick a template";
            if (picked == 2 && GlyphData.Find(symbol) == null) return "Pick a symbol";
            return picked == 4 ? QrProblem(room) : TooLong(room);
        }

        /// <summary>A block, a label, a symbol or a pattern over what the box has room for (review: the box cut the end of
        /// the text off); its characters counted once per settings (a pattern of 16 x 16 cells is 10 KB).</summary>
        string TooLong(int room)
        {
            string key = Describe();
            if (key != longFor)
            {
                longFor = key;
                List<Snippets.Line> lines = Lines();
                longChars = lines == null ? 0 : Snippets.Compose(lines).Length;
            }
            return longChars > room ? $"Too long: {longChars / 1000.0:0.#} k characters, {Math.Max(0, room) / 1000.0:0.#} k left" : null;
        }
        string longFor;
        int longChars;

        /// <summary>The QR template's rows: the text (a link, mostly), the error correction, the size, the two colours.</summary>
        void QrRows(SignLinkWindow w)
        {
            using (Widgets.Horizontal())
            {
                GUILayout.Label("Link or text", Theme.Label, Widgets.W(Widgets.LabelW));
                qrText = GUILayout.TextField(qrText, 1000, Theme.Field, Widgets.W(Widgets.RestW));
            }
            int l = Widgets.Choice("Error correction", qrLevel, Levels);
            if (l != qrLevel) w.Later(() => qrLevel = l);
            qrM = Widgets.Number("tpl.qr", "Size", qrM, 0.2f, 10f, 0.05f, "m");
            ColourRows(w, "Dark", qrDark, "tpl.qrdark", opacity: false);
            ColourRows(w, "Light", qrLight, "tpl.qrlight", opacity: false);
            if (Event.current.type == EventType.Layout) MakeQr();
        }

        /// <summary>Why the QR code cannot go in now, or null: no text, too long for a QR code, or more characters than the
        /// text box has room for (room: what is left of TextSource.MaxChars).</summary>
        string QrProblem(int room)
        {
            if (qrWhy != null) return qrWhy;
            if (qrLines == null) return "Making the code ...";
            if (qrChars > room)
                return $"Too long: {qrChars / 1000.0:0.#} k characters, {Math.Max(0, room) / 1000.0:0.#} k left";
            return null;
        }

        void SymbolRows(SignLinkWindow w)
        {
            int g = Widgets.Choice("Palette", group, GlyphData.GroupNames);
            if (g != group) w.Later(() => group = g);
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
            Widgets.Hint(found != null ? $"{symbol}  {found.Name}" : "Pick one");
            symbolM = Widgets.Number("tpl.symbol", "Size", symbolM, 0.05f, 30f, 0.05f, "m");
            rotate = Widgets.Number("tpl.rotate", "Rotate", rotate, -180f, 180f, 15f, "°", "0");
            mirror = Widgets.Check(mirror, "Mirror (flip left to right)");
            ColourRows(w, "Colour", a, "tpl.a");
        }

        /// <summary>The swatches, a hex field and the opacity of one colour.</summary>
        static void ColourRows(SignLinkWindow w, string label, Colour c, string key, bool opacity = true)
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
            if (opacity) c.Alpha = Widgets.Number(key + ".alpha", "Opacity", c.Alpha, 10f, 100f, 5f, "%", "0");
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

        /// <summary>The picked template's lines, as Insert puts them in; null while Problem says why not.</summary>
        public List<Snippets.Line> Lines()
        {
            switch (picked)
            {
                case 0: return Snippets.Block(wM, hM, a.Rgb, A(a));
                case 1: return Snippets.Label(label, letterM, a.Rgb, A(a));
                case 2: return Snippets.Symbol(symbol, symbolM, a.Rgb, A(a), mirror, rotate);
                case 3: return Snippets.Pattern(GlyphData.PatternGlyphs[Mathf.Clamp(patternGlyph, 0, GlyphData.PatternGlyphs.Length - 1)],
                                                cols, rows, cellCm / 100f, a.Rgb, A(a), b.Rgb, A(b), alternate);
                case 4: return qrLines != null ? new List<Snippets.Line>(qrLines) : null;
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
                case 4: return qrWhat;
                default: return "";
            }
        }
    }
}
