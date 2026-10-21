using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using PetyusPolisz.Kit;
using SignLink.Pictures;
using SignLink.Signs;
using UnityEngine;

namespace SignLink.Animation
{
    /// <summary>Animations saved on the sign itself: the frames go into a byte array on the sign's ZDO, next to its text
    /// (AnimBlob; custom keys persist in saves and on dedicated servers, docs/mod-standards.md "Stored animations").
    /// Every SignLink client in range plays them locally on the shared world clock (ZNet.GetTimeSeconds), so everyone
    /// with the mod sees the same frame; vanilla clients see the sign's text, the first frame. They survive logout and
    /// restarts and cost no network per frame: the blob travels once, with the sign.
    /// A blob from another player's sign is judged on a worker before it plays (unpacked within AnimBlob's caps, every
    /// frame's glyphs counted): one that would take over MaxFrameMs to show a frame, or over a tenth of every second
    /// (the budget of docs/limits.md), is refused and its first frame stays as a still; one that turns out slow while
    /// playing (two frames in a row over StopMs, or three of the last ten over SlowMsPerSecond) stops. All playing frames together
    /// stay under MaxChars, the nearest signs first; with playback off nothing is kept.</summary>
    internal sealed class StoredAnims
    {
        public static readonly int Key = "signlink_anim".GetStableHashCode();
        /// <summary>Moving switched off on the Playing tab, for everyone (2026-10-16, the author: not a Delete but the
        /// Moving switch, and it stays in the list to switch back on): the frames stay on the sign and no SignLink game
        /// plays them, the sign showing its first frame, until Moving goes on again. New frames (Store) or new content
        /// (ClearBlob) clear it.</summary>
        public static readonly int StillKey = "signlink_anim_still".GetStableHashCode();
        public const int MaxBytes = AnimBlob.MaxBytes;
        const double MaxFrameMs = 100, MaxMsPerSecond = 100, StopMs = 150, SlowMsPerSecond = 150, GlyphMs = Pictures.SaveCost.GlyphSeconds * 1000;
        /// <summary>All stored animations together (0.20.151, review: each was judged alone, so ten light ones near each
        /// other took a second of every second, and on the world clock they changed frame in the same Unity frame): at most
        /// BudgetMsPerSecond of every second, and StepMs in one Unity frame (the rest show at the next), the nearest first.
        /// StepMs is a small share of a frame (1.0.1, multiplayer test: at 50 ms, 25 moving pieces spent the whole second's
        /// budget in the first two frames, two 60-70 ms hitches every second); at least one sign steps each frame.</summary>
        const double BudgetMsPerSecond = 100, StepMs = 6;
        double spentMs;
        float spentSince;
        readonly Nearest nearest = new Nearest();

        sealed class Nearest : IComparer<Playing>
        {
            public Vector3 Me;
            float Far(Playing a) => a.Sign ? (a.Sign.transform.position - Me).sqrMagnitude : float.MaxValue;
            public int Compare(Playing x, Playing y) => Far(x).CompareTo(Far(y));
        }
        const int SlowWindow = 10, SlowFrames = 3, JudgesPerSecond = 2;
        const long MaxChars = 64L << 20;

        sealed class Playing
        {
            public Sign Sign;
            public string[] Frames;
            public float Fps;
            public uint Revision;
            public int Hash;
            public long Chars;
            public int Index = -1, SlowAt;
            public readonly bool[] Slow = new bool[SlowWindow];
            /// <summary>The last frame shown took over StopMs: a second one in a row stops it (one alone may be the game's
            /// own hitch, a collection or a load).</summary>
            public bool OverLast;
        }

        sealed class Judged
        {
            public string[] Frames;
            public float Fps;
            public long Chars;
            public string Why;
        }

