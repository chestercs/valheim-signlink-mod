using System;
using System.Collections.Generic;
using UnityEngine;

namespace SignLink.Signs
{
    /// <summary>Moves or turns a stack a little without making its signs again (3 Place, Move the sign): the signs move
    /// in this game at once, for this player only, and their ZDOs follow when the player applies (Write; 0.20.34, the
    /// author: have Apply and Undo; each update sends the stack's texts to everyone near, so a run of steps is one
    /// update) or are put back (Revert: Undo, the window closing, another sign selected). Anything that changes the stack
    /// writes first, so it starts from where the signs are. Other SignLink players' games move the signs when the ZDO arrives
    /// (Follow: signs have no ZSyncTransform, the game places a sign only when it loads), so players without SignLink
    /// see the move when they come back to the area. The total move (NudgeKey, world metres: as far as the game holds
    /// the sign up there, Holds, within MaxM of where it was built) and the total
    /// turn (TurnKey, degrees about the up axis through the first sign, as the hammer turns a piece) are kept on the
    /// first sign for Reset to default (3 Place › Changes). The stack's base pose moves and turns along (SignPose.MoveBase), so
    /// its pose stays as it is.</summary>
    internal static class SignMove
    {
        /// <summary>On a stack's first sign: how far it has been moved since it was built (world metres).</summary>
        public static readonly int NudgeKey = "signlink_nudge".GetStableHashCode();
        /// <summary>On a stack's first sign: how far it has been turned about the up axis since it was built (degrees,
        /// clockwise seen from above).</summary>
        public static readonly int TurnKey = "signlink_turned".GetStableHashCode();
        /// <summary>How far a sign moves from where it was built: as far as the game holds it up there (Holds: support
        /// weighed as the game weighs it, 0.20.106), within MaxM, so a step gone wrong does not carry it off (0.20.109, the
        /// author: larger distances too ... go ahead, sounds good if we can really do it reliably): a sign standing
        /// on a floor goes metres towards or away, one on a wall barely off it. Fixed limits before (0.20.55: half a metre
        /// along its face, 10 cm off it), from when only whether anything was there could be asked.</summary>
        public const float MaxM = 5f;
        const float FollowEvery = 0.5f, VerifyAfter = 2f;

        static List<Sign> pending;
        static Vector3 pendingDelta;
        static float pendingTurn;
        static float nextFollow, nextScan, verifyAt;
        static List<KeyValuePair<Sign, Pose>> verify;
        static readonly HashSet<Sign> moving = new HashSet<Sign>();
        /// <summary>The moving signs' wear, which the game does not weigh until the steps are applied or undone (Paused).</summary>
        static readonly HashSet<WearNTear> paused = new HashSet<WearNTear>();
        /// <summary>Why the game would not hold the stack up where the steps not applied leave it (Weigh), or null.</summary>
        static string unheld;
        static Sign[] known = new Sign[0];
        static readonly Dictionary<Sign, uint> seen = new Dictionary<Sign, uint>();
        static int seenGone;

        /// <summary>How far the stack whose first sign this is has been moved (world metres), moves not yet written
        /// included.</summary>
        public static Vector3 Moved(Sign root)
        {
            ZDO zdo = SignFinder.Zdo(root);
            Vector3 m = zdo != null ? zdo.GetVec3(NudgeKey, Vector3.zero) : Vector3.zero;
            return pending != null && pending.Contains(root) ? m + pendingDelta : m;
        }

        /// <summary>The move of this stack made but not yet written to its ZDOs (zero otherwise).</summary>
        public static Vector3 Pending(Sign root) => pending != null && pending.Contains(root) ? pendingDelta : Vector3.zero;

        /// <summary>The turn of this stack made but not yet written (degrees about the up axis through its first sign,
        /// where it stands now; zero otherwise). SignPose.Base turns the stored base by it, as Write will.</summary>
        public static float PendingTurn(Sign root) => pending != null && pending.Contains(root) ? pendingTurn : 0f;

        /// <summary>How far the stack whose first sign this is has been turned since it was built (degrees, clockwise
        /// seen from above, -180..180), turns not yet written included.</summary>
        public static float Turned(Sign root)
        {
            ZDO zdo = SignFinder.Zdo(root);
            float t = zdo != null ? zdo.GetFloat(TurnKey, 0f) : 0f;
            if (pending != null && pending.Contains(root)) t += pendingTurn;
            return Mathf.DeltaAngle(0f, t);
        }

