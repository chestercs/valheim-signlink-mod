using System;
using System.Collections.Generic;
using System.Text;
using SignLink.Pictures;
using SignLink.Signs;

namespace SignLink.Animation
{
    /// <summary>Sign-text animations: groups of signs flipped on one clock. The frame shown is the one due from the
    /// elapsed time, so a slow game drops frames but keeps the real speed; a sign whose text did not change since
    /// its last write is not written again (no TMP rebuild, no ZDO revision, no network).</summary>
    internal sealed class TextAnimator
    {
        sealed class Anim
        {
            public string Key;
            public Sign[] Signs;
            public string[][] Frames;            // [frame][sign]
            public string[] Last;
            public float Fps, Started;
            public bool Local, Loop;
            public int Index, Shown, Skipped, Writes;
            public long Chars;
            public double SumMs, MaxMs;
        }

        readonly List<Anim> anims = new List<Anim>();
        readonly HashSet<Sign> driven = new HashSet<Sign>();

        /// <summary>Something of SignLink's shows its own text on this sign now (a tool's animation): the game's
        /// refresh and the saved animations leave it alone.</summary>
        public bool Drives(Sign s) => driven.Contains(s);

        /// <summary>Every frame (Plugin.Update): animations whose signs are all gone go, playback on or off, and all of
        /// them when the world is left (0.20.132, review: up to 16 MB of an API animation's frames each stayed after
        /// leaving the world, and with playback off nothing was swept); then the frames due are shown.</summary>
        public void Step(float now)
        {
            if (!ZNet.instance)
            {
                if (anims.Count > 0 || driven.Count > 0)
                {
                    anims.Clear();
                    driven.Clear();
                }
                return;
            }
            for (int g = anims.Count - 1; g >= 0; g--)
                if (AllGone(anims[g])) Remove(anims[g], restore: false);    // unloaded with their zone, or removed
            if (!PluginConfig.PlayAnimations.Value) return;
            for (int g = anims.Count - 1; g >= 0; g--)
            {
                Anim a = anims[g];
                try { Show(a, now); }
                catch (Exception e)
                {
                    Plugin.Log.LogWarning($"animation on {a.Key} stopped: {e.Message}");
                    Remove(a, restore: true);
                }
            }
        }

        static bool AllGone(Anim a)
        {
            foreach (Sign s in a.Signs)
                if (s && SignFinder.Zdo(s) != null) return false;
            return true;
        }

        /// <summary>Shows the frame due now; signs whose text did not change are not written again.</summary>
        void Show(Anim a, float now)
        {
            long due = (long)((now - a.Started) * a.Fps);
            if (!a.Loop && due >= a.Frames.Length) { Remove(a, restore: false); return; }
            int index = (int)(due % a.Frames.Length);
            if (a.Shown > 0 && index == a.Index) return;
            if (a.Shown > 0) a.Skipped += ((index - a.Index - 1) % a.Frames.Length + a.Frames.Length) % a.Frames.Length;
            a.Index = index;
            string[] frame = a.Frames[index];
            double ms = 0;
            for (int i = 0; i < a.Signs.Length; i++)
            {
                Sign s = a.Signs[i];
                if (!s || SignFinder.Zdo(s) == null || frame[i] == a.Last[i]) continue;
                ms += SignText.Apply(s, frame[i], a.Local);
                a.Last[i] = frame[i];
                a.Writes++;
                a.Chars += frame[i].Length;
            }
            a.SumMs += ms;
            a.MaxMs = Math.Max(a.MaxMs, ms);
            a.Shown++;
        }

        /// <summary>The most bytes a second an animation written to the world (mode=zdo) may send in a shared world:
        /// about a tenth of what a server sends a player (SaveCost.DownloadSeconds: 150 KB/s at best).</summary>
        public const long SharedBytesPerSecond = 16 * 1024;

