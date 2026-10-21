using System;
using System.Collections.Generic;
using PetyusPolisz.Kit;
using UnityEngine;

namespace SignLink.Signs
{
    /// <summary>Finding signs: every loaded one (the registry), the one under the crosshair, by id, and the stack a
    /// sign belongs to (the copies behind a picture's first sign), with whether a stack has fully arrived.</summary>
    internal static class SignFinder
    {
        /// <summary>Signs turned the same way, at most this far apart sideways and StackDepth along the normal, are
        /// candidates for one stack: copies of one sign that together show a picture too big for one (SignBuilder.Clone
        /// puts them 0.5 mm apart, so 25 cm holds 500 of them).</summary>
        public const float StackRadius = 0.02f, StackDepth = 0.25f;

        /// <summary>Copies stand this close behind each other (0.3-0.5 mm); a sign placed by hand stands at least 4.45 cm
        /// from any other (placement pushes it out of the collider, Player.PlacePiece), so a gap over MaxGap ends a stack.</summary>
        public const float MaxGap = 0.002f;

        /// <summary>On a stack's first sign: how many signs the stack has (written with every rebuild or content change),
        /// so a client that sees fewer knows the rest are still on their way (IsLoaded).</summary>
        public static readonly int CountKey = "signlink_count".GetStableHashCode();

        public static ZDO Zdo(Sign s)
        {
            ZNetView nv = s ? s.m_nview : null;
            return nv != null && nv.IsValid() ? nv.GetZDO() : null;
        }

        /// <summary>The ZDO id ("user:id"). Saves keep no ids: they change every session.</summary>
        public static string Id(Sign s) => Zdo(s)?.m_uid.ToString();

        static readonly HashSet<Sign> registry = new HashSet<Sign>();
        /// <summary>When each loaded sign awoke (IsLoaded: a stack short of its count is loading only for a while).</summary>
        static readonly Dictionary<Sign, float> awoke = new Dictionary<Sign, float>();
        /// <summary>How long a stack with fewer signs than its count may still be loading; after that the signs found are
        /// the stack (a copy hammered away outside SignLink: the next write corrects the count).</summary>
        const float ArriveSeconds = 10f;
        static Sign[] loaded = new Sign[0];
        static int loadedFrame = -1;
        static bool dirty;

        /// <summary>A sign that just awoke (Patches.SignRegistryPatch).</summary>
        public static void Register(Sign s)
        {
            if (!s) return;
            registry.Add(s);
            awoke[s] = Time.unscaledTime;
            dirty = true;
        }

        /// <summary>Every sign already in the world into the registry, as long arrived: a plugin loaded again in a running
        /// game (ScriptEngine's reload, 0.20.135, the author: now let us build the OnDestroy solution mentioned) patches Sign.Awake after these signs awoke.</summary>
        public static void Seed()
        {
            if (!Plugin.Patched(typeof(Patches.SignRegistryPatch))) return;
            foreach (Sign s in UnityEngine.Object.FindObjectsByType<Sign>(FindObjectsSortMode.None))
                if (s && registry.Add(s))
                {
                    awoke[s] = -1000f;
                    dirty = true;
                }
        }

        /// <summary>Every loaded sign: the registry (a scene scan only if the registry patch did not apply). The same
        /// array within a frame unless a sign awoke meanwhile (the copies a placement makes are in the list its screen
        /// reads a moment later); callers that keep it longer check each sign still exists.</summary>
        public static Sign[] Loaded()
        {
            Sweep();
            if (!Plugin.Patched(typeof(Patches.SignRegistryPatch))) return UnityEngine.Object.FindObjectsByType<Sign>(FindObjectsSortMode.None);
            return loaded;
        }

        /// <summary>Rises whenever the registry finds signs gone (destroyed: unloaded with their zone, removed), so the
        /// caches keyed by sign drop theirs (Prune). A destroyed sign stays a C# object while anything holds it, and with
        /// it its text's TMP buffers (megabytes for a picture's band): caches that kept every sign met piled up some 70-80
        /// MB with every return to a big base until their caps cleared them (0.20.132, review). Without the registry
        /// patch it rises every 2 s (nothing tells then which signs went).</summary>
        public static int Gone { get; private set; }
        static float fallbackGoneAt;

        /// <summary>The registry without its destroyed signs, and the list Loaded returns: at most once a frame unless a
        /// sign awoke meanwhile.</summary>
        static void Sweep()
        {
            if (!Plugin.Patched(typeof(Patches.SignRegistryPatch)))
            {
                if (Time.unscaledTime >= fallbackGoneAt)
                {
                    fallbackGoneAt = Time.unscaledTime + 2f;
                    Gone++;
                }
                return;
            }
            if (Time.frameCount == loadedFrame && !dirty) return;            // a sign that just awoke (a copy made this frame) is in the next list
            loadedFrame = Time.frameCount;
            int gone = registry.RemoveWhere(s => !s);
            if (gone > 0)
            {
                Gone++;
                foreach (Sign d in new List<Sign>(awoke.Keys)) if (!d) awoke.Remove(d);
            }
            if (gone > 0 || dirty || loaded.Length != registry.Count)
            {
                loaded = new Sign[registry.Count];
                registry.CopyTo(loaded);
                dirty = false;
            }
        }