        /// <summary>Turns the stack by degrees about the up axis through its first sign, in this game now, as it is
        /// (the copies swing along, the sign stays where it stands); the ZDOs follow on Apply (Write). Throws, changing nothing,
        /// where it may not: a ward, the stack still loading.</summary>
        public static void Turn(List<Sign> stack, float degrees, Sign any = null)
        {
            Sign root = stack.Count > 0 ? stack[0] : any;
            if (!root || SignFinder.Zdo(root) == null) throw new InvalidOperationException("the sign is gone (unloaded or removed)");
            if (Mathf.Abs(degrees) < 1e-4f) return;
            if (pending != null && !Same(pending, stack)) Write();       // another stack, or it changed: those moves first
            SignText.RequireEdit(root);
            SignBuilder.Guard();
            SignFinder.RequireLoaded(root);
            Quaternion q = Quaternion.AngleAxis(degrees, Vector3.up);
            if (!Paces && !Holds(root, stack, Vector3.zero, q, out string why)) throw new InvalidOperationException(why);   // turned too (0.20.106)
            RequireSpot(stack, root.transform.position, q * root.transform.rotation);
            Vector3 pivot = root.transform.position;
            foreach (Sign s in stack)
            {
                if (!s) continue;
                s.transform.position = pivot + q * (s.transform.position - pivot);
                s.transform.rotation = q * s.transform.rotation;
                Moving(s);
            }
            Begin(stack);
            pendingTurn += degrees;
            Weigh(root, stack);
        }

        /// <summary>The steps go where the game would not hold the sign up (1.0.2, the author: the rotation sliders stopped
        /// where the sign would have no support; let them, and refuse only the apply, in red): the game does not weigh a
        /// moving sign's support (Patches.StepWearPatch, Paused), so it does not break it before the steps are applied,
        /// and Write refuses while it would. Without the patch (a game update) every step is weighed and refused, as
        /// before.</summary>
        static bool Paces => Plugin.Patched(typeof(Patches.StepWearPatch)) && !Patches.StepWearPatch.Off;

        static void Moving(Sign s)
        {
            moving.Add(s);
            WearNTear w = s.GetComponent<WearNTear>();
            if (w) paused.Add(w);
        }

        static void Stopped()
        {
            moving.Clear();
            paused.Clear();
            unheld = null;
        }

        /// <summary>Is this piece one of the signs whose steps are not applied yet (the game leaves its wear and support
        /// until they are: StepWearPatch)?</summary>
        public static bool Paused(WearNTear w) => paused.Count > 0 && paused.Contains(w);

        /// <summary>Why the game would not hold this stack up where its steps not applied leave it ("nothing would hold
        /// it up there ..."), or null: Apply is refused while there is a reason (Write).</summary>
        public static string Unheld(Sign root) => pending != null && root && pending.Contains(root) ? unheld : null;

        /// <summary>Do steps not applied leave some stack where the game would not hold it up (cheap: no stack looked at)?</summary>
        public static bool AnyUnheld => pending != null && unheld != null;

        /// <summary>Weighs where the steps leave the stack against where it stands for everyone (its ZDO), as a step is
        /// weighed (Holds, SupportMath.Refuse: no weaker than there, or held well enough), so the steps add up: one step
        /// off the wall and one back is held again.</summary>
        static void Weigh(Sign root, List<Sign> stack)
        {
            unheld = null;
            if (!Paces) return;                                             // without the patch every step is weighed (Holds)
            WearNTear wear = root.GetComponent<WearNTear>();
            ZDO zdo = SignFinder.Zdo(root);
            if (!wear || !wear.m_noSupportWear || zdo == null) return;
            try
            {
                var own = new HashSet<Collider>();
                foreach (Sign s in stack)
                    if (s) own.UnionWith(s.GetComponentsInChildren<Collider>(true));
                Transform t = root.transform;
                float least = wear.GetMinSupport();
                float there = SupportAt(root, wear, own, zdo.GetPosition() - t.position, zdo.GetRotation() * Quaternion.Inverse(t.rotation), false);
                float here = SupportAt(root, wear, own, Vector3.zero, Quaternion.identity, false);
                if (SupportMath.Refuse(there, here, least)) unheld = Weak(here, least);
            }
            catch (Exception e)
            {
                unheld = "it could not be weighed: " + e.Message;            // refused rather than let through unweighed (review)
            }
        }

        static string Weak(float after, float least) =>
            after <= 0f ? "nothing holds it there"
                        : $"too weak there ({after:0} of {least:0})";

