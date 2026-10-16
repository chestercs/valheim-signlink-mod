using System;
using System.Collections.Generic;
using HarmonyLib;
using SignLink.Signs;
using SignLink.UI;
using UnityEngine;

namespace SignLink.Patches
{
    /// <summary>Signs with more text than the game's editor takes (TextInput gets Sign.m_characterLimit = 50: pictures,
    /// glyph art, the copies of a stack; no player typed them) read as one thing in the world, the stack's copies as
    /// ghosts of their original:
    /// - the hover text is one short line about the stack, not the whole text: vanilla strips its tags with a regex every
    ///   frame (Sign.cs:45), megabytes for a picture, and shows the rest by the crosshair (Hovers);
    /// - E opens SignLink on the stack instead of the editor, which cuts the text to 50 characters (TMP_InputField.cs:1874)
    ///   and would break the picture on Enter: only for content SignLink made (its marks) or a picture-like text; another
    ///   mod's long text keeps the game's editor, which that mod may have widened (Opens).
    /// Signs whose text is hidden (user-generated content off, a muted author) keep the game's behaviour.
    /// "3 - Sign options / PreventEditor" switches it off.</summary>
    internal static class PictureSign
    {
        sealed class Entry
        {
            public uint Revision;
            public float At;
            public int Count;
            public bool Ours, Opens, Picture;
            public string Line;
        }

        static readonly Dictionary<Sign, Entry> lines = new Dictionary<Sign, Entry>();
        static int linesGone;

        /// <summary>Does this sign get the short hover line: more text than the editor takes, or one of SignLink's
        /// stacks or pictures (signs stacked by hand keep the game's hover text, and so does a short text SignLink wrote
        /// or a sign it only posed: they read as they are)?</summary>
        public static bool Hovers(Sign s)
        {
            if (!PluginConfig.PreventEditor.Value || !s || !s.m_isViewable || s.m_textWidget == null) return false;
            int limit = s.m_characterLimit;                          // the saved text, or what shows (an animation frame)
            if (SignText.SavedText(s).Length > limit || (s.m_textWidget.text?.Length ?? 0) > limit) return true;
            ZDO z = SignFinder.Zdo(s);                                 // no mark: a plain sign, no stack to look for
            if (z == null || !Marked(z)) return false;
            Entry e = Info(s);
            return e.Ours && (e.Count > 1 || e.Picture);
        }

        /// <summary>Does E open SignLink on it instead of the game's editor?</summary>
        public static bool Opens(Sign s) => Hovers(s) && Info(s).Opens;

        public static string Line(Sign s) => Info(s).Line;

        static bool Marked(ZDO z) => z.GetLong(SignMarks.MadeKey, 0L) != 0L || z.GetInt(SignBuilder.CopyKey, 0) != 0;

        /// <summary>The sign's stack and one line about it, made again when the sign changed or every 2 s (the stack may
        /// grow; finding it looks at every loaded sign, so only a marked sign's is looked for).</summary>
        static Entry Info(Sign s)
        {
            ZDO zdo = SignFinder.Zdo(s);
            uint rev = zdo?.DataRevision ?? 0;
            // an animated sign changes every frame: counted again at most every half second
            if (lines.TryGetValue(s, out Entry e) &&
                (Time.unscaledTime - e.At < 0.5f || (e.Revision == rev && Time.unscaledTime - e.At < 2f))) return e;
            SignFinder.Prune(lines, ref linesGone);                         // destroyed signs go (0.20.132, review)
            if (lines.Count > 500) lines.Clear();
            bool marked = zdo != null && Marked(zdo);
            List<Sign> stack = marked ? SignFinder.Stack(s) : new List<Sign> { s };
            int n = stack.Count;
            string text = SignText.SavedText(s);
            bool picture = text.IndexOf('■') >= 0;
            bool pictureLike = picture || text.StartsWith("<line-height", StringComparison.Ordinal) || text.StartsWith("<size", StringComparison.Ordinal);
            bool ours = marked && SignMarks.IsSignLinks(stack);
            // a piece of glyph art says so (0.20.191, test: Midnight light read "A long text")
            bool art = marked && SignMarks.Source(stack[0]).StartsWith("art|", StringComparison.Ordinal);
            string what = picture ? "A picture" : art ? "Glyph art" : "A long text";
            string line = (n > 1 ? $"{what} on {n} stacked signs" : what) + (ours ? " (SignLink)" : "");
            e = new Entry { Revision = rev, At = Time.unscaledTime, Count = n, Ours = ours, Opens = marked || pictureLike, Picture = picture,
                            Line = line };
            lines[s] = e;
            return e;
        }
    }

    /// <summary>An error inside either patch switches it off (the game's own hover text and editor come back) instead
    /// of throwing on every sign, every frame.</summary>
    [HarmonyPatch(typeof(Sign), nameof(Sign.GetHoverText))]
    internal static class PictureSignHoverPatch
    {
        static bool off;

        static bool Prefix(Sign __instance, ref string __result)
        {
            if (off) return true;
            try
            {
                if (!PictureSign.Hovers(__instance)) return true;
                string line = PictureSign.Line(__instance);
                __result = !PictureSign.Opens(__instance) || !PrivateArea.CheckAccess(__instance.transform.position, 0f, false) ? line
                    : line + "\n" + Localization.instance.Localize("[<color=yellow><b>$KEY_Use</b></color>]") + " Open in SignLink";
                return false;
            }
            catch (Exception e)
            {
                off = true;
                Plugin.Log.LogWarning($"PictureSignHoverPatch is off after an error: {e.Message}");
                return true;
            }
        }
    }

    [HarmonyPatch(typeof(Sign), nameof(Sign.Interact))]
    internal static class PictureSignInteractPatch
    {
        static bool off;

        static bool Prefix(Sign __instance, bool hold, ref bool __result)
        {
            if (off) return !Long(__instance, ref __result);
            try
            {
                if (hold || !PictureSign.Opens(__instance) || !SignLinkWindow.Instance) return true;
                __result = PrivateArea.CheckAccess(__instance.transform.position);    // flashes the ward, as the game does
                if (__result) SignLinkWindow.Instance.OpenFor(__instance);
                return false;
            }
            catch (Exception e)
            {
                off = true;
                Plugin.Log.LogWarning($"PictureSignInteractPatch is off after an error: {e.Message}");
                return !Long(__instance, ref __result);
            }
        }

        /// <summary>A text longer than the game's editor takes: it would cut it on Enter (a picture broken, on a server for
        /// everyone), so E does nothing on it while the patch is off (0.20.130, review).</summary>
        static bool Long(Sign s, ref bool result)
        {
            try
            {
                if (s && SignText.SavedText(s).Length > s.m_characterLimit)
                {
                    result = false;
                    return true;
                }
            }
            catch (Exception) { }
            return false;
        }
    }
}
