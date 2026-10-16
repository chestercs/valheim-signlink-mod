using System;
using System.Text.RegularExpressions;
using HarmonyLib;

namespace SignLink.Patches
{
    /// <summary>Valheim's RemoveRichTextTags keeps tags with commas, so a sign's &lt;sprite anim="a,b,f"&gt; reaches its
    /// hover text; when that text empties, TMP returns before StopAllAnimations and the face stays by the crosshair
    /// (docs/sign-rendering.md 7f, "ghost face"). Sprite tags are always stripped from the hover text (the sign itself
    /// still shows them); a switch to keep them (0.19-0.20.3, HoverSprites) only let the stuck face back in. An error
    /// inside switches the patch off (the hover text is left as the game made it).</summary>
    [HarmonyPatch(typeof(Sign), nameof(Sign.GetHoverText))]
    internal static class HoverTextPatch
    {
        // no '<' inside a tag (0.20.132, review): with "[^>]*" every "<sprite" of a text without '>' scanned to its end,
        // O(k x n) for k of them; now a scan stops at the next '<', so the text is read once. Well-formed tags match as before.
        static readonly Regex SpriteTag = new Regex("<sprite[^<>]*>", RegexOptions.IgnoreCase);
        static bool off;

        static void Postfix(ref string __result)
        {
            if (off) return;
            try
            {
                if (__result != null && __result.IndexOf("<sprite", StringComparison.OrdinalIgnoreCase) >= 0)
                    __result = SpriteTag.Replace(__result, "");
            }
            catch (Exception e)
            {
                off = true;
                Plugin.Log.LogWarning($"HoverTextPatch is off after an error: {e.Message}");
            }
        }
    }
}