        /// <summary>Drops the entries of destroyed signs from a cache keyed by sign, when signs went since the cache's
        /// last look (seen: its own copy of Gone): on its rescan, or before it adds an entry (0.20.132, review).</summary>
        public static void Prune<T>(Dictionary<Sign, T> cache, ref int seen)
        {
            Sweep();
            if (seen == Gone) return;
            seen = Gone;
            List<Sign> dead = null;
            foreach (Sign s in cache.Keys)
                if (!s) (dead ?? (dead = new List<Sign>())).Add(s);
            if (dead != null) foreach (Sign s in dead) cache.Remove(s);
        }

        public static Dictionary<string, Sign> ById()
        {
            var d = new Dictionary<string, Sign>();
            foreach (Sign s in Loaded())
            {
                string id = Id(s);
                if (id != null) d[id] = s;
            }
            return d;
        }

        /// <summary>The sign and every copy stacked on it: one chain along the sign's normal (neighbours within MaxGap,
        /// the same prefab, the same rotation within a degree, within StackRadius sideways), rearmost first: the
        /// original the copies were made from. From a copy the chain reaches back to its first sign. A sign placed by
        /// hand on a stack's spot, anyone's, is never in it: it cannot stand within MaxGap of another sign.</summary>
        public static List<Sign> Stack(Sign s) => Stack(s, Loaded());

        /// <summary>The first sign of s's stack (s itself when alone).</summary>
        public static Sign Root(Sign s)
        {
            List<Sign> stack = Stack(s);
            return stack.Count > 0 ? stack[0] : s;
        }

        /// <summary>The stacks among these signs, each its first sign first, in the order of the signs: a sign's
        /// stack-mates are looked for among the signs in its own and the neighbouring metre cells (they lie within a few
        /// centimetres of each other), so a base of 400 signs is not 400 x 400 distance tests (Signs nearby, Show all).
        /// seed: only the stacks of the signs it takes (Show all: SignLink's marked ones, 0.20.132, review), their
        /// stack-mates still looked for among all the signs.</summary>
        public static List<List<Sign>> Stacks(List<Sign> signs, Func<Sign, bool> seed = null)
        {
            var cells = new Dictionary<Vector3Int, List<Sign>>();
            foreach (Sign s in signs)
            {
                Vector3Int c = Vector3Int.FloorToInt(s.transform.position);
                if (!cells.TryGetValue(c, out List<Sign> cell)) cells[c] = cell = new List<Sign>();
                cell.Add(s);
            }
            var stacks = new List<List<Sign>>();
            var seen = new HashSet<Sign>();
            var near = new List<Sign>();
            foreach (Sign s in signs)
            {
                if (seen.Contains(s) || (seed != null && !seed(s))) continue;
                near.Clear();
                Vector3Int c = Vector3Int.FloorToInt(s.transform.position);
                for (int dx = -1; dx <= 1; dx++)
                    for (int dy = -1; dy <= 1; dy++)
                        for (int dz = -1; dz <= 1; dz++)
                            if (cells.TryGetValue(c + new Vector3Int(dx, dy, dz), out List<Sign> cell)) near.AddRange(cell);
                List<Sign> stack = Stack(s, near);
                if (stack.Count == 0) stack = new List<Sign> { s };
                foreach (Sign o in stack) seen.Add(o);
                stacks.Add(stack);
            }
            return stacks;
        }

        /// <summary>The same among given signs (callers that look at many stacks list the signs once: finding every
        /// loaded sign is the expensive part).</summary>
        public static List<Sign> Stack(Sign s, IList<Sign> among)
        {
            var chain = new List<Sign>();
            ZDO sz = Zdo(s);
            if (sz == null) return chain;
            Transform t = s.transform;
            int prefab = sz.GetPrefab();
            var near = new List<KeyValuePair<float, Sign>>();
            foreach (Sign o in among)
            {
                ZDO oz = o ? Zdo(o) : null;
                if (oz == null || oz.GetPrefab() != prefab || Quaternion.Angle(o.transform.rotation, t.rotation) >= 1f) continue;
                Vector3 d = o.transform.position - t.position;
                float along = Vector3.Dot(d, t.forward);
                if (Mathf.Abs(along) < StackDepth && (d - along * t.forward).magnitude < StackRadius)
                    near.Add(new KeyValuePair<float, Sign>(along, o));
            }
            near.Sort((a, b) => a.Key.CompareTo(b.Key));
            int me = near.FindIndex(e => ReferenceEquals(e.Value, s));
            if (me < 0) return new List<Sign> { s };
            var along2 = new float[near.Count];
            for (int i = 0; i < along2.Length; i++) along2[i] = near[i].Key;
            foreach (int i in StackMath.Chain(along2, me, MaxGap)) chain.Add(near[i].Value);
            return chain;
        }