        /// <summary>Is this reason (Holds, Unheld) the sign's support, and not a ward or a spot taken? The frame turns red for
        /// that alone (ContentTab.PoseProblem).</summary>
        public static bool WeakReason(string why) =>
            why != null && (why.StartsWith("nothing holds it", StringComparison.Ordinal) || why.StartsWith("too weak there", StringComparison.Ordinal));

        /// <summary>The stack whose moves and turns are gathered until the player applies or undoes them.</summary>
        static void Begin(List<Sign> stack)
        {
            if (pending != null) return;
            pending = stack;
            pendingDelta = Vector3.zero;
            pendingTurn = 0f;
        }

        /// <summary>Moves the stack by delta (world metres) in this game now; the ZDOs follow on Apply (Write). Throws, changing
        /// nothing, where it may not: a ward, the stack still loading, past MaxM, too weakly held there (Holds).</summary>
        public static void Nudge(Sign any, Vector3 delta) => Nudge(SignFinder.Stack(any), delta, any);


        /// <summary>The same for a stack found already (an arrow key's step does not search for it again).</summary>
        public static void Nudge(List<Sign> stack, Vector3 delta, Sign any = null)
        {
            Sign root = stack.Count > 0 ? stack[0] : any;
            if (!root || SignFinder.Zdo(root) == null) throw new InvalidOperationException("the sign is gone (unloaded or removed)");
            if (pending != null && !Same(pending, stack)) Write();       // another stack, or it changed: those moves first
            SignText.RequireEdit(root);
            SignBuilder.Guard();
            SignFinder.RequireLoaded(root);
            Vector3 moved = Moved(root), total = moved + delta;
            // a limit stops only a step that takes the sign further past it (0.20.70, review): one back towards where it
            // was built always goes
            if (total.magnitude > MaxM + 0.0001f && total.magnitude > moved.magnitude + 0.0001f)
                throw new InvalidOperationException($"at most {MaxM:0} m from where it was built");
            if (!Paces && !Holds(root, stack, delta, Quaternion.identity, out string why)) throw new InvalidOperationException(why);
            if (!PrivateArea.CheckAccess(root.transform.position + delta, 0f, false))
                throw new InvalidOperationException("a ward you have no access to protects that spot");
            RequireSpot(stack, root.transform.position + delta, root.transform.rotation);
            foreach (Sign s in stack)
            {
                if (!s) continue;
                s.transform.position += delta;
                Moving(s);
            }
            Begin(stack);
            pendingDelta += delta;
            Weigh(root, stack);
        }

        /// <summary>Throws when the stack's first sign may not stand at pos turned rot: the game builds nothing there (a
        /// trader's or a boss's place, Location.IsInsideNoBuildLocation, as the hammer checks), or another sign stands
        /// there that would read as one stack with it (SignFinder.Intruder: the next write would take it for a copy and
        /// overwrite or remove it; 0.20.130, review).</summary>
        internal static void RequireSpot(List<Sign> stack, Vector3 pos, Quaternion rot)
        {
            if (Location.IsInsideNoBuildLocation(pos)) throw new InvalidOperationException("the game allows no building there");
            Sign other = SignFinder.Intruder(stack, pos, rot);
            if (other) throw new InvalidOperationException("another sign stands there: it would join this sign's copies (move it a little further)");
        }

        static int supportMask;


        static readonly Collider[] supportHits = new Collider[256];

        /// <summary>Would the game hold the stack's first sign up well enough moved by delta and turned by q about where it
        /// stands (0.20.106, the author: I kept pressing until the signpost broke at random)? Weighed as the game weighs
        /// it (SupportMath): once where the signs will be when the move is applied (made again there, their boxes made
        /// anew), and once as the game sees a move not applied yet (its boxes stay where they were made, its centre of
        /// mass moves along), so a sign is not left too weakly held either way. A step that holds it better than before
        /// stays allowed. A sign with no WearNTear cannot break.</summary>
        internal static bool Holds(Sign root, List<Sign> stack, Vector3 delta, Quaternion q, out string why, bool remade = false)
        {
            why = null;
            WearNTear wear = root.GetComponent<WearNTear>();
            if (!wear || !wear.m_noSupportWear) return true;
            var own = new HashSet<Collider>();
            foreach (Sign s in stack)
                if (s) own.UnionWith(s.GetComponentsInChildren<Collider>(true));
            float least = wear.GetMinSupport();
            float now = SupportAt(root, wear, own, Vector3.zero, Quaternion.identity, false), after = SupportAt(root, wear, own, delta, q, false);
            if (SupportMath.Refuse(now, after, least))
            {
                why = Weak(after, least);
                return false;
            }
            if (wear.m_bounds != null && !remade)                           // made again (a new pose): boxes made anew there
            {
                float nowSeen = SupportAt(root, wear, own, Vector3.zero, Quaternion.identity, true), afterSeen = SupportAt(root, wear, own, delta, q, true);
                if (SupportMath.Refuse(nowSeen, afterSeen, least))
                {
                    why = $"too weakly held there until the move is applied ({afterSeen:0} of the {least:0} it needs, as the game counts it): " +
                          "apply the steps so far first";
                    return false;
                }
            }
            return true;
        }