        readonly Dictionary<Sign, Playing> playing = new Dictionary<Sign, Playing>();
        readonly Dictionary<ZDOID, int> rejected = new Dictionary<ZDOID, int>();          // the blob (its hash) not played
        readonly Dictionary<ZDOID, KeyValuePair<uint, int>> hashes = new Dictionary<ZDOID, KeyValuePair<uint, int>>();   // revision -> hash
        readonly HashSet<ZDOID> judging = new HashSet<ZDOID>();
        /// <summary>Animations paused on this computer (the Playing tab's Pause): the sign shows its first frame, the
        /// saved text, until Play; kept while the sign unloads and comes back (by its ZDO).</summary>
        readonly HashSet<ZDOID> paused = new HashSet<ZDOID>();
        /// <summary>Animations left out for room (EnforceCap): the blob (its hash) and the characters it unpacked to.
        /// Without it the next scan unpacked them again (up to MaxReadChars each, much garbage) only to forget them, every
        /// second, a round a hostile client could start with five big blobs (0.20.132, review).</summary>
        readonly Dictionary<ZDOID, KeyValuePair<int, long>> evicted = new Dictionary<ZDOID, KeyValuePair<int, long>>();
        /// <summary>Signs are judged within PlayDistance and this many metres (ready as the player walks in), and one left
        /// out for room again once this much nearer than the farthest one playing (not back and forth at the edge).</summary>
        const float JudgeMargin = 16f, Nearer = 4f;
        readonly List<Playing> due = new List<Playing>();
        float nextScan, judgeSecond;
        int judgesThisSecond;

        public int Count => playing.Count;

        public bool Drives(Sign s) => s && playing.ContainsKey(s);

        public static byte[] Pack(IList<string> frames, float fps) => AnimBlob.Pack(frames, fps);

        public static bool TryUnpack(byte[] blob, out string[] frames, out float fps) => AnimBlob.TryUnpack(blob, out frames, out fps);

        public static byte[] Blob(Sign s) => SignFinder.Zdo(s)?.GetByteArray(Key);

        /// <summary>Saves the frames on the sign (its text becomes the first frame, for vanilla players). Like any sign
        /// edit it needs ward access; the blob and the text together must stay under MaxBytes, and the frames under
        /// AnimBlob.MaxChars characters in all (what a game reads back plays, 0.20.132).</summary>
        public void Store(Sign s, IList<string> frames, float fps, string name = null)
        {
            if (frames.Count == 0) throw new ArgumentException(Lang.T("no frames"));
            if (fps <= 0 || fps > 60) throw new ArgumentException(Lang.T("fps must be in (0, 60]"));
            if (!AnimBlob.Fits(frames, out long chars))
                throw new ArgumentException(Lang.F("{0} characters in all: over the {1} a saved animation holds (no game would play it)", chars, AnimBlob.MaxChars));
            SignText.RequireEdit(s);
            byte[] blob = AnimBlob.Pack(frames, fps);
            int total = blob.Length + Encoding.UTF8.GetByteCount(frames[0]);
            if (total > MaxBytes)
                throw new ArgumentException(Lang.F("{0} KB with the first frame as text: over {1} KB, too big to send in multiplayer", total / 1024, MaxBytes / 1024));
            SignText.RequireRoom(s, Pictures.SaveCost.Bytes(new[] { frames[0] }, blob.Length), Lang.T("this animation"));   // the multiplayer limits
            Sign root = SignBuilder.SetContent(s, new[] { frames[0] }, name)[0];   // an animation lives on one sign
            ZDO rootZdo = SignFinder.Zdo(root);
            rootZdo.Set(Key, blob);
            if (rootZdo.GetInt(StillKey) != 0) rootZdo.Set(StillKey, 0);    // new frames move, for everyone
            paused.Remove(rootZdo.m_uid);                                   // new frames play (0.20.189, review)
            nextScan = 0;
        }

        /// <summary>Does this sign carry frames (moving or not)?</summary>
        public static bool HasFrames(ZDO zdo)
        {
            byte[] blob = zdo?.GetByteArray(Key);
            return blob != null && blob.Length > 0;
        }

