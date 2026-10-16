using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using SignLink.Api;
using SignLink.Pictures;
using SignLink.Signs;
using UnityEngine;

namespace SignLink.Animation
{
    /// <summary>Screens come back: a GIF placed as moving (the recipe on its first sign says so, SignRecipe.Animate)
    /// plays over its still again whenever its stack is loaded near you and the GIF file is in your images folder: on
    /// this computer, and on any other that has the file (the frames are never in the world: a GIF's frames are
    /// megabytes, far over what a sign can carry, docs/limits.md). Every two seconds the nearest such stack without a
    /// screen is decoded on a worker thread (one at a time) and its screen started exactly as placing did
    /// (PictureImporter.PlayOver: the plan from the recipe and the GIF's size, the place from the fit on the sign). A
    /// stack passed over (its file missing, a decode error, or stopped by the player on the Playing tab) is left alone
    /// until its recipe changes or the game restarts; one whose frames find no room in the screens' budget is looked at
    /// again after RetrySeconds (0.20.152, review: room comes back when the other screens are left behind). Frames the screens parked (the stack
    /// was rewritten, made again or unloaded, ScreenPlayer), or that another screen plays, play again at once, without
    /// the file; a file is decoded at most once a minute (0.20.132).</summary>
    internal sealed class ScreenRestore
    {
        const float ScanEvery = 2f;
        float nextScan;
        bool busy;

        /// <summary>A file is decoded for a screen at most once in this long (0.20.132, review: another player's recipe
        /// naming a GIF of your folder, its text changed each time, made your client decode it every scan). A stack whose
        /// file waits is looked at again when it may be decoded.</summary>
        const float RetrySeconds = 60f;
        /// <summary>When each file (PictureImporter.Stamp) was last decoded for a screen.</summary>
        readonly Dictionary<string, float> decoded = new Dictionary<string, float>();
        /// <summary>Stacks whose file waits for RetrySeconds, by their first sign's ZDO: until when.</summary>
        readonly Dictionary<ZDOID, float> waiting = new Dictionary<ZDOID, float>();

        /// <summary>The next scan now (the window changed a stack: its screen comes back without the two-second wait).</summary>
        public void Soon() => nextScan = 0f;
        /// <summary>Stacks passed over, by their first sign's ZDO and the recipe they had then.</summary>
        readonly Dictionary<ZDOID, string> skipped = new Dictionary<ZDOID, string>();
        /// <summary>Stacks the player paused (the Playing tab, 1 Sign): a screen restored on them starts paused, so the
        /// pause holds while the stack unloads and comes back.</summary>
        readonly Dictionary<ZDOID, string> paused = new Dictionary<ZDOID, string>();

        /// <summary>A tool stops this screen, or every screen (null): not again this session unless the sign's content
        /// changes. Called before the screen stops.</summary>
        public void Stopped(Sign only)
        {
            if (only)
            {
                Skip(only, "stopped by a tool");
                return;
            }
            foreach (ScreenPlayer.Row sc in Plugin.Screens.Describe(Time.unscaledTime)) Skip(sc.Sign, "stopped by a tool");
        }

        /// <summary>The player pauses this stack's screen or plays it on: remembered for a screen restored on it later.</summary>
        public void Pause(Sign s, bool on)
        {
            ZDO zdo = s ? SignFinder.Zdo(s) : null;
            if (zdo == null) return;
            if (on) paused[zdo.m_uid] = SignMarks.Source(s);
            else paused.Remove(zdo.m_uid);
        }

        /// <summary>No room in the screens' budget now: looked at again after RetrySeconds, not passed over for the session.</summary>
        void Later(Sign s, string why, float now)
        {
            ZDO zdo = s ? SignFinder.Zdo(s) : null;
            if (zdo == null) return;
            waiting[zdo.m_uid] = now + RetrySeconds;
            Plugin.Log.LogInfo($"screen on {SignFinder.Id(s)} waits: {why}");
        }

        /// <summary>The world was left: no file counts as decoded just now (its parked frames went with the world).</summary>
        public void Clear()
        {
            decoded.Clear();
            waiting.Clear();
        }

        void Skip(Sign s, string why)
        {
            ZDO zdo = s ? SignFinder.Zdo(s) : null;
            if (zdo == null) return;
            skipped[zdo.m_uid] = SignMarks.Source(s);
            Plugin.Log.LogInfo($"screen on {SignFinder.Id(s)} not restored: {why}");
        }