        /// <summary>The support the game would find for the first sign moved by delta and turned by q about its position:
        /// its colliders' boxes grown by 0.3 m (WearNTear.SetupColliders) moved along, or as the game keeps them (seen:
        /// made when the sign was, not moved by steps), and its centre of mass moved along.</summary>
        static float SupportAt(Sign root, WearNTear wear, HashSet<Collider> own, Vector3 delta, Quaternion q, bool seen)
        {
            Vector3 pivot = root.transform.position;
            var boxes = new List<KeyValuePair<Vector3, Vector3>>();
            var turns = new List<Quaternion>();
            if (seen)
                foreach (WearNTear.BoundData b in wear.m_bounds)
                {
                    boxes.Add(new KeyValuePair<Vector3, Vector3>(b.m_pos, b.m_size));
                    turns.Add(b.m_rot);
                }
            else
                foreach (Collider c in root.GetComponentsInChildren<Collider>(true))
                {
                    if (c.isTrigger || c.attachedRigidbody != null) continue;
                    Vector3 pos, size;
                    Quaternion rot;
                    if (c is BoxCollider b)
                    {
                        rot = b.transform.rotation;
                        pos = b.transform.position + b.transform.TransformVector(b.center);
                        size = Vector3.Scale(b.transform.lossyScale, b.size);
                    }
                    else
                    {
                        rot = Quaternion.identity;
                        pos = c.bounds.center;
                        size = c.bounds.size;
                    }
                    boxes.Add(new KeyValuePair<Vector3, Vector3>(pivot + q * (pos - pivot) + delta, (size + Vector3.one * 0.3f) * 0.5f));
                    turns.Add(q * rot);
                }
            Vector3 com = pivot + q * (wear.GetCOM() - pivot) + delta;
            wear.GetMaterialProperties(out float most, out _, out float sideways, out float downwards);
            if (supportMask == 0) supportMask = LayerMask.GetMask("piece", "Default", "static_solid", "Default_small", "terrain");
            int mask = WearNTear.s_rayMask != 0 ? WearNTear.s_rayMask : supportMask;
            int terrain = LayerMask.NameToLayer("terrain");
            var found = new List<SupportMath.Supporter>();
            for (int i = 0; i < boxes.Count; i++)
            {
                int n = Physics.OverlapBoxNonAlloc(boxes[i].Key, boxes[i].Value, supportHits, turns[i], mask);
                for (int k = 0; k < n; k++)
                {
                    Collider o = supportHits[k];
                    if (o.attachedRigidbody != null || o.isTrigger || own.Contains(o)) continue;
                    if (o.gameObject.layer == terrain) return most;
                    WearNTear w = o.GetComponentInParent<WearNTear>();
                    if (w == null) return most;                             // a rock, a tree: the most
                    if (!w.m_supports) continue;
                    float d = Vector3.Distance(com, w.GetCOM()) + 0.1f, d2 = Vector3.Distance(com, w.transform.position) + 0.1f;
                    if (d2 < d && !wear.m_forceCorrectCOMCalculation) d = d2;
                    Vector3 at = WearNTear.FindSupportPoint(com, w, o) - com;
                    found.Add(new SupportMath.Supporter { Support = w.GetSupport(), Distance = d, Px = at.x, Py = at.y, Pz = at.z });
                }
            }
            return (float)SupportMath.Support(found, most, sideways, downwards);
        }