        /// <summary>Is Moving off on this sign's frames, for everyone? The key holds the mark of the frames switched off
        /// (Mark), not a plain flag (1.0.2, review: a 1.0.1 player, who knows no such key, putting new frames on a sign once
        /// switched off left the flag on, and every 1.0.2 player saw the new animation frozen): other frames move.</summary>
        public static bool Still(ZDO zdo)
        {
            int mark = zdo != null ? zdo.GetInt(StillKey) : 0;
            if (mark == 0) return false;
            byte[] blob = zdo.GetByteArray(Key);
            return blob != null && blob.Length > 0 && Mark(blob) == mark;
        }

        /// <summary>A frames blob's mark: FNV-1a over its length and its first and last 4 KB (cheap on every scan, and
        /// any other frames differ there), never 0 (no mark) or 1 (the plain flag of a test build).</summary>
        static int Mark(byte[] blob)
        {
            uint h = 2166136261;
            void Add(byte b) { h ^= b; h *= 16777619; }
            for (int i = 0; i < 4; i++) Add((byte)(blob.Length >> (8 * i)));
            int head = System.Math.Min(4096, blob.Length);
            for (int i = 0; i < head; i++) Add(blob[i]);
            for (int i = System.Math.Max(head, blob.Length - 4096); i < blob.Length; i++) Add(blob[i]);
            int m = (int)h;
            return m == 0 || m == 1 ? 2 : m;
        }

        /// <summary>The Playing tab's Moving switch, for everyone: off keeps the frames on the sign and shows its first frame
        /// (as players without SignLink see it); on plays them again. A piece from the art library says so in its recipe
        /// too, so 2 Content's Moving switch shows the same. Like any sign edit it needs ward access.</summary>
        public string SetMoving(Sign s, bool on)
        {
            ZDO zdo = s ? SignFinder.Zdo(s) : null;
            if (zdo == null) throw new InvalidOperationException(Lang.T("the sign is gone (unloaded or removed)"));
            if (!HasFrames(zdo)) return Lang.T("that sign has no saved animation any more.");
            SignText.RequireEdit(s);
            s.m_nview.ClaimOwnership();
            zdo.Set(StillKey, on ? 0 : Mark(zdo.GetByteArray(Key)));     // these frames, not any later ones (Still)
            SignRecipe r = SignRecipe.Parse(SignMarks.Source(s));
            if (r.Kind == SignRecipe.Kinds.Art && r.Still == on)
            {
                r.Still = !on;
                SignMarks.SetSource(s, r.ToString());
            }
            if (on)
            {
                rejected.Remove(zdo.m_uid);                                 // judged again
                nextScan = 0;
                return "it moves again, for every SignLink player.";
            }
            Forget(s);                                                      // its first frame shows
            return "it moves no more, for anyone: its sign shows the first frame; Moving starts it again.";
        }