        /// <summary>Every frame (Plugin.Update): every ScanEvery seconds, the nearest moving GIF without a screen.</summary>
        public void Step(float now)
        {
            if (busy || now < nextScan) return;
            nextScan = now + ScanEvery;
            if (!ZNet.instance)
            {
                if (decoded.Count > 0 || waiting.Count > 0) Clear();
                return;
            }
            if (!PluginConfig.PlayGifs.Value) return;
            Player p = Player.m_localPlayer;
            if (!p) return;
            Vector3 me = p.transform.position;
            float best = PluginConfig.PlayDistance.Value;                  // farther ones would pause anyway
            Sign pick = null;
            string pickSrc = null;
            SignRecipe pickRecipe = null;
            foreach (Sign s in SignFinder.Loaded())
            {
                ZDO zdo = SignFinder.Zdo(s);
                if (zdo == null || zdo.GetInt(SignBuilder.CopyKey, 0) != 0) continue;   // the first sign carries the recipe
                string src = zdo.GetString(SignMarks.SourceKey, "");
                if (!src.StartsWith("pic|", StringComparison.Ordinal)) continue;         // cheap: most signs hold no picture
                src = SignMarks.Source(s);                                               // "" once its text was rewritten
                if (src.Length == 0 || (skipped.TryGetValue(zdo.m_uid, out string was) && was == src)) continue;
                if (waiting.TryGetValue(zdo.m_uid, out float until) && now < until) continue;   // its file was decoded just now
                if (Plugin.Screens.Plays(s)) continue;
                float d = Vector3.Distance(me, s.transform.position);
                if (d > best) continue;
                SignRecipe r = SignRecipe.Parse(src);
                if (r.Kind != SignRecipe.Kinds.Picture || !r.Animate || !r.File.EndsWith(".gif", StringComparison.OrdinalIgnoreCase)) continue;
                if (!SignFinder.IsLoaded(s)) continue;                       // copies still on their way: the screen covers the whole stack
                pick = s;
                best = d;
                pickSrc = src;
                pickRecipe = r;
            }
            if (pick) Start(pick, pickSrc, pickRecipe, now);
            if (skipped.Count > 2000) skipped.Clear();                      // stacks long gone: looked at again if met
            if (paused.Count > 2000) paused.Clear();
            if (waiting.Count > 2000) waiting.Clear();
            if (decoded.Count > 2000) decoded.Clear();
        }

        /// <summary>CON, PRN, AUX, NUL, COM1-9 and LPT1-9, with or without an extension: Windows opens a device for such
        /// a name in any folder (reading CON waits on the console), so another player's recipe must not name one
        /// (0.20.132, review).</summary>
        static bool Reserved(string name)
        {
            int dot = name.IndexOf('.');
            string stem = (dot < 0 ? name : name.Substring(0, dot)).TrimEnd(' ').ToUpperInvariant();
            if (stem == "CON" || stem == "PRN" || stem == "AUX" || stem == "NUL") return true;
            return stem.Length == 4 && (stem.StartsWith("COM", StringComparison.Ordinal) || stem.StartsWith("LPT", StringComparison.Ordinal)) &&
                   "123456789¹²³".IndexOf(stem[3]) >= 0;
        }

