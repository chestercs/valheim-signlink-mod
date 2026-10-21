using System;
using System.Collections.Generic;
using PetyusPolisz.Kit;
using UnityEngine;
using Object = UnityEngine.Object;

namespace SignLink.Signs
{
    /// <summary>Places and removes sign pieces in code, as the build hammer would (persistent ZDOs, synced to everyone).
    /// Copies have no creator, like world-placed pieces: monsters do not target them; the hammer still removes them, and
    /// they drop nothing (CopyKey, Patches/CopyDropPatch: they cost nothing to make).</summary>
    internal static class SignBuilder
    {
        public const float DefaultStep = 0.0005f;
        /// <summary>An int 1 on every copy of a stack (not on its first sign).</summary>
        public static readonly int CopyKey = "signlink_copy".GetStableHashCode();
        /// <summary>An int 1 on every sign SignLink made for nothing (a copy), kept when a copy comes to lead its stack
        /// (CopyKey goes then): such a sign drops nothing (CopyDropPatch), or hammering leaders would make wood.</summary>
        public static readonly int FreeKey = "signlink_free".GetStableHashCode();

        /// <summary>One copy of src per text at its exact rotation and position, the k-th moved k x stepM metres past
        /// the stack's front along the sign's normal (a hair apart, so tiles of one picture never z-fight but stay in
        /// one plane; 0: DefaultStep, or FloorPlan.Step on a floor picture, whose boards must stay under the floor). The
        /// copies hang where the original hangs, so they share its support.</summary>
        public static List<Sign> Clone(Sign src, IList<string> texts, float stepM = 0f, bool checkedFirst = false)
        {
            if (stepM <= 0f) stepM = SignPose.Tipped(src) ? (float)FloorPlan.Step : DefaultStep;
            ZDO srcZdo = SignFinder.Zdo(src) ?? throw new InvalidOperationException(Lang.T("the sign has no ZDO"));
            GameObject prefab = ZNetScene.instance.GetPrefab(srcZdo.GetPrefab())
                                ?? throw new InvalidOperationException(Lang.T("prefab not found"));
            if (Location.IsInsideNoBuildLocation(src.transform.position))   // as the hammer (1.0.2, review)
                throw new InvalidOperationException(Lang.T("the game allows no building there"));
            if (!checkedFirst)                                              // SetContent checked before writing anything
            {
                SignText.RequireEdit(src);
                SignFinder.RequireLoaded(src);                              // no copy of the stack left out
                foreach (string t0 in texts) SignText.RequireSendable(t0);
            }
            SignMove.WriteFor(src);                                         // the copies go where the signs are now
            Transform t = src.transform;
            float front = 0f;
            foreach (Sign o in SignFinder.Stack(src))
                front = Mathf.Max(front, Vector3.Dot(o.transform.position - t.position, t.forward));
            if (front + stepM * texts.Count >= SignFinder.StackDepth)       // past it the copies would not read as one stack
                throw new ArgumentException(Lang.F("too many copies for one sign ({0} more in front of {1:0.#} mm)", texts.Count, front * 1000));
            var made = new List<Sign>();
            for (int k = 0; k < texts.Count; k++)
            {
                GameObject go = Object.Instantiate(prefab, t.position + t.forward * (front + stepM * (k + 1)), t.rotation);
                Sign s = go.GetComponent<Sign>();
                ZDO zdo = SignFinder.Zdo(s);
                zdo.m_rotation = srcZdo.m_rotation;                         // the very same angles: the same in the save
                zdo.Set(CopyKey, 1);
                zdo.Set(FreeKey, 1);
                zdo.Set(ZDOVars.s_text, texts[k]);
                SignText.StampAuthor(zdo);
                s.m_lastRevision = uint.MaxValue;
                try { s.UpdateText(); }
                catch (Exception e) { Plugin.Log.LogWarning($"a new copy shows its text later: {e.Message}"); }
                made.Add(s);
            }
            return made;
        }

        /// <summary>Throws when n signs, a step apart, would reach past one stack's depth (SignFinder.StackDepth): the
        /// copies past it would read as no stack's (0.20.130, review).</summary>
        public static void RequireDepth(Sign root, int n)
        {
            float step = SignPose.Tipped(root) ? (float)FloorPlan.Step : DefaultStep;
            if (n * step >= SignFinder.StackDepth) throw new ArgumentException(Lang.F("too many copies for one sign ({0})", n));
        }

        /// <summary>Removes signs for good (ZDO destroyed after an ownership claim; nothing drops). Signs under a ward
        /// you cannot access are skipped. Returns how many went.</summary>
        public static int Delete(IEnumerable<Sign> signs)
        {
            var all = new List<Sign>(signs);
            SignMove.Drop(all);                                             // their moves not applied go with them, unsent
            int n = 0;
            foreach (Sign s in all)
            {
                if (!s || SignFinder.Zdo(s) == null || !SignText.CanEdit(s)) continue;
                Plugin.Screens.Stop(s);
                s.m_nview.ClaimOwnership();
                ZNetScene.instance.Destroy(s.gameObject);
                n++;
            }
            return n;
        }