        public void Step(float now)
        {
            if (!ZNet.instance)                                             // the world left: its frames go (0.20.130, review),
            {                                                               // and what was kept by ZDO (1.0.2, review: another
                if (playing.Count > 0) playing.Clear();                     // world reuses the ids)
                if (rejected.Count + hashes.Count + paused.Count + evicted.Count > 0)
                {
                    rejected.Clear();
                    hashes.Clear();
                    paused.Clear();
                    evicted.Clear();
                }
                return;
            }
            if (!PluginConfig.PlayAnimations.Value)
            {
                if (playing.Count > 0) ForgetAll();                         // frame 0 shows, as vanilla players see it
                return;
            }
            if (now >= nextScan) Scan(now);
            if (playing.Count == 0) return;
            Player p = Player.m_localPlayer;
            float far = PluginConfig.PlayDistance.Value;
            double t = ZNet.instance.GetTimeSeconds();
            due.Clear();
            due.AddRange(playing.Values);
            if (p && due.Count > 1)                                         // the nearest first, when the budget runs out
            {
                nearest.Me = p.transform.position;                          // one comparer, no closure a frame (0.20.152, review)
                due.Sort(nearest);
            }
            if (now - spentSince >= 1f)
            {
                spentSince = now;
                spentMs = 0;
            }
            double thisStep = 0;
            foreach (Playing a in due)
            {
                if (spentMs >= BudgetMsPerSecond || thisStep >= StepMs) break;   // the rest wait: a later frame or second
                ZDO az = a.Sign ? SignFinder.Zdo(a.Sign) : null;
                if (az == null || paused.Contains(az.m_uid)) continue;
                if (!a.Sign.m_isViewable) continue;               // a muted or blocked author: vanilla shows a notice
                if (p && Vector3.Distance(p.transform.position, a.Sign.transform.position) > far) continue;
                int index = (int)((long)Math.Floor(t * a.Fps) % a.Frames.Length);
                if (index == a.Index) continue;
                a.Index = index;
                try
                {
                    double ms = SignText.Apply(a.Sign, a.Frames[index], local: true);
                    spentMs += ms;
                    thisStep += ms;
                    // two frames in a row (0.20.188: one 242 ms frame stopped a light piece for good, a hitch of the game's own
                    // by its look: the piece had been judged under MaxFrameMs before it played)
                    bool over = ms > StopMs;
                    if (over && a.OverLast) throw new InvalidOperationException($"two frames in a row took over {StopMs:0} ms to show (the last {ms:0} ms)");
                    a.OverLast = over;
                    a.Slow[a.SlowAt] = ms * a.Fps > SlowMsPerSecond;
                    a.SlowAt = (a.SlowAt + 1) % SlowWindow;
                    int slow = 0;
                    foreach (bool b in a.Slow) if (b) slow++;
                    if (slow >= SlowFrames) throw new InvalidOperationException($"showing it takes over {SlowMsPerSecond:0} ms of every second");
                }
                catch (Exception e)
                {
                    Plugin.Log.LogWarning($"stored animation on {SignFinder.Id(a.Sign)} stopped: {e.Message}");
                    ZDO zdo = SignFinder.Zdo(a.Sign);
                    if (zdo != null) rejected[zdo.m_uid] = a.Hash;            // not again until the blob changes
                    playing.Remove(a.Sign);
                    SignText.Restore(a.Sign);
                }
            }
        }

        /// <summary>Once a second: which loaded signs carry an animation, did any change, and which new one goes to a
        /// worker to be judged (at most JudgesPerSecond: an unpack is megabytes). Only signs within PlayDistance and
        /// JudgeMargin are judged (farther ones would not play; 0.20.132, review), and one left out for room (EnforceCap)
        /// only once it is Nearer than the farthest one playing, or there is room for it.</summary>
        void Scan(float now)
        {
            nextScan = now + 1f;
            if (now - judgeSecond >= 1f)
            {
                judgeSecond = now;
                judgesThisSecond = 0;
            }
            Player p = Player.m_localPlayer;
            Vector3 me = p ? p.transform.position : Vector3.zero;
            float reach = PluginConfig.PlayDistance.Value + JudgeMargin, farthest = -1f;
            long chars = 0;
            foreach (Playing a in playing.Values)
            {
                chars += a.Chars;
                if (a.Sign) farthest = Mathf.Max(farthest, Vector3.Distance(me, a.Sign.transform.position));
            }
            var seen = new HashSet<Sign>();
            foreach (Sign s in SignFinder.Loaded())
            {
                ZDO zdo = SignFinder.Zdo(s);
                if (zdo == null || Still(zdo)) continue;                    // Moving off (Playing): not seen, so it stops below
                playing.TryGetValue(s, out Playing a);
                if (a != null && a.Revision == zdo.DataRevision)
                {
                    seen.Add(s);
                    continue;
                }
                byte[] blob = zdo.GetByteArray(Key);
                if (blob == null || blob.Length == 0) continue;
                float d = p ? Vector3.Distance(me, s.transform.position) : 0f;
                if (d > reach)                                              // too far to play: judged once nearer, and one
                {                                                           // playing stays as it is (it shows nothing there)
                    if (a != null) seen.Add(s);
                    continue;
                }
                int hash = HashOf(zdo, blob);
                string text = zdo.GetString(ZDOVars.s_text, "");
                if (a != null && a.Hash == hash && text == a.Frames[0])
                {
                    a.Revision = zdo.DataRevision;                          // another field changed: the same animation
                    seen.Add(s);
                    continue;
                }
                if (rejected.TryGetValue(zdo.m_uid, out int bad) && bad == hash) continue;
                if (evicted.TryGetValue(zdo.m_uid, out KeyValuePair<int, long> left) && left.Key == hash &&
                    chars + left.Value > MaxChars && !(d < farthest - Nearer))
                {
                    if (a != null) seen.Add(s);
                    continue;                                               // left out for room, and still no nearer
                }
                if (judging.Contains(zdo.m_uid) || judgesThisSecond >= JudgesPerSecond)
                {
                    if (a != null) seen.Add(s);                             // the old one plays on until judged
                    continue;
                }
                judgesThisSecond++;
                judging.Add(zdo.m_uid);
                Judge(s, zdo.m_uid, zdo.DataRevision, blob, hash, text);
                if (a != null) seen.Add(s);
            }
            foreach (Sign s in new List<Sign>(playing.Keys))
                if (!seen.Contains(s)) Forget(s);
            if (rejected.Count > 2000) rejected.Clear();                    // signs long gone: judged again if met
            if (hashes.Count > 2000) hashes.Clear();
            if (evicted.Count > 2000) evicted.Clear();
            EnforceCap();
        }

