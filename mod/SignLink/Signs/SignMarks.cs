using System;
using System.Collections.Generic;

namespace SignLink.Signs
{
    /// <summary>SignLink's marks on a stack's first sign: when it last wrote it, what it put there and which version
    /// did it, and the record of what made the content (the source that can edit it again).</summary>
    internal static class SignMarks
    {
        /// <summary>SignLink's mark, on a stack's first sign: when SignLink last wrote it (UTC ticks; 0 or none: not
        /// SignLink's), what it put there (a picture's file name, an art piece's name; empty when unknown) and
        /// which SignLink version did it (since 0.18.0). A few bytes in the sign's data: nothing shows, nothing to load.</summary>
        public static readonly int MadeKey = "signlink_made".GetStableHashCode(), NameKey = "signlink_name".GetStableHashCode(),
                                   VersionKey = "signlink_ver".GetStableHashCode();

        /// <summary>On a stack's first sign: what put its content there, so the source that did it can edit it again
        /// (since 0.19.0; SignRecipe): "pic|width|pitch|cover|alpha|glow|animate|file", "art|light|path",
        /// "saved|light|path" (before 0.20.63, read as none), "text"; "" when unknown (typed, pasted, older versions). Its
        /// size and place on the sign are the fit (SignFit), its pose the sign's.</summary>
        public static readonly int SourceKey = "signlink_src".GetStableHashCode(), SourceLenKey = "signlink_src_len".GetStableHashCode();

        /// <summary>The record, while the first sign still holds the text it was written for (its length: anything that
        /// rewrote it since, an older SignLink, the game's editor or a tool, leaves no record to edit from).</summary>
        public static string Source(Sign root)
        {
            ZDO zdo = SignFinder.Zdo(root);
            if (zdo == null) return "";
            string src = zdo.GetString(SourceKey, "");
            return src.Length > 0 && zdo.GetInt(SourceLenKey, -1) == zdo.GetString(ZDOVars.s_text, "").Length ? src : "";
        }

        /// <summary>The longest recipe kept (a picture's name and its settings stay far under it): a pasted clipboard is
        /// another player's text, and a recipe of megabytes would ride on the sign's data past every size check (1.1.0,
        /// review). A longer one is not kept: the sign then shows its content without the recipe, as a plain text.</summary>
        public const int MaxSource = 1024;

        public static void SetSource(Sign root, string source)
        {
            ZDO zdo = SignFinder.Zdo(root);
            if (zdo == null) return;
            if (source != null && source.Length > MaxSource)
            {
                Plugin.Log.LogWarning($"a recipe of {source.Length} characters is not kept on {SignFinder.Id(root)} (at most {MaxSource})");
                source = "";
            }
            root.m_nview.ClaimOwnership();
            zdo.Set(SourceKey, source ?? "");
            zdo.Set(SourceLenKey, zdo.GetString(ZDOVars.s_text, "").Length);
        }

        /// <summary>The SignLink version that last wrote the stack whose first sign this is ("" when unknown).</summary>
        public static string MadeWith(Sign root) => SignFinder.Zdo(root)?.GetString(VersionKey, "") ?? "";

        /// <summary>Puts SignLink's mark on a first sign (made: content SignLink put there, named), or takes it off (an
        /// emptied stack).</summary>
        public static void Mark(ZDO root, bool made, string name)
        {
            if (root == null) return;
            root.Set(MadeKey, made ? DateTime.UtcNow.Ticks : 0L);
            root.Set(NameKey, made ? name ?? "" : "");
            root.Set(VersionKey, made ? Plugin.Version : "");
            root.Set(SourceKey, "");                                        // the writer says what it put (SetSource)
        }

        /// <summary>A sign SignLink made or wrote: its mark, a copy's mark (0.17.0), or more text than the game's editor
        /// takes (pictures of older versions: nobody types those).</summary>
        static bool Marked(ZDO z, int limit) =>
            z != null && (z.GetLong(MadeKey, 0L) > 0 || z.GetInt(SignBuilder.CopyKey, 0) != 0 || z.GetString(ZDOVars.s_text, "").Length > limit);

        /// <summary>What SignLink put on the stack whose first sign this is ("" when unknown).</summary>
        public static string Name(Sign root) => SignFinder.Zdo(root)?.GetString(NameKey, "") ?? "";

        /// <summary>Did SignLink make what this stack holds? Only by its marks (Marked), never by how many signs stand
        /// there: signs stacked by hand are the player's own.</summary>
        public static bool IsSignLinks(List<Sign> stack)
        {
            foreach (Sign s in stack)
                if (s && Marked(SignFinder.Zdo(s), s.m_characterLimit)) return true;
            return false;
        }

        /// <summary>The same from a ZDO alone (a sign too far away to be loaded; the editor takes 50 characters).</summary>
        public static bool IsSignLinks(ZDO zdo) => Marked(zdo, 50);

        /// <summary>One sign SignLink made or wrote, by its own marks (a first sign's, a copy's, or more text than the
        /// game's editor takes): what the multiplayer area limit counts (SignArea, 0.20.67).</summary>
        public static bool IsSignLinks(Sign s) => s && Marked(SignFinder.Zdo(s), s.m_characterLimit);

        /// <summary>A copy stacked on a first sign (SignBuilder.Clone): the stack it belongs to counts once.</summary>
        public static bool IsCopy(ZDO z) => z != null && z.GetInt(SignBuilder.CopyKey, 0) != 0;
    }
}