        /// <summary>Gives a sign's whole stack exactly these texts, one per sign, with exactly as many signs as texts: the
        /// stack's first sign (the original) takes texts[0], the copies already there take the next ones in order, more
        /// copies are made when needed and the ones left over are removed. Every write of new texts goes through here (a new
        /// pose writes them as it makes the signs again, SignPose.Set), so no copy keeps part of an earlier picture, and clearing
        /// a stack shrinks it back to one sign. The first sign gets SignLink's mark and the content's name (SignMarks.MadeKey,
        /// NameKey; an empty stack loses both). Returns the stack's signs, first to last.</summary>
        public static List<Sign> SetContent(Sign sign, IList<string> texts, string name = null)
        {
            if (texts == null || texts.Count == 0) texts = new[] { "" };
            if (!sign || SignFinder.Zdo(sign) == null) throw new InvalidOperationException(Lang.T("the sign is gone (unloaded or removed)"));
            List<Sign> stack = SignFinder.Stack(sign);
            Sign root = stack.Count > 0 ? stack[0] : sign;
            SignText.RequireEdit(root);
            SignFinder.RequireLoaded(root);                                   // before anything is written: no copy missed
            foreach (string t in texts) SignText.RequireSendable(t);       // all of them, before anything is written
            SignText.RequireRoom(root, Pictures.SaveCost.Bytes(texts), Lang.T("this content"));   // the multiplayer limits
            RequireDepth(root, texts.Count);
            // more signs or fewer is building or removing, which the game allows nowhere near a trader or a boss (as the
            // hammer: 1.0.2, review); the same number of signs only takes new texts, as vanilla's own editing does
            if (texts.Count < stack.Count && Location.IsInsideNoBuildLocation(root.transform.position))
            {
                // fewer texts there keep the signs, the spare ones emptied (review: 1 Sign's Clear of a stack worked in 1.0.1, and
                // emptying is vanilla's own editing; a copy fewer is removing)
                var kept = new List<string>(texts);
                while (kept.Count < stack.Count) kept.Add("");
                texts = kept;
            }
            if (texts.Count != stack.Count && Location.IsInsideNoBuildLocation(root.transform.position))
                throw new InvalidOperationException(Lang.F("the game allows no building or removing there, so the sign keeps its {0} sign(s) and this content needs {1}",
                                                           stack.Count, texts.Count));
            List<Sign> copies = stack.FindAll(o => o != root);
            int need = texts.Count - 1;
            SignMove.WriteFor(root);                                        // allowed: moves not applied yet are kept, and copies
                                                                            // go in front of where the signs are
            foreach (Sign s in stack) Plugin.Screens.Stop(s);
            SignText.Apply(root, texts[0], local: false);
            bool empty = true;
            foreach (string t in texts) empty &= string.IsNullOrEmpty(t);
            ZDO rootZdo = SignFinder.Zdo(root);
            SignMarks.Mark(rootZdo, !empty, name);
            rootZdo?.Set(CopyKey, 0);                                     // a stack whose original went: its front copy leads now
            rootZdo?.Set(SignFit.FitKey, Vector3.zero);                   // a new content: its fit is written by whoever
            rootZdo?.Set(SignFit.CentreKey, Vector3.zero);                // knows it (SignFit), else it is as made
            var result = new List<Sign> { root };
            int reuse = Math.Min(need, copies.Count);
            for (int i = 0; i < reuse; i++)
            {
                SignText.Apply(copies[i], texts[i + 1], local: false);
                ZDO c = SignFinder.Zdo(copies[i]);
                if (c != null && c.GetLong(ZDOVars.s_creator, 0L) != 0L)
                {
                    c.Set(ZDOVars.s_creator, 0L);                         // an old copy (0.12.0) with its first sign's
                    c.Set(CopyKey, 1);                                    // creator: a plain copy from now on
                }
                result.Add(copies[i]);
            }
            if (need > reuse)
            {
                var rest = new List<string>();
                for (int i = reuse + 1; i < texts.Count; i++) rest.Add(texts[i]);
                result.AddRange(Clone(root, rest, 0f, checkedFirst: true));
            }
            List<Sign> left = copies.GetRange(reuse, copies.Count - reuse);
            if (left.Count > 0) Delete(left);
            rootZdo?.Set(SignFinder.CountKey, result.Count);                // what a client must see before it edits (IsLoaded)
            return result;
        }

        /// <summary>The texts of a sign's whole stack, first to last (what SetContent would put back).</summary>
        public static List<string> Content(Sign sign)
        {
            var texts = new List<string>();
            foreach (Sign s in SignFinder.Stack(sign)) texts.Add(SignText.SavedText(s));
            while (texts.Count > 1 && texts[texts.Count - 1].Length == 0) texts.RemoveAt(texts.Count - 1);
            return texts;
        }

        /// <summary>Creating and deleting signs needs a world with the player in it; single player and multiplayer alike
        /// (0.20.44: the multiplayer limits keep the content small, a ward still protects its signs).</summary>
        public static void Guard()
        {
            if (!ZNet.instance || !Player.m_localPlayer) throw new InvalidOperationException(Lang.T("not in a world"));
        }
    }
}
