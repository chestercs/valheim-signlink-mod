using System;
using System.Collections.Generic;
using System.Diagnostics;
using SignLink.Pictures;
using Splatform;

namespace SignLink.Signs
{
    /// <summary>A sign's text: reading what it holds, writing it the way the game does (ward, ownership, author), its
    /// size, and what other players can receive.</summary>
    internal static class SignText
    {
        public static string SavedText(Sign s) => SignFinder.Zdo(s)?.GetString(ZDOVars.s_text, "") ?? "";

        static readonly Dictionary<Sign, KeyValuePair<uint, int>> bytes = new Dictionary<Sign, KeyValuePair<uint, int>>();
        static int bytesGone;

        /// <summary>The UTF-8 size of the sign's saved text, counted again only when the sign changed (DataRevision): a
        /// picture's texts are megabytes, and the window asks every second.</summary>
        public static int TextBytes(Sign s)
        {
            ZDO zdo = SignFinder.Zdo(s);
            if (zdo == null) return 0;
            if (bytes.TryGetValue(s, out KeyValuePair<uint, int> c) && c.Key == zdo.DataRevision) return c.Value;
            SignFinder.Prune(bytes, ref bytesGone);                         // destroyed signs go (0.20.132, review)
            if (bytes.Count > 2000) bytes.Clear();
            int n = System.Text.Encoding.UTF8.GetByteCount(zdo.GetString(ZDOVars.s_text, ""));
            bytes[s] = new KeyValuePair<uint, int>(zdo.DataRevision, n);
            return n;
        }

        /// <summary>May this player change things here (no foreign ward)? Sign.SetText asks the same.</summary>
        public static bool CanEdit(Sign s) => s && PrivateArea.CheckAccess(s.transform.position, 0f, false);

        public static void RequireEdit(Sign s)
        {
            if (!CanEdit(s)) throw new InvalidOperationException("a ward you have no access to protects this sign");
        }

        /// <summary>Multiplayer: other players receive this world's signs (playing on a server, hosting a world others can join
        /// with Start server, ZNet.IsOpenServer, even before they do, or hosting with peers). A sign over 512 KiB cannot be
        /// sent to them (docs/limits.md: the connection stalls), and the multiplayer limits hold (RequireRoom); single
        /// player has no limit.</summary>
        public static bool Shared() => ZNet.instance &&
            (!ZNet.instance.IsServer() || ZNet.IsOpenServer() || ZNet.instance.GetPeers().Count > 0);

        /// <summary>In a shared world, refuses to let the stack of any hold newBytes (SaveCost.Bytes) when that breaks a
        /// multiplayer limit (SaveCost.Allowed: SignLimit on the stack, AreaLimit for the signs around it unless it gets
        /// no bigger); what says what is being stored, for the message. Single player: nothing to check.</summary>
        public static void RequireRoom(Sign any, long newBytes, string what = null)
        {
            if (!Shared()) return;
            List<Sign> stack = any ? SignFinder.Stack(any) : new List<Sign>();
            Sign root = stack.Count > 0 ? stack[0] : any;
            long old = root ? SignPose.Stored(root) : 0;
            long others = root ? SignArea.Others(root.transform.position, stack, out _) : 0;
            if (!SaveCost.Allowed(newBytes, old, others, true, out string why))
                throw new InvalidOperationException((what != null ? what + ": " : "") + why + " (single player has no limit)");
        }

        public const int NetworkLimit = 512 * 1024;

        public static void RequireSendable(string text)
        {
            if (Shared() && System.Text.Encoding.UTF8.GetByteCount(text ?? "") > NetworkLimit)
                throw new InvalidOperationException("a sign would hold over 512 KB: more than other players can receive (only single player allows that)");
        }

        /// <summary>Shows text on the sign and returns the milliseconds it took, TMP mesh included. local: only this
        /// client's text mesh (and m_currentText, which the vanilla 2 s refresh re-applies); else Sign.SetText, as the
        /// in-game editor does (ward check, ownership, author, ZDO write: every client gets it).</summary>
        public static double Apply(Sign s, string text, bool local)
        {
            var sw = Stopwatch.StartNew();
            if (local)
            {
                s.m_currentText = text;
                s.m_textWidget.text = text;
            }
            else
            {
                RequireSendable(text);
                RequireEdit(s);
                try { s.SetText(text); }
                catch (InvalidOperationException) when (SavedText(s) == text)   // written; the game's view check threw on a
                {                                                           // "host" author (not signed in): shown, and the
                    s.m_currentText = text;                                 // stack goes on (0.20.130, review)
                    s.m_textWidget.text = text;
                }
                Animation.StoredAnims.ClearBlob(s);
            }
            s.m_textWidget.ForceMeshUpdate();
            return sw.Elapsed.TotalMilliseconds;
        }

        /// <summary>The author fields Sign.SetText writes (who wrote it: platforms hide text by blocked or muted
        /// authors, and by everyone when user content is off).</summary>
        public static void StampAuthor(ZDO zdo)
        {
            Author(out string id, out string shown);
            zdo.Set(ZDOVars.s_author, id);
            if (shown != null) zdo.Set(ZDOVars.s_authorDisplayName, shown);
        }

        /// <summary>This player as a sign's author: the platform id ("host" when not signed in) and the name the server
        /// gave them, if any.</summary>
        public static void Author(out string id, out string shown)
        {
            var user = PlatformManager.DistributionPlatform.LocalUser;
            id = user.IsSignedIn ? user.PlatformUserID.ToString() : "host";
            shown = ZNet.TryGetServerAssignedDisplayName(user.PlatformUserID, out string name) ? name : null;
        }

        /// <summary>Shows the saved text again (after a local animation).</summary>
        public static void Restore(Sign s)
        {
            if (!s || SignFinder.Zdo(s) == null) return;                   // removed or unloading this frame
            s.m_lastRevision = uint.MaxValue;
            try { s.UpdateText(); }                                         // the game throws for some authors it does not own
            catch (Exception e) { Plugin.Log.LogWarning($"a sign shows its saved text later: {e.Message}"); }
        }
    }
}