        /// <summary>A blob's identity, cheap: its length and a hash of its bytes, counted once per data revision.</summary>
        int HashOf(ZDO zdo, byte[] blob)
        {
            if (hashes.TryGetValue(zdo.m_uid, out KeyValuePair<uint, int> h) && h.Key == zdo.DataRevision) return h.Value;
            unchecked
            {
                int x = (int)2166136261 ^ blob.Length;
                for (int i = 0; i < blob.Length; i++) x = (x ^ blob[i]) * 16777619;
                hashes[zdo.m_uid] = new KeyValuePair<uint, int>(zdo.DataRevision, x);
                return x;
            }
        }

        /// <summary>Unpacks and weighs a blob on the pool; the main thread then plays it or remembers it as refused.</summary>
        void Judge(Sign s, ZDOID uid, uint revision, byte[] blob, int hash, string text)
        {
            ThreadPool.QueueUserWorkItem(_ =>
            {
                var j = new Judged();
                try
                {
                    if (!AnimBlob.TryUnpack(blob, out string[] frames, out float fps)) j.Why = "unreadable";
                    else if (frames[0] != text) j.Why = "its first frame is not the sign's text (edited by hand since)";
                    else
                    {
                        long most = 0, total = 0;
                        foreach (string f in frames)
                        {
                            long g = SaveCost.Glyphs(new[] { f });
                            most = Math.Max(most, g);
                            total += g;
                            j.Chars += f.Length;
                        }
                        double frameMs = most * GlyphMs, perSecond = total * GlyphMs * fps / frames.Length;
                        if (frameMs > MaxFrameMs) j.Why = $"too heavy to play: a frame takes about {frameMs:0} ms to show";
                        else if (perSecond > MaxMsPerSecond) j.Why = $"too heavy to play: about {perSecond:0} ms of every second";
                        else
                        {
                            j.Frames = frames;
                            j.Fps = fps;
                        }
                    }
                }
                catch (Exception e) { j.Why = e.Message; }
                MainThread.Post(() => Judged_(s, uid, revision, hash, j));
            });
        }

        void Judged_(Sign s, ZDOID uid, uint revision, int hash, Judged j)
        {
            judging.Remove(uid);
            ZDO zdo = s ? SignFinder.Zdo(s) : null;
            if (zdo == null || zdo.m_uid != uid) return;                    // gone meanwhile
            if (j.Why != null)
            {
                rejected[uid] = hash;
                Plugin.Log.LogInfo($"a saved animation on {SignFinder.Id(s)} is not played ({j.Why}): its first frame shows");
                return;
            }
            if (HashOf(zdo, zdo.GetByteArray(Key) ?? new byte[0]) != hash) return;   // changed again: judged on the next scan
            playing[s] = new Playing { Sign = s, Frames = j.Frames, Fps = j.Fps, Revision = zdo.DataRevision, Hash = hash, Chars = j.Chars };
            rejected.Remove(uid);
            evicted.Remove(uid);
            EnforceCap();
        }