        /// <summary>A loaded sign outside this stack that would read as one stack with it if its first sign stood at pos
        /// turned rot (Stack's tests: the same prefab, the same rotation within a degree, within StackRadius sideways and
        /// StackDepth along the normal), or null (SignMove.RequireSpot, 0.20.130).</summary>
        public static Sign Intruder(List<Sign> stack, Vector3 pos, Quaternion rot)
        {
            Sign root = stack.Count > 0 ? stack[0] : null;
            ZDO rz = Zdo(root);
            if (rz == null) return null;
            int prefab = rz.GetPrefab();
            Vector3 forward = rot * Vector3.forward;
            foreach (Sign o in Loaded())
            {
                if (!o || stack.Contains(o)) continue;
                ZDO oz = Zdo(o);
                if (oz == null || oz.GetPrefab() != prefab || Quaternion.Angle(o.transform.rotation, rot) >= 1f) continue;
                Vector3 d = o.transform.position - pos;
                float along = Vector3.Dot(d, forward);
                if (Mathf.Abs(along) < StackDepth && (d - along * forward).magnitude < StackRadius) return o;
            }
            return null;
        }

        static readonly List<ZDO> near = new List<ZDO>();

        /// <summary>Throws while a sign of the stack at s exists in the world but not yet in the game: it would be left
        /// out of a change, or left behind. Two things say so (IsLoaded): the first sign's count is more than the signs
        /// found (copies still on their way from the server, which sends them one by one), and a ZDO of the prefab
        /// within 30 cm that has no object yet (ZNetScene makes only so many a frame, right after arriving).</summary>
        public static void RequireLoaded(Sign s)
        {
            if (!IsLoaded(s)) throw new InvalidOperationException(Lang.T("the sign's stack is still loading (its copies arrive one by one): try again in a moment"));
        }

        /// <summary>Is every sign of the stack at s in the game (RequireLoaded without the throw)? Zones this client
        /// never instantiates (outside its active area) do not count as loading.</summary>
        public static bool IsLoaded(Sign s)
        {
            ZDO zdo = Zdo(s);
            if (zdo == null || ZDOMan.instance == null || !ZNetScene.instance || ZoneSystem.instance == null) return true;
            List<Sign> stack = Stack(s);
            Sign root = stack.Count > 0 ? stack[0] : s;
            ZDO rz = Zdo(root);
            int count = rz != null ? rz.GetInt(CountKey, 0) : 0;
            if (rz != null && rz.GetInt(SignBuilder.CopyKey, 0) != 0)       // a copy leads what arrived: its first sign may be
            {                                                               // on its way (the server sends the nearest first;
                float newest = 0f;                                          // 0.20.130, review), unless that was a while ago
                foreach (Sign o in stack) if (awoke.TryGetValue(o, out float t)) newest = Mathf.Max(newest, t);
                if (Time.unscaledTime - newest < ArriveSeconds) return false;
            }
            if (count > 0 && stack.Count < count)                           // copies still on their way, unless they are gone:
            {                                                               // the newest sign of the stack awoke a while ago
                float newest = 0f;
                foreach (Sign o in stack) if (awoke.TryGetValue(o, out float t)) newest = Mathf.Max(newest, t);
                if (Time.unscaledTime - newest < ArriveSeconds) return false;
            }
            Vector3 p = root.transform.position;
            const float reach = 0.3f;
            var zones = new List<Vector2s>(4);
            for (int i = 0; i < 4; i++)
            {
                Vector2s z = ZoneSystem.GetZone(p + new Vector3((i & 1) == 0 ? -reach : reach, 0f, (i & 2) == 0 ? -reach : reach));
                if (!zones.Contains(z)) zones.Add(z);
            }
            near.Clear();
            foreach (Vector2s z in zones) ZDOMan.instance.FindSectorObjects(z, new SimulationDistance(0, 0), near);
            int prefab = zdo.GetPrefab();
            bool loaded = true;
            foreach (ZDO o in near)
            {
                if (o.GetPrefab() != prefab || (o.GetPosition() - p).sqrMagnitude >= reach * reach) continue;
                if (ZNetScene.instance.FindInstance(o) != null || ZNetScene.instance.OutsideActiveArea(o.GetPosition())) continue;
                loaded = false;
                break;
            }
            near.Clear();
            return loaded;
        }
    }
}
