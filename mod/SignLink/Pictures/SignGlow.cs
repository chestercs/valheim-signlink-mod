using System;
using System.Collections.Generic;
using System.Text;

namespace SignLink.Pictures
{
    /// <summary>How sign content is lit: as it was made, all glowing, or all lit by the scene.</summary>
    internal enum GlowMode { AsMade, Glowing, Lit }

    /// <summary>Glowing or lit sign text. The sign's own material is lit by the scene: shaded in daylight, dark at
    /// night. A &lt;font&gt; tag naming the Valheim-Norse font asset switches to that asset's unlit material, so colours
    /// show as written, day and night (docs/sign-rendering.md 4b). The tag is the Resources path, or any value with the
    /// asset name's TMP hash (the name itself, a font_alias.py alias). Other fonts (the Rune font) are left alone.</summary>
    internal static class SignGlow
    {
        /// <summary>What a glowing text starts with.</summary>
        public const string Tag = "<font=" + PixelText.Font + ">";

        static readonly int NorseHash = TmpHash("Valheim-Norse");

        public static IList<string> Apply(IList<string> texts, GlowMode mode)
        {
            if (mode == GlowMode.AsMade) return texts;
            var o = new string[texts.Count];
            for (int i = 0; i < o.Length; i++) o[i] = Apply(texts[i], mode);
            return o;
        }

        /// <summary>Glowing: the whole text inside one glowing tag (its own glowing tags dropped first, so lit parts glow
        /// too). Lit: every glowing tag dropped. An empty sign stays empty.</summary>
        public static string Apply(string text, GlowMode mode)
        {
            if (mode == GlowMode.AsMade || string.IsNullOrEmpty(text)) return text;
            string lit = Strip(text);
            return mode == GlowMode.Lit ? lit : Tag + lit;
        }

        /// <summary>Does the text hold a glowing font tag anywhere (the Text source's Light row: glowing, else lit by the
        /// scene like any sign text)? An empty text is neither.</summary>
        public static bool IsGlowing(string text) => !string.IsNullOrEmpty(text) && Strip(text) != text;

        /// <summary>The text without its glowing &lt;font&gt; tags and the &lt;/font&gt; tags that close them. Tags are read as
        /// TMP reads them: from '&lt;' to the next '&gt;', a '&lt;' before it starts over.</summary>
        public static string Strip(string text)
        {
            if (string.IsNullOrEmpty(text) || text.IndexOf("font", StringComparison.OrdinalIgnoreCase) < 0) return text;
            var sb = new StringBuilder(text.Length);
            var glowing = new Stack<bool>();                   // per open <font>: was it a glowing one (dropped)?
            int i = 0, copied = 0;
            while (i < text.Length)
            {
                int lt = text.IndexOf('<', i);
                if (lt < 0) break;
                int end = text.IndexOfAny(SaveCost.Brackets, lt + 1);
                if (end < 0) break;
                if (text[end] == '<')
                {
                    i = end;
                    continue;
                }
                bool drop = false;
                if (string.Compare(text, lt + 1, "font=", 0, 5, StringComparison.OrdinalIgnoreCase) == 0)
                {
                    drop = IsGlowFont(text.Substring(lt + 6, end - lt - 6));
                    glowing.Push(drop);
                }
                else if (end - lt == 6 && string.Compare(text, lt + 1, "/font", 0, 5, StringComparison.OrdinalIgnoreCase) == 0)
                    drop = glowing.Count > 0 && glowing.Pop();
                if (drop)
                {
                    sb.Append(text, copied, lt - copied);
                    copied = end + 1;
                }
                i = end + 1;
            }
            if (copied == 0) return text;
            sb.Append(text, copied, text.Length - copied);
            return sb.ToString();
        }

        /// <summary>Does a &lt;font=VALUE&gt; switch to the unlit Valheim-Norse material?</summary>
        public static bool IsGlowFont(string value)
        {
            value = value.Trim().Trim('"');
            return value.Equals(PixelText.Font, StringComparison.OrdinalIgnoreCase) || TmpHash(value) == NorseHash;
        }

        /// <summary>TMP_TextUtilities.GetHashCode: h = h * 33 ^ upper(c) (ASCII letters only), from 0.</summary>
        static int TmpHash(string s)
        {
            int h = 0;
            foreach (char c in s) h = ((h << 5) + h) ^ (c >= 'a' && c <= 'z' ? c - 32 : c);
            return h;
        }
    }
}