        public string Start(string[] ids, string[][] frames, float fps, bool local, bool loop, float now)
        {
            if (frames.Length == 0) throw new ArgumentException("no frames");
            if (fps <= 0 || fps > 60) throw new ArgumentException("fps must be in (0, 60]");
            var all = SignFinder.ById();
            var signs = new Sign[ids.Length];
            for (int i = 0; i < ids.Length; i++)
                if (!all.TryGetValue(ids[i], out signs[i]))
                    throw new ArgumentException($"no loaded sign with id {ids[i]} (ids change every session: list /signs again)");
            if (!local)
            {
                foreach (Sign s in signs)
                    if (!SignText.CanEdit(s)) throw new InvalidOperationException($"a ward you have no access to protects sign {SignFinder.Id(s)}");
                // every frame is stored on the signs in turn: the multiplayer limits once per stack, with its animated signs'
                // texts swapped for the largest frame's (the limits only grow with the bytes, so that one decides)
                var byRoot = new Dictionary<Sign, List<int>>();
                for (int i = 0; i < signs.Length; i++)
                {
                    Sign root = SignFinder.Root(signs[i]) ?? signs[i];
                    if (!byRoot.TryGetValue(root, out List<int> at)) byRoot[root] = at = new List<int>();
                    at.Add(i);
                }
                foreach (KeyValuePair<Sign, List<int>> e in byRoot)
                {
                    long current = 0, most = 0;
                    foreach (int i in e.Value) current += Encoding.UTF8.GetByteCount(SignText.SavedText(signs[i]));
                    foreach (string[] f in frames)
                    {
                        long b = 0;
                        foreach (int i in e.Value) b += Encoding.UTF8.GetByteCount(f[i] ?? "");
                        most = Math.Max(most, b);
                    }
                    SignText.RequireRoom(e.Key, SignPose.Stored(e.Key) - current + most, "this animation");
                }
                // every changed frame is sent to every player near it: in a shared world at most SharedBytesPerSecond
                // (0.20.130, review: a 100-300 KB sign at 4 fps kept every nearby peer's sync queue full)
                if (SignText.Shared())
                {
                    long frameMost = 0;
                    foreach (string[] f in frames)
                    {
                        long b = 0;
                        foreach (string t in f) b += Encoding.UTF8.GetByteCount(t ?? "");
                        frameMost = Math.Max(frameMost, b);
                    }
                    if (frameMost * fps > SharedBytesPerSecond)
                        throw new ArgumentException($"a {fps:0.#} fps animation of up to {SaveCost.Label(frameMost)} a frame sends about " +
                                                    $"{SaveCost.Label((long)(frameMost * fps))} a second to every player near it, over the " +
                                                    $"{SaveCost.Label(SharedBytesPerSecond)} an animation may send in a multiplayer world " +
                                                    "(mode=local plays it for you only; store=1 saves it on the sign once)");
                }
            }
            foreach (Sign s in signs) StopFor(s);
            anims.Add(new Anim { Key = string.Join(",", ids), Signs = signs, Frames = frames, Last = new string[ids.Length],
                                 Fps = fps, Local = local, Loop = loop, Started = now });
            foreach (Sign s in signs) driven.Add(s);
            long chars = 0;
            foreach (string[] f in frames) foreach (string t in f) chars += t.Length;
            return "{\"ok\":true,\"signs\":" + ids.Length + ",\"frames\":" + frames.Length + ",\"chars\":" + chars + "}";
        }

        public void StopFor(Sign s)
        {
            foreach (Anim a in anims.ToArray())
                if (IndexOf(a.Signs, s) >= 0) Remove(a, restore: false);
        }

        /// <summary>Stops the animation driving that sign, or all (null); a local one shows the saved text again. A sign
        /// that Unity already destroyed still means that one sign, not all (reference comparison).</summary>
        public int Stop(Sign only)
        {
            int n = 0;
            foreach (Anim a in anims.ToArray())
                if (ReferenceEquals(only, null) || IndexOf(a.Signs, only) >= 0) { Remove(a, restore: true); n++; }
            return n;
        }

        static int IndexOf(Sign[] signs, Sign s)
        {
            for (int i = 0; i < signs.Length; i++)
                if (ReferenceEquals(signs[i], s)) return i;
            return -1;
        }

        void Remove(Anim a, bool restore)
        {
            anims.Remove(a);
            foreach (Sign s in a.Signs)
            {
                driven.Remove(s);
                if (restore && a.Local) SignText.Restore(s);
            }
        }

        /// <summary>One line per animation for the menu, with its first sign (to stop it).</summary>
        public List<KeyValuePair<string, Sign>> Describe(float now)
        {
            var list = new List<KeyValuePair<string, Sign>>();
            foreach (Anim a in anims)
                list.Add(new KeyValuePair<string, Sign>(
                    $"{a.Signs.Length} sign(s), {a.Frames.Length} frames at {a.Fps:0.#} fps, {(a.Local ? "local" : "synced")} " +
                    $"({a.Shown / Math.Max(0.001f, now - a.Started):0.#} shown/s)", a.Signs.Length > 0 ? a.Signs[0] : null));
            return list;
        }

        public string Stats(float now)
        {
            var sb = new StringBuilder("[");
            bool first = true;
            for (int g = 0; g < anims.Count; g++)
            {
                Anim a = anims[g];
                float secs = Math.Max(0.001f, now - a.Started);
                if (!first) sb.Append(',');
                first = false;
                sb.Append("{\"ids\":").Append(Json.Str(a.Key))
                  .Append(",\"mode\":").Append(Json.Str(a.Local ? "local" : "zdo"))
                  .Append(",\"frames\":").Append(a.Frames.Length)
                  .Append(",\"fps\":").Append(Json.F(a.Fps))
                  .Append(",\"shown\":").Append(a.Shown)
                  .Append(",\"realFps\":").Append(Json.F(a.Shown / secs))
                  .Append(",\"skipped\":").Append(a.Skipped)
                  .Append(",\"writes\":").Append(a.Writes)
                  .Append(",\"kbPerSec\":").Append(Json.F(a.Chars / 1024.0 / secs))
                  .Append(",\"avgMs\":").Append(Json.F(a.Shown > 0 ? a.SumMs / a.Shown : 0))
                  .Append(",\"maxMs\":").Append(Json.F(a.MaxMs))
                  .Append('}');
            }
            return sb.Append(']').ToString();
        }
    }
}