        /// <summary>Frames under the file's key (playing on another screen, or parked) play again now; else the file is
        /// read and decoded on a worker (GifDecoder, as the import does), at most once in RetrySeconds, and the screen
        /// starts on the main thread.</summary>
        void Start(Sign s, string src, SignRecipe r, float now)
        {
            string name, file;
            try
            {
                name = Path.GetFileName(r.File);                            // a name from another player's sign: no folders
                file = name.Length > 0 && !Reserved(name) ? Path.Combine(PictureImporter.Folder, name) : "";
            }
            catch (ArgumentException)                                       // characters Windows does not take in a name
            {
                Skip(s, $"{SignRecipe.Clean(r.File)} is not a file name here, so its still stays");
                return;
            }
            if (file.Length == 0 || !File.Exists(file))
            {
                Skip(s, $"{SignRecipe.Clean(r.File)} is not in the images folder, so its still stays");
                return;
            }
            var o = new PictureOptions();
            r.Apply(o);                                                     // the picture's own settings (Place = Custom)
            o.Detail = 0;                                                   // the grid on the sign from its width and pixel, not the player's
                                                                            // Resolution: Fine Width makes a few pixels more or less (0.20.152, review)
            string key = PictureImporter.Stamp(file);
            ScreenFrames kept = Plugin.Screens.FramesFor(key);
            if (kept != null)
            {
                Finish(s, src, name, o, o.PlanFor(kept.SourceW, kept.SourceH), kept, how: "from memory");
                return;
            }
            ZDO zdo = SignFinder.Zdo(s);
            if (key != null && decoded.TryGetValue(key, out float at) && now - at < RetrySeconds)
            {
                if (zdo != null) waiting[zdo.m_uid] = at + RetrySeconds;    // not skipped: looked at again then
                return;
            }
            if (key != null) decoded[key] = now;
            if (zdo != null) waiting.Remove(zdo.m_uid);
            long room = Plugin.Screens.Room;                                // what the playing screens leave (parked ones go)
            busy = true;
            bool noRoom = false;
            ThreadPool.QueueUserWorkItem(_ =>
            {
                try
                {
                    var sw = Stopwatch.StartNew();
                    byte[] bytes = File.ReadAllBytes(file);                 // on the worker (0.20.132, review)
                    // the budget from the blocks, before any frame is decoded: frames that cannot play are not made
                    if (GifDecoder.Measure(bytes, out int gw, out int gh, out int count))
                    {
                        if (count < 2) throw new InvalidOperationException("the file has one frame");
                        long need = PictureImporter.ScreenBytes(gw, gh, count);
                        noRoom = need > room;
                        if (noRoom)
                            throw new InvalidOperationException($"its frames would be {need >> 20} MB, the screens have room for {Math.Max(0, room) >> 20} MB");
                    }
                    ScreenFrames frames = PictureImporter.GifScreen(bytes) ?? throw new InvalidOperationException("the file has one frame");
                    PicturePlan plan = o.PlanFor(frames.SourceW, frames.SourceH);
                    frames.Key = key;
                    string how = $"decoded in {sw.Elapsed.TotalSeconds:0.0} s";
                    MainThread.Post(() => Finish(s, src, name, o, plan, frames, how: how));
                }
                catch (Exception e)
                {
                    MainThread.Post(() =>
                    {
                        busy = false;
                        if (noRoom) Later(s, $"{name}: {e.Message}", Time.unscaledTime);
                        else Skip(s, $"{name}: {e.Message}");
                    });
                }
            });
        }

        /// <summary>The screen over the still, where the still sits now: its fit is read here, on the main thread, so a
        /// refit during the decode (the same recipe since 0.20.40) still gets the screen over it.</summary>
        void Finish(Sign s, string src, string name, PictureOptions o, PicturePlan plan, ScreenFrames frames, string how)
        {
            busy = false;
            if (!s || SignFinder.Zdo(s) == null || SignMarks.Source(s) != src || Plugin.Screens.Plays(s))
            {                                                               // gone, changed or placed again meanwhile: looked at
                Plugin.Screens.Park(frames, Time.unscaledTime);             // again, its frames kept for it (0.20.132)
                return;
            }
            Fit fit = SignFit.Read(s, out _, out _, out _);                  // where the still sits: the screen goes over it
            o.RightM = (float)(fit.Right / 40);
            o.UpM = (float)(fit.Up / 40);
            o.OutM = (float)(fit.Out / 40);
            o.TiltDeg = (float)fit.Tilt;
            o.TurnDeg = (float)fit.Turn;
            o.EdgePivot = fit.Edge;
            o.Origin(plan, out double cx, out double bottom);
            if (!PictureImporter.PlayOver(s, frames, plan, cx, bottom, !o.Glow, out string why))
            {
                if (why != null && why.StartsWith("too big to play", StringComparison.Ordinal))
                {                                                           // the budget: kept parked, tried again later
                    Plugin.Screens.Park(frames, Time.unscaledTime);
                    Later(s, $"{name}: {why}", Time.unscaledTime);
                }
                else Skip(s, $"{name}: {why}");
                return;
            }
            ZDO zdo = SignFinder.Zdo(s);
            bool hold = zdo != null && paused.TryGetValue(zdo.m_uid, out string was) && was == src;
            if (hold) Plugin.Screens.Pause(s, true);
            Plugin.Log.LogInfo($"screen restored on {SignFinder.Id(s)}: {name} {(hold ? "is back, paused as the player left it" : "plays over its still again")} ({how})");
        }
    }
}