        /// <summary>All playing frames together stay under MaxChars: the farthest animations are forgotten first, and
        /// remembered (evicted), so the next scan does not unpack them again only to forget them (Scan judges them again
        /// once nearer, or with room).</summary>
        void EnforceCap()
        {
            long chars = 0;
            foreach (Playing a in playing.Values) chars += a.Chars;
            if (chars <= MaxChars) return;
            Player p = Player.m_localPlayer;
            Vector3 me = p ? p.transform.position : Vector3.zero;
            var far = new List<Playing>(playing.Values);
            far.Sort((a, b) => (b.Sign ? Vector3.Distance(b.Sign.transform.position, me) : 0f)
                              .CompareTo(a.Sign ? Vector3.Distance(a.Sign.transform.position, me) : 0f));
            foreach (Playing a in far)
            {
                if (chars <= MaxChars) break;
                chars -= a.Chars;
                ZDO zdo = a.Sign ? SignFinder.Zdo(a.Sign) : null;
                if (zdo != null) evicted[zdo.m_uid] = new KeyValuePair<int, long>(a.Hash, a.Chars);
                Forget(a.Sign);
            }
        }

        void Forget(Sign s)
        {
            if (playing.Remove(s) && s && SignFinder.Zdo(s) != null) SignText.Restore(s);
        }

        /// <summary>Every animation playing here stops, its sign showing its saved text again (the plugin unloads).</summary>
        public void StopAll() => ForgetAll();

        void ForgetAll()
        {
            foreach (Sign s in new List<Sign>(playing.Keys)) Forget(s);
        }

        /// <summary>Drops a stored animation without touching the text: the mod writes a new text over it (a picture, a
        /// cleared sign). Otherwise the blob would stay and could push the sign past the network limit.</summary>
        public static void ClearBlob(Sign s)
        {
            ZDO zdo = SignFinder.Zdo(s);
            byte[] blob = zdo?.GetByteArray(Key);
            if (blob != null && blob.Length > 0) zdo.Set(Key, new byte[0]);
            if (zdo != null && zdo.GetInt(StillKey) != 0) zdo.Set(StillKey, 0);
            Plugin.Stored?.playing.Remove(s);                 // its old frames must not paint over the new text
        }

        /// <summary>Is the animation on this sign paused here?</summary>
        public bool Paused(Sign s)
        {
            ZDO zdo = s ? SignFinder.Zdo(s) : null;
            return zdo != null && paused.Contains(zdo.m_uid);
        }

        /// <summary>Pause shows the first frame (the saved text, as vanilla players see it) on this computer only; Play
        /// moves it again from where the shared clock is.</summary>
        public void Pause(Sign s, bool pause)
        {
            ZDO zdo = s ? SignFinder.Zdo(s) : null;
            if (zdo == null) return;
            if (pause)
            {
                paused.Add(zdo.m_uid);
                SignText.Restore(s);
            }
            else paused.Remove(zdo.m_uid);
            if (playing.TryGetValue(s, out Playing a)) a.Index = -1;       // the next step shows the right frame
        }

        /// <summary>One line per stored animation within reach, for the menu.</summary>
        public List<KeyValuePair<string, Sign>> Describe()
        {
            var list = new List<KeyValuePair<string, Sign>>();
            foreach (Playing a in playing.Values)
                if (a.Sign)
                    list.Add(new KeyValuePair<string, Sign>(Lang.F("{0} frames at {1:0.#} fps", a.Frames.Length, a.Fps), a.Sign));
            return list;
        }

        /// <summary>The frames and rate stored on a sign, if any (the window shows them).</summary>
        public bool Info(Sign s, out int frames, out float fps)
        {
            frames = 0;
            fps = 0;
            if (!s || !playing.TryGetValue(s, out Playing a)) return false;
            frames = a.Frames.Length;
            fps = a.Fps;
            return true;
        }
    }
}