        /// <summary>Every frame (Plugin.Update): checks a write once after a while, and moves signs whose ZDO another
        /// player moved.</summary>
        public static void Step(float now)
        {
            if (verify != null && now >= verifyAt) Verify();             // moves wait for Apply (Write) or Undo (Revert)
            if (now < nextFollow) return;
            nextFollow = now + FollowEvery;
            // the steps not applied weighed again: a piece built under the sign, or taken away, changes what holds it
            // (1.0.2, review: Apply stayed refused until another step)
            if (pending != null && pending.Count > 0 && pending[0] && SignFinder.Zdo(pending[0]) != null)
            {
                // its own guard: three failures in a row switch this whole step off (Plugin.Safe), Follow with it
                try { Weigh(pending[0], pending); }
                catch (Exception e) { Plugin.Log.LogDebug($"weighing the steps not applied: {e.Message}"); }
            }
            else if (pending != null && (pending.Count == 0 || !pending[0]))
            {                                                               // its first sign unloaded, removed or the world
                Revert();                                                   // left: the steps go, unsent (as Drop); copies still
            }                                                               // here go back to their ZDO, reboxed (1.0.2, review)
            Follow(now);
        }

        /// <summary>Two seconds after a write: a sign whose ZDO no longer holds the position written (another game wrote
        /// the same ZDO at that moment and the server kept its data over ours) is written again, once.</summary>
        static void Verify()
        {
            List<KeyValuePair<Sign, Pose>> check = verify;
            verify = null;
            int again = 0;
            foreach (KeyValuePair<Sign, Pose> e in check)
            {
                Sign s = e.Key;
                ZDO zdo = s ? SignFinder.Zdo(s) : null;
                if (zdo == null) continue;
                bool moved = (zdo.GetPosition() - e.Value.position).sqrMagnitude > 1e-8f;
                bool turned = Quaternion.Angle(zdo.GetRotation(), e.Value.rotation) > 0.05f;   // a lost turn too: a stack
                if (!moved && !turned) continue;                                              // half turned falls apart
                if (!zdo.IsOwner()) continue;                               // another player moved it since: theirs stands (0.20.130)
                s.m_nview.ClaimOwnership();
                zdo.SetPosition(e.Value.position);
                if (turned) zdo.SetRotation(e.Value.rotation);
                s.transform.SetPositionAndRotation(e.Value.position, e.Value.rotation);
                again++;
            }
            if (again > 0) Plugin.Log.LogDebug($"{again} sign(s) lost their move to another writer: written again");
        }

        /// <summary>Writes the moves and turns made so far to the ZDOs (owned first, as vanilla does before any change).
        /// Anything that changes a stack calls it first, so it starts from where the signs are. Refused, changing nothing,
        /// while the game would not hold the stack up there (Unheld): applied, the game would break it.</summary>
        public static void Write()
        {
            if (pending == null) return;
            if (Paces && pending.Count > 0 && pending[0]) Weigh(pending[0], pending);   // as it stands now, not up to half a second ago (review)
            if (unheld != null) throw new InvalidOperationException("no support: " + unheld);
            List<Sign> stack = pending;
            Vector3 d = pendingDelta;
            float turn = pendingTurn;
            pending = null;
            pendingDelta = Vector3.zero;
            pendingTurn = 0f;
            Stopped();
            verify = new List<KeyValuePair<Sign, Pose>>(stack.Count);
            verifyAt = Time.unscaledTime + VerifyAfter;
            foreach (Sign s in stack)
            {
                ZDO zdo = SignFinder.Zdo(s);
                if (zdo == null) continue;
                s.m_nview.ClaimOwnership();
                zdo.SetPosition(s.transform.position);
                if (Mathf.Abs(turn) > 1e-4f) zdo.SetRotation(s.transform.rotation);
                verify.Add(new KeyValuePair<Sign, Pose>(s, new Pose(s.transform.position, s.transform.rotation)));
            }
            Rebox(stack);
            Sign root = stack.Count > 0 ? stack[0] : null;
            ZDO rz = SignFinder.Zdo(root);
            if (rz == null) return;
            rz.Set(NudgeKey, rz.GetVec3(NudgeKey, Vector3.zero) + d);
            if (Mathf.Abs(turn) > 1e-4f) rz.Set(TurnKey, Mathf.DeltaAngle(0f, rz.GetFloat(TurnKey, 0f) + turn));
            SignPose.MoveBase(root, d, turn);
        }

        /// <summary>Does this stack have moves or turns not applied yet (only this player sees them)?</summary>
        public static bool HasPending(Sign root) => pending != null && root && pending.Contains(root);

        /// <summary>Undo: the moves and turns not applied yet are taken back; the signs go back to where their ZDOs say
        /// (the last applied place), nothing is written.</summary>
        public static void Revert()
        {
            if (pending == null) return;
            List<Sign> stack = pending;
            pending = null;
            pendingDelta = Vector3.zero;
            pendingTurn = 0f;
            Stopped();
            foreach (Sign s in stack)
            {
                ZDO zdo = s ? SignFinder.Zdo(s) : null;
                if (zdo == null) continue;
                s.transform.SetPositionAndRotation(zdo.GetPosition(), zdo.GetRotation());
            }
            Rebox(stack);
        }

        /// <summary>The game makes a piece's support boxes once (WearNTear.SetupColliders, at its first support check) and
        /// keeps them: boxes made while a step was not applied stayed metres off after an undo, and the sign broke
        /// (0.20.130, review). Dropped here, the next check makes them where the signs stand.</summary>
        static void Rebox(List<Sign> stack)
        {
            foreach (Sign s in stack) Rebox(s);
        }

        static void Rebox(Sign s)
        {
            WearNTear w = s ? s.GetComponent<WearNTear>() : null;
            if (w) w.m_colliders = null;
        }

        /// <summary>Writes the moves not applied first when they are this stack's (anything that changes it starts from
        /// where its signs are); another stack's stay its own, to apply or undo (0.20.130, review: a picture finishing on
        /// one sign wrote the steps taken meanwhile on another, past Undo).</summary>
        public static void WriteFor(Sign any)
        {
            if (pending == null || !any) return;
            if (pending.Contains(any) || pending.Contains(SignFinder.Root(any))) Write();
        }

        /// <summary>The signs are being removed: if they hold the moves not applied, those go with them, unsent, and the
        /// stack's other signs (a delete of only some, or one a ward kept) go back where they stand for everyone (1.0.2,
        /// review: left displaced and no longer paused, the game could break one where nothing held it).</summary>
        public static void Drop(ICollection<Sign> signs)
        {
            if (pending == null || signs == null) return;
            foreach (Sign s in signs)
                if (pending.Contains(s))
                {
                    Revert();
                    return;
                }
        }

        /// <summary>Reverts unless any belongs to the stack being moved (another sign was selected).</summary>
        public static void RevertUnless(Sign any)
        {
            if (pending == null || (any && (pending.Contains(any) || pending.Contains(SignFinder.Root(any))))) return;
            Revert();
        }

        /// <summary>A sign SignLink moved or turned (Write sets these on the stack's first sign), marked or not: a plain
        /// short-text sign moved with Move the sign has no SignLink mark.</summary>
        static bool Moved(ZDO zdo) =>
            !float.IsNaN(zdo.GetVec3(NudgeKey, NaNVector).x) || !float.IsNaN(zdo.GetFloat(TurnKey, float.NaN));

        static readonly Vector3 NaNVector = new Vector3(float.NaN, 0f, 0f);

        static bool Same(List<Sign> a, List<Sign> b)
        {
            if (a.Count != b.Count) return false;
            for (int i = 0; i < a.Count; i++)
                if (!ReferenceEquals(a[i], b[i])) return false;
            return true;
        }

        /// <summary>Signs placed where their ZDO no longer is (moved or turned by another player's SignLink) go there,
        /// their support boxes made anew where they stand (Rebox, as for this player's own moves; 0.20.132, review: the
        /// game kept the boxes where the sign was built). Only with other players around, and only signs whose ZDO
        /// changed since the last look (a move raises the revision).</summary>
        static void Follow(float now)
        {
            if (!ZNet.instance || ZNet.instance.GetPeers().Count == 0) return;
            if (now >= nextScan)
            {
                nextScan = now + 2f;
                known = SignFinder.Loaded();
                SignFinder.Prune(seen, ref seenGone);                       // destroyed signs go (0.20.132, review)
                if (seen.Count > 4000) seen.Clear();
            }
            foreach (Sign s in known)
            {
                if (!s || moving.Contains(s)) continue;
                ZDO zdo = SignFinder.Zdo(s);
                if (zdo == null || !(SignMarks.IsSignLinks(zdo) || Moved(zdo))) continue;   // other mods may move their own pieces
                if (seen.TryGetValue(s, out uint rev) && rev == zdo.DataRevision) continue;
                seen[s] = zdo.DataRevision;
                bool moved = false;
                Vector3 p = zdo.GetPosition();
                if ((p - s.transform.position).sqrMagnitude > 1e-8f)
                {
                    s.transform.position = p;
                    moved = true;
                }
                Quaternion r = zdo.GetRotation();
                if (Quaternion.Angle(r, s.transform.rotation) > 0.05f)
                {
                    s.transform.rotation = r;
                    moved = true;
                }
                if (moved) Rebox(s);
            }
        }
    }
}
