using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using SignLink.Pictures;
using SignLink.Signs;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace SignLink.Animation
{
    /// <summary>Texture screens for SignLink viewers: the frames are pixel images shown by a RawImage on the sign's own
    /// canvas, where the same picture in sign text would be. A new frame is one texture upload (well under a
    /// millisecond for video sizes) instead of a TMP rebuild (13.5 us per glyph, docs/limits.md). Clients without
    /// the mod keep seeing the sign's text. hideText hides the whole stack (SignFinder.Stack): a picture placed over several
    /// stacked signs is covered at once. lit shades the screen like the sign's text (the game's Lambert shader; the
    /// default UI material is unlit, so a screen without it glows day and night). Every screen plays at its frames' own
    /// times (FrameTimes): a GIF with 40 and 60 ms frames does not drift, and a long hold does not slow the rest. All
    /// screens together hold at most Budget bytes of frames, the parked ones included: a screen whose sign went (unloaded,
    /// made again, its text rewritten) parks its frames under their key (the file they came from), and ScreenRestore
    /// plays them again from there without decoding the file; the oldest parked frames go when a new screen needs the
    /// room, and any after ParkSeconds unused. Frames another screen plays are found there (FramesFor) and not parked
    /// twice (0.20.132). A screen the player stops drops its frames.</summary>
    internal sealed class ScreenPlayer
    {
        sealed class Screen
        {
            public Sign Sign;
            public string Id, Key;
            public GameObject Go;
            public Texture2D Tex;
            public byte[][] Frames;                  // RGB24, bottom-up rows (Unity's order)
            public int[] Delays;                     // each frame's ms (kept for parking)
            public int SourceW, SourceH;             // the GIF's own size (kept for parking)
            public long[] Ends;                      // when each frame ends (ms from the start)
            public long Total, Bytes;
            public int W, H, Index = -1, Shown, Skipped;
            public float Fps, Started, LastUpload;   // Fps: the average, for the stats and the skip rule
            public bool Loop, Lit, Idle, Paused;     // Idle: logged once while no new frame shows; Paused: the player's
            public List<Sign> Hidden;                // the stack whose text the screen covers
            public double SumMs, MaxMs;
            public float WinAt, Rate;                // frames shown per second over the last RateWindow seconds
            public int WinShown;
            public uint Revision;                    // its sign's data as the screen started over it (Step: written since?)
            public string Src;
            public int TextHash;
            public float Dist;                       // from the player this step (the nearest uploads first)
        }

        /// <summary>The frames shown per second (the Playing tab, the stats) are counted over windows this long.</summary>
        const float RateWindow = 2f;

        /// <summary>One row of the Playing tab.</summary>
        public sealed class Row
        {
            public string Text;
            public Sign Sign;
            public bool Paused;
        }

        /// <summary>A screen that shows no new frame for this long says why in the log, once.</summary>
        const float IdleSeconds = 3f;

        /// <summary>What every moving GIF's frames may take together on this computer: Settings › Playback › GIF memory
        /// (256 MB until 1.0.2; the author: 512 and 1024 too). Lowered under what is held, Trim parks the farthest screens.</summary>
        public static long Budget => (long)(PluginConfig.GifMemory?.Value ?? 256) << 20;

        /// <summary>The most one GIF's kept frames may take: past it every k-th frame is kept (GifDecoder.Stride), counted at
        /// the screen's size. 200 MB of the 256 (1.0.1 counted the full-size frames: a GIF on a small sign lost every second
        /// frame for nothing, 1.0.2 review).</summary>
        public static long FrameCap => Budget * 25 / 32;

        /// <summary>The shader of a screen lit by the scene: Unity's Lambert surface shader, in the set every build
        /// includes (unity_builtin_extra; verified in Valheim 1.0.17). Lit like the sign's text material (sun, ambient,
        /// torches; dark at night): a canvas mesh's normals face the canvas's front, the same as the text's.</summary>
        public const string LitShader = "Legacy Shaders/Diffuse";

        sealed class Parked
        {
            public ScreenFrames Frames;
            public float At;
        }

        readonly List<Screen> screens = new List<Screen>();
        /// <summary>How many screens show each frame: the budget counts a frame once, however many signs show it (1.0.2,
        /// the author: a GIF copied onto a third sign stayed still, its frames counted three times though shared).</summary>
        readonly Dictionary<byte[], int> users = new Dictionary<byte[], int>();
        readonly Dictionary<string, Parked> parked = new Dictionary<string, Parked>();
        long held, parkedBytes;
        static Material litMaterial;
        static bool litLooked;

        /// <summary>The lit screens' one material; null when the build has no LitShader (logged once): they glow instead.</summary>
        internal static Material LitMaterial()
        {
            if (litLooked) return litMaterial;
            litLooked = true;
            Shader shader = Shader.Find(LitShader);
            if (shader == null)
            {
                Plugin.Log.LogWarning($"screens: the game has no '{LitShader}' shader, so a GIF lit by the scene glows instead");
                return null;
            }
            litMaterial = new Material(shader) { name = "SignLinkScreenLit" };
            return litMaterial;
        }

        /// <summary>The lit material goes with the plugin (0.20.152, review: one leaked per reload, against the 0.20.135
        /// rule); the next copy makes its own.</summary>
        internal static void DropLitMaterial()
        {
            if (litMaterial) UnityEngine.Object.Destroy(litMaterial);
            litMaterial = null;
            litLooked = false;
        }

        /// <summary>Bytes of frames held now: playing and parked.</summary>
        public long Held => held + parkedBytes;

        /// <summary>The most a new screen's frames may hold now: what the playing screens leave (parked frames make room).</summary>
        public long Room => Budget - held;

        /// <summary>Is there room for this many bytes of frames? Parked frames make room, oldest first.</summary>
        public bool CanHold(long bytes)
        {
            if (bytes == 0) return true;                                    // frames playing already (the same GIF on another
                                                                            // sign) take nothing, whatever the limit (1.0.2, review)
            if (held + bytes > Budget) return false;                        // never, whatever is dropped: keep the parked sets
            while (held + parkedBytes + bytes > Budget && parked.Count > 0) DropOldest("to make room");
            return held + parkedBytes + bytes <= Budget;
        }

        void DropOldest(string why)
        {
            string oldest = null;
            float at = float.MaxValue;
            foreach (KeyValuePair<string, Parked> p in parked)
                if (p.Value.At < at)
                {
                    at = p.Value.At;
                    oldest = p.Key;
                }
            if (oldest != null) Drop(oldest, why);
        }

        /// <summary>The frames parked under this key, out of the park (the screen started with them holds them from
        /// here); false when none are parked.</summary>
        public bool TakeParked(string key, out ScreenFrames frames)
        {
            frames = null;
            if (key == null || !parked.TryGetValue(key, out Parked p)) return false;
            parked.Remove(key);
            parkedBytes -= p.Frames.Bytes;
            frames = p.Frames;
            return true;
        }

        /// <summary>The frames under this key (the file they came from, PictureImporter.Stamp), playing on a screen or
        /// parked; null when there are none (0.20.132, review: an Update of a moving GIF, or its stack loaded again next
        /// to another that shows it, decoded the file again although the very frames were here). Frames are never
        /// written, so screens share them; parked ones stay parked until a screen starts with them (StartTimed takes
        /// them out of the park then).</summary>
        public ScreenFrames FramesFor(string key)
        {
            if (key == null) return null;
            foreach (Screen sc in screens)
                if (sc.Key == key)
                    return new ScreenFrames { W = sc.W, H = sc.H, Frames = sc.Frames, DelaysMs = sc.Delays, Bytes = sc.Bytes,
                                              SourceW = sc.SourceW, SourceH = sc.SourceH, Key = key };
            return parked.TryGetValue(key, out Parked p) ? p.Frames : null;
        }

        /// <summary>The frames of this file (its Stamp) at the size a sign shows it (1.0.2: a screen is sized to its sign,
        /// PictureImporter.ScreenKey): screenW gives the screen's width for the GIF's own size, which the frames carry.</summary>
        public ScreenFrames FramesFor(string stamp, Func<int, int, int> screenW)
        {
            if (stamp == null) return null;
            string prefix = stamp + "@";
            foreach (Screen sc in screens)
                if (sc.Key != null && sc.Key.StartsWith(prefix, StringComparison.Ordinal) && sc.W == screenW(sc.SourceW, sc.SourceH))
                    return FramesFor(sc.Key);
            foreach (KeyValuePair<string, Parked> p in parked)
                if (p.Key.StartsWith(prefix, StringComparison.Ordinal) && p.Value.Frames.W == screenW(p.Value.Frames.SourceW, p.Value.Frames.SourceH))
                    return p.Value.Frames;
            return null;
        }

        /// <summary>Is there room for this many bytes of frames on this sign, ever: what the other screens play (parked
        /// frames make room, this sign's own screen goes first), checked before a request's frames are copied.</summary>
        bool Fits(Sign s, long bytes)
        {
            long others = held;
            foreach (Screen sc in screens)
                if (ReferenceEquals(sc.Sign, s)) others -= OnlyHere(sc);
            return others + bytes <= Budget;
        }

        /// <summary>The room a new picture on this sign gets back from the screen over it now (its own frames go first).</summary>
        public long Frees(Sign s)
        {
            long n = 0;
            foreach (Screen sc in screens)
                if (ReferenceEquals(sc.Sign, s)) n += OnlyHere(sc);
            return n;
        }

        /// <summary>The bytes of this screen's frames no other screen shows (what stopping it frees).</summary>
        long OnlyHere(Screen sc)
        {
            long n = 0;
            var seen = new HashSet<byte[]>();
            foreach (byte[] f in sc.Frames)
                if (seen.Add(f) && users.TryGetValue(f, out int u) && u == 1) n += f.Length;
            return n;
        }

        /// <summary>Parked frames nobody took again in this long are dropped (0.20.132, review: a few GIFs left behind held
        /// up to 256 MB all session). Parking is for a stack made again or loaded again soon (0.20.14): ten minutes.</summary>
        public const float ParkSeconds = 600f;
        float nextExpiry;

        /// <summary>Drops the parked sets older than ParkSeconds, every few seconds at most (Step).</summary>
        void Expire(float now)
        {
            if (parked.Count == 0 || now < nextExpiry) return;
            nextExpiry = now + 5f;
            List<string> old = null;
            foreach (KeyValuePair<string, Parked> p in parked)
                if (now - p.Value.At > ParkSeconds) (old ?? (old = new List<string>())).Add(p.Key);
            if (old != null)
                foreach (string key in old) Drop(key, $"after {ParkSeconds / 60:0} minutes unused");
        }

        void Park(Screen sc, float now)
        {
            if (sc.Key == null) return;
            foreach (Screen other in screens)                               // another screen plays them: FramesFor finds them
                if (!ReferenceEquals(other, sc) && other.Key == sc.Key) return;   // there, no second copy in the park (0.20.132)
            if (parked.TryGetValue(sc.Key, out Parked old)) parkedBytes -= old.Frames.Bytes;
            parked[sc.Key] = new Parked
            {
                Frames = new ScreenFrames { W = sc.W, H = sc.H, Frames = sc.Frames, DelaysMs = sc.Delays, Bytes = sc.Bytes,
                                            SourceW = sc.SourceW, SourceH = sc.SourceH, Key = sc.Key },
                At = now,
            };
            parkedBytes += sc.Bytes;
        }

        /// <summary>Frames decoded for a screen that could not start (its stack went or changed meanwhile) are parked as a
        /// screen's would be, when there is room: the next look plays them without decoding the file again (ScreenRestore
        /// decodes a file at most once a minute; 0.20.132, final review). Frames another screen plays are not parked.</summary>
        public void Park(ScreenFrames f, float now)
        {
            if (f == null || f.Key == null || parked.ContainsKey(f.Key)) return;
            foreach (Screen other in screens)
                if (other.Key == f.Key) return;
            if (!CanHold(f.Bytes)) return;
            parked[f.Key] = new Parked { Frames = f, At = now };
            parkedBytes += f.Bytes;
        }

        void Drop(string key, string why)
        {
            if (!parked.TryGetValue(key, out Parked p)) return;
            parked.Remove(key);
            parkedBytes -= p.Frames.Bytes;
            Plugin.Log.LogInfo($"parked frames of {key.Split('|')[0]} dropped {why} ({p.Frames.Bytes >> 20} MB); {parked.Count} set(s) parked, {Held >> 20} MB held");
        }

        /// <summary>rgb: n frames of w x h x 3 bytes, rows top-down, at a fixed rate. (cx, cy): the picture's centre
        /// relative to the sign, in wu (2.5 cm); pitch: wu per pixel. hideText: hide the sign's own text while the
        /// screen shows. lit: shaded by the scene like the sign's text; otherwise it glows.</summary>
        public string Start(string id, int w, int h, float fps, float cx, float cy, float pitch, bool loop, bool hideText,
                            bool point, bool lit, byte[] rgb, float now)
        {
            if (w <= 0 || h <= 0 || w > 4096 || h > 4096) throw new ArgumentException("w and h must be 1..4096");
            if (fps <= 0 || fps > 60) throw new ArgumentException("fps must be in (0, 60]");
            CheckPlace(cx, cy, pitch, 0f);
            int frameBytes = w * h * 3;
            if (rgb.Length == 0 || rgb.Length % frameBytes != 0)
                throw new ArgumentException($"body is {rgb.Length} bytes, not a whole number of {w} x {h} RGB frames");
            Sign s = SignFinder.Find(id);
            if (!Fits(s, rgb.Length))                                        // before the body is copied (0.20.132, review)
                throw new ArgumentException($"too big to play: the screens hold {Held >> 20} MB of frames already (at most {Budget >> 20} MB)");
            int n = rgb.Length / frameBytes, row = w * 3;
            var frames = new byte[n][];
            for (int f = 0; f < n; f++)
            {
                var fr = new byte[frameBytes];
                for (int r = 0; r < h; r++)
                    Buffer.BlockCopy(rgb, f * frameBytes + r * row, fr, (h - 1 - r) * row, row);
                frames[f] = fr;
            }
            return StartFrames(s, w, h, fps, cx, cy, pitch, loop, hideText, point, lit, frames, now);
        }

        /// <summary>The same from frames already in Unity's row order (bottom-up), w x h x 3 bytes each, at a fixed
        /// rate: the uniform case of StartTimed.</summary>
        public string StartFrames(Sign s, int w, int h, float fps, float cx, float cy, float pitch, bool loop, bool hideText,
                                  bool point, bool lit, byte[][] frames, float now)
        {
            if (fps <= 0 || fps > 60) throw new ArgumentException("fps must be in (0, 60]");
            var delays = new int[frames.Length];
            for (int i = 0; i < delays.Length; i++) delays[i] = Math.Max(11, (int)Math.Round(1000f / fps));
            return StartTimed(s, w, h, delays, cx, cy, pitch, loop, hideText, point, lit, frames, now);
        }

        /// <summary>Frames in Unity's row order, each with its own delay (a GIF's; 10 ms or less counts as 100, as
        /// browsers time it); a frame may appear several times (the same array) to stay longer. Throws when the
        /// screens' budget has no room. key (and the GIF's own size): the frames are parked under it when the screen's
        /// sign goes, to play again without the file; null: dropped then.</summary>
        public string StartTimed(Sign s, int w, int h, int[] delaysMs, float cx, float cy, float pitch, bool loop, bool hideText,
                                 bool point, bool lit, byte[][] frames, float now, string key = null, int sourceW = 0, int sourceH = 0,
                                 float pitchY = 0f)
        {
            if (w <= 0 || h <= 0 || w > 4096 || h > 4096) throw new ArgumentException("w and h must be 1..4096");
            CheckPlace(cx, cy, pitch, pitchY);                               // before anything reaches the screen's rect
            if (frames.Length == 0) throw new ArgumentException("no frames");
            if (delaysMs.Length != frames.Length) throw new ArgumentException("one delay per frame");
            long bytes = 0;
            var distinct = new HashSet<byte[]>();
            foreach (byte[] f in frames)
            {
                if (f == null || f.Length != w * h * 3) throw new ArgumentException($"every frame must be {w} x {h} x 3 bytes");
                if (distinct.Add(f)) bytes += f.Length;
            }
            Stop(s, keep: false);                                            // the same sign again: the old frames go
            Plugin.Text.Stop(s);                                             // a tool's local frame goes: the saved text shows under the screen
            Parked wasParked = null;                                         // the same frames parked: they play now
            if (key != null && parked.TryGetValue(key, out wasParked)) TakeParked(key, out _);
            long added = 0;                                                  // only frames no screen shows yet take room
            foreach (byte[] f in distinct)
                if (!users.ContainsKey(f)) added += f.Length;
            if (!CanHold(added))
            {
                if (wasParked != null && !parked.ContainsKey(key))          // back in the park: a later try needs no decode
                {                                                           // (1.0.2, review: they were dropped)
                    parked[key] = wasParked;
                    parkedBytes += wasParked.Frames.Bytes;
                }
                throw new ArgumentException($"too big to play: the screens hold {Held >> 20} MB of frames already (at most {Budget >> 20} MB)");
            }
            long[] ends = FrameTimes.Ends(delaysMs);
            long total = ends[ends.Length - 1];
            float fps = frames.Length * 1000f / Math.Max(1, total);
            var tex = new Texture2D(w, h, TextureFormat.RGB24, false)
            {
                name = "SignLinkScreen",
                filterMode = point ? FilterMode.Point : FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
            };
            tex.LoadRawTextureData(frames[0]);
            tex.Apply(false);

            var go = new GameObject("SignLinkScreen", typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(s.m_textWidget.transform, false);
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            // sign-relative wu -> text-local units: 1 local unit = 5 cm = 2 wu (docs/sign-rendering.md section 2)
            rt.sizeDelta = new Vector2(w * pitch / 2f, h * (pitchY > 0f ? pitchY : pitch) / 2f);   // pitchY: its own overscan (PlayOver)
            rt.anchoredPosition = new Vector2(cx / 2f, cy / 2f);
            var img = go.AddComponent<RawImage>();
            img.texture = tex;
            img.raycastTarget = false;
            Material shaded = lit ? LitMaterial() : null;                    // null: the default UI material, unlit
            if (shaded != null) img.material = shaded;
            List<Sign> hidden = hideText ? SignFinder.Stack(s) : new List<Sign>();
            foreach (Sign o in hidden) o.m_textWidget.enabled = false;

            foreach (byte[] f in distinct)
                users[f] = users.TryGetValue(f, out int u) ? u + 1 : 1;
            held += added;
            screens.Add(new Screen { Sign = s, Id = SignFinder.Id(s), Key = key, Go = go, Tex = tex, Frames = frames, Delays = delaysMs,
                                     Revision = SignFinder.Zdo(s)?.DataRevision ?? 0, Src = SignMarks.Source(s), TextHash = SignText.SavedText(s).GetHashCode(),
                                     SourceW = sourceW, SourceH = sourceH, Ends = ends, Total = total, Bytes = bytes,
                                     W = w, H = h, Fps = fps, Started = now, LastUpload = now, WinAt = now, Loop = loop, Lit = shaded != null,
                                     Hidden = hidden, Index = 0, Shown = 1 });
            Plugin.Log.LogInfo($"screen on {SignFinder.Id(s)}: {w}x{h}, {frames.Length} frames at {fps:0.#} fps ({bytes >> 20} MB), " +
                               $"{(shaded != null ? "lit by the scene" : "glowing")}, hides the text of {hidden.Count} sign(s); " +
                               $"{screens.Count} screen(s) playing, {parked.Count} parked, {Held >> 20} MB held");
            return "{\"ok\":true,\"frames\":" + frames.Length + ",\"w\":" + w + ",\"h\":" + h + ",\"mb\":" + Json.F(bytes / 1048576.0) +
                   ",\"lit\":" + (shaded != null ? "true" : "false") + "}";
        }

        /// <summary>A screen's place and pixel size, in wu (from a sign's recipe and fit: another player's numbers, or the
        /// API's), must be finite and in range before they reach the RectTransform (0.20.132, review): 1 km from the sign,
        /// a pixel up to 4096 wu.</summary>
        static void CheckPlace(float cx, float cy, float pitch, float pitchY)
        {
            const float Far = 40000f, Pixel = 4096f;
            if (!(Math.Abs(cx) <= Far) || !(Math.Abs(cy) <= Far))
                throw new ArgumentException("the screen's place is not a number within 1 km of the sign");
            if (!(pitch > 0f && pitch <= Pixel) || !(pitchY >= 0f && pitchY <= Pixel))
                throw new ArgumentException($"pitch must be positive, at most {Pixel:0}");
        }

        public void Step(float now)
        {
            if (!ZNet.instance)                                             // the world left: nothing of it is kept (0.20.130, review)
            {
                if (screens.Count > 0 || parked.Count > 0) StopAll();
                return;
            }
            Expire(now);
            if (screens.Count == 0) return;
            bool play = PluginConfig.PlayGifs.Value;                        // GIFs have a switch of their own (0.20.160)
            Player p = Player.m_localPlayer;
            float far = PluginConfig.PlayDistance.Value;
            Trim(now, p);
            foreach (Screen sc in screens)                                  // the nearest uploads first (FrameBudgetMs): the
                sc.Dist = p && sc.Sign ? Vector3.Distance(p.transform.position, sc.Sign.transform.position) : 0f;   // loop runs from the end
            screens.Sort(Farther);
            double frameMs = 0;
            for (int i = screens.Count - 1; i >= 0; i--)
            {
                Screen sc = screens[i];
                ZDO zdo = sc.Sign ? SignFinder.Zdo(sc.Sign) : null;
                if (zdo == null)                                                 // unloaded or removed
                {
                    Dispose(sc, "its sign is gone", now, keep: true);
                    screens.RemoveAt(i);
                    continue;
                }
                if (zdo.DataRevision != sc.Revision)                             // written since: by another player, the game's
                {                                                                // editor (0.20.130, review: the old GIF stayed
                    sc.Revision = zdo.DataRevision;                              // over new content for good)
                    if (SignMarks.Source(sc.Sign) != sc.Src || SignText.SavedText(sc.Sign).GetHashCode() != sc.TextHash)
                    {
                        Dispose(sc, "its sign was written again", now, keep: true);   // ScreenRestore: back if it still moves
                        screens.RemoveAt(i);
                        continue;
                    }
                }
                if (now - sc.WinAt >= RateWindow)                                // the rate over the last window: a pause
                {                                                                // shows 0, playing again shows the real rate
                    sc.Rate = sc.WinShown / (now - sc.WinAt);
                    sc.WinShown = 0;
                    sc.WinAt = now;
                }
                if (sc.Paused || (sc.Go && !sc.Go.activeSelf))                   // the player's pause (the still shows) or hidden
                {                                                                // under a preview (Veil): no uploads, and the
                    sc.LastUpload = now;                                         // time is no stall for Idle once it plays again
                    continue;
                }
                if (!play)
                {
                    Idle(sc, now, "GIFs are paused (Playing tab)", expected: true);
                    continue;
                }
                float dist = sc.Dist;
                if (dist > far)
                {
                    if (Stale(sc, now)) Idle(sc, now, $"{dist:0} m from the sign, over the Playback Distance of {far:0} m", expected: true);
                    continue;
                }
                long elapsed = (long)((now - sc.Started) * 1000f);
                if (!sc.Loop && elapsed >= sc.Total) elapsed = sc.Total - 1;       // a one-shot keeps its last frame
                int index = FrameTimes.FrameAt(sc.Ends, sc.Loop ? elapsed % sc.Total : elapsed);
                if (index == sc.Index)
                {
                    if (Stale(sc, now)) Idle(sc, now, $"frame {index} of {sc.Frames.Length} is due (the frames last {sc.Total} ms together)");
                    continue;
                }
                if (frameMs > FrameBudgetMs)                                     // the nearer screens took this game frame's
                {                                                                // upload time: at the next one (1.0.2, review)
                    if (Stale(sc, now)) Idle(sc, now, $"the nearer GIFs take the {FrameBudgetMs:0} ms a game frame gives the uploads", expected: true);
                    continue;
                }
                // a screen whose uploads would cost over 50 ms of every second shows every k-th frame instead
                double load = sc.Shown > 0 ? sc.SumMs / sc.Shown * sc.Fps : 0;
                int keep = load > 50 ? (int)Math.Ceiling(load / 50) : 1;
                if (keep > 1 && now - sc.LastUpload < keep / sc.Fps)
                {
                    if (Stale(sc, now)) Idle(sc, now, $"its uploads cost {load:0} ms of every second: every {keep}th frame shows");
                    continue;
                }
                sc.Skipped += ((index - sc.Index - 1) % sc.Frames.Length + sc.Frames.Length) % sc.Frames.Length;
                var sw = Stopwatch.StartNew();
                sc.Tex.LoadRawTextureData(sc.Frames[index]);
                sc.Tex.Apply(false);
                double ms = sw.Elapsed.TotalMilliseconds;
                frameMs += ms;
                sc.SumMs += ms;
                sc.MaxMs = Math.Max(sc.MaxMs, ms);
                sc.Index = index;
                sc.Shown++;
                sc.WinShown++;
                sc.LastUpload = now;
                sc.Idle = false;
            }
        }

        /// <summary>The most the uploads of every screen may take of one game frame, the nearest first (1.0.2, review: the
        /// 50 ms a second rule is each screen's own, so many big GIFs together could stall a weak CPU).</summary>
        const double FrameBudgetMs = 5;
        static readonly Comparison<Screen> Farther = (a, b) => b.Dist.CompareTo(a.Dist);

        /// <summary>Would Idle say something now? Asked before its words are built (1.0.2, review: a string a game frame a screen).</summary>
        static bool Stale(Screen sc, float now) => !sc.Idle && now - sc.LastUpload >= IdleSeconds;

        /// <summary>A screen with no new frame for IdleSeconds says why, once, until it shows one again: a warning, or a
        /// plain line when nothing is wrong (expected: paused, or far away; 1.0.2, the night's test: a walk away put
        /// warnings in the log).</summary>
        static void Idle(Screen sc, float now, string why, bool expected = false)
        {
            if (!Stale(sc, now)) return;
            sc.Idle = true;
            string line = $"screen on {sc.Id} has shown no new frame for {IdleSeconds:0} s: {why}";
            if (expected) Plugin.Log.LogInfo(line);
            else Plugin.Log.LogWarning(line);
        }

        /// <summary>Room for a GIF about to play on `near`: true when the screens leave it; else the screens idle because they
        /// stand beyond the Playback Distance, or paused, park, the farthest first, until it fits (1.0.2, review: the budget
        /// went to whatever loaded first, so a GIF beside the player waited for a far one's zone to unload). False when even
        /// that leaves too little.</summary>
        public bool MakeRoom(long need, Sign near, float now)
        {
            if (Room >= need) return true;
            Player p = Player.m_localPlayer;
            if (!p || !near) return false;
            float far = PluginConfig.PlayDistance.Value;
            Vector3 me = p.transform.position;
            Func<Screen, float, bool> idle = (sc, d) => !ReferenceEquals(sc.Sign, near) && (d > far || sc.Paused);
            // parked only when the idle screens together give what is needed: a set one near screen shares stays whatever
            // its other users do, and nothing is parked for nothing
            long gain = IdleGain(me, idle);
            if (Room + gain < need) return false;
            while (Room < need)
            {
                Screen pick = Farthest(me, idle);
                if (pick == null) return false;
                screens.Remove(pick);
                Dispose(pick, $"parked to make room for a GIF {Vector3.Distance(me, near.transform.position):0} m from you (it stood " +
                              $"{Vector3.Distance(me, pick.Sign.transform.position):0} m away{(pick.Paused ? ", paused" : "")})", now, keep: true);
            }
            return true;
        }

        /// <summary>What parking the screens `idle` says are idle would free: the frames no other screen uses.</summary>
        long IdleGain(Vector3 me, Func<Screen, float, bool> idle)
        {
            var among = new Dictionary<byte[], int>();                      // each frame's users among the idle screens
            foreach (Screen sc in screens)
                if (sc.Sign && idle(sc, Vector3.Distance(me, sc.Sign.transform.position)))
                    foreach (byte[] f in new HashSet<byte[]>(sc.Frames))
                        among[f] = among.TryGetValue(f, out int n) ? n + 1 : 1;
            long gain = 0;
            foreach (KeyValuePair<byte[], int> f in among)
                if (users.TryGetValue(f.Key, out int u) && u == f.Value) gain += f.Key.Length;
            return gain;
        }

        /// <summary>What MakeRoom could free for a GIF on `near` without parking anything yet: the frames of the screens
        /// idle far away or paused that no other screen uses (1.0.2, review: the Moving row warned of GIFs that would play).</summary>
        public long Parkable(Sign near)
        {
            Player p = Player.m_localPlayer;
            if (!p || !near) return 0;
            float far = PluginConfig.PlayDistance.Value;
            return IdleGain(p.transform.position, (sc, d) => !ReferenceEquals(sc.Sign, near) && (d > far || sc.Paused));
        }

        /// <summary>Do frames under this key play on a screen now (parked ones do not: starting them takes their room)?</summary>
        public bool Playing(string key)
        {
            if (key == null) return false;
            foreach (Screen sc in screens)
                if (sc.Key == key) return true;
            return false;
        }

        /// <summary>The farthest screen `takes` allows: one whose frames are its own first (parking it frees them), else any
        /// (a set shared by screens all parked frees as the last of them goes); null when there is none.</summary>
        Screen Farthest(Vector3 me, Func<Screen, float, bool> takes)
        {
            Screen pick = null;
            float farthest = -1f;
            for (int pass = 0; pass < 2 && pick == null; pass++)
                foreach (Screen sc in screens)
                {
                    if (!sc.Sign) continue;
                    float d = Vector3.Distance(me, sc.Sign.transform.position);
                    if (d > farthest && takes(sc, d) && (pass == 1 || OnlyHere(sc) > 0))
                    {
                        farthest = d;
                        pick = sc;
                    }
                }
            return pick;
        }

        /// <summary>The limit lowered (Settings › Playback › GIF memory, 1.0.2): the farthest screens park until the rest fit,
        /// and the oldest parked sets go until everything kept fits.</summary>
        void Trim(float now, Player p)
        {
            long budget = Budget;
            while (held > budget && screens.Count > 0)
            {
                Screen pick = Farthest(p ? p.transform.position : Vector3.zero, (sc, d) => true);
                if (pick == null) break;
                screens.Remove(pick);
                Dispose(pick, $"stopped: the GIF memory is {budget >> 20} MB now (the farthest first)", now, keep: true);
            }
            while (held + parkedBytes > budget && parked.Count > 0) DropOldest("over the GIF memory");
        }

        /// <summary>Stops the screen over this sign (null: every screen). keep: its frames are parked, to play again
        /// without the file (a stack rewritten or made again); false: dropped (the player's Stop, the API's).</summary>
        public int Stop(Sign only, bool keep = true)
        {
            int n = 0;
            for (int i = screens.Count - 1; i >= 0; i--)
            {
                if (!ReferenceEquals(only, null) && !ReferenceEquals(screens[i].Sign, only)) continue;
                Dispose(screens[i], keep ? "stopped (its frames parked)" : "stopped", Time.unscaledTime, keep);
                screens.RemoveAt(i);
                n++;
            }
            return n;
        }

        /// <summary>Every screen and every parked set goes (the plugin shuts down).</summary>
        public void StopAll()
        {
            Stop(null, keep: false);
            parked.Clear();
            parkedBytes = 0;
        }

        /// <summary>Does a screen play over this sign (paused or not)?</summary>
        public bool Plays(Sign s) => screens.Exists(sc => ReferenceEquals(sc.Sign, s));

        /// <summary>Is the screen over this sign paused by the player?</summary>
        public bool Paused(Sign s) => screens.Exists(sc => ReferenceEquals(sc.Sign, s) && sc.Paused);

        /// <summary>The screen over any of these signs (its object), or null: the frame in the world lies in front of it
        /// (Ghost, 0.20.105).</summary>
        public Transform ScreenOver(IList<Sign> stack)
        {
            foreach (Screen sc in screens)
                if (sc.Go)
                    foreach (Sign o in stack)
                        if (ReferenceEquals(sc.Sign, o)) return sc.Go.transform;
            return null;
        }

        /// <summary>Hides the screens over these signs (on) or shows them again, on this computer: a new pick over the
        /// stack (HeldVeil, 0.20.95). They play on meanwhile; a screen made while hidden shows.</summary>
        public void Veil(IList<Sign> stack, bool on)
        {
            foreach (Screen sc in screens)
            {
                bool over = false;
                for (int j = 0; j < stack.Count; j++)
                {
                    Sign o = stack[j];
                    if (ReferenceEquals(sc.Sign, o)) over = true;
                    else
                        for (int i = 0; i < sc.Hidden.Count && !over; i++)   // plain loops: no closure per pair, every Layout
                            over = ReferenceEquals(sc.Hidden[i], o);       // (0.20.132, review)
                    if (over) break;
                }
                if (over && sc.Go && sc.Go.activeSelf == on) sc.Go.SetActive(!on);
            }
        }

        /// <summary>Pauses the screen over this sign (null: every screen) or plays it on: its frames and its row stay,
        /// the still shows meanwhile. Returns how many screens changed.</summary>
        public int Pause(Sign only, bool paused)
        {
            int n = 0;
            foreach (Screen sc in screens)
            {
                if (!ReferenceEquals(only, null) && !ReferenceEquals(sc.Sign, only)) continue;
                if (sc.Paused == paused) continue;
                sc.Paused = paused;
                if (!paused)                                                     // plays on from the frame it stopped at
                {
                    long at = sc.Index > 0 && sc.Index <= sc.Ends.Length ? sc.Ends[sc.Index - 1] : 0;
                    sc.Started = Time.unscaledTime - at / 1000f;
                }
                n++;
            }
            return n;
        }

        void Dispose(Screen sc, string why, float now, bool keep)
        {
            long freed = 0;
            foreach (byte[] f in new HashSet<byte[]>(sc.Frames))           // a frame another screen shows stays counted
            {
                if (!users.TryGetValue(f, out int u)) continue;
                if (u > 1) users[f] = u - 1;
                else
                {
                    users.Remove(f);
                    held -= f.Length;
                    freed += f.Length;
                }
            }
            if (keep) Park(sc, now);
            if (freed > 0) Plugin.Restore?.RoomFreed();                     // a GIF waiting for room is looked at again now
            if (sc.Go) Object.Destroy(sc.Go);
            if (sc.Tex) Object.Destroy(sc.Tex);
            int shown = 0;
            foreach (Sign o in sc.Hidden)
                if (o)
                {
                    o.m_textWidget.enabled = true;
                    shown++;
                }
            Plugin.Log.LogInfo($"screen on {sc.Id} {why} after {now - sc.Started:0.0} s: {sc.Shown} frames shown, {sc.Skipped} skipped, " +
                               $"the text of {shown} of {sc.Hidden.Count} sign(s) shows again; {parked.Count} parked, {Held >> 20} MB held");
        }

        /// <summary>One row per screen for the menu, with its sign (to pause or play it) and whether it is paused.</summary>
        public List<Row> Describe(float now)
        {
            var list = new List<Row>();
            bool play = PluginConfig.PlayGifs.Value;
            var ordered = new List<Screen>(screens);                       // a steady order (review: the rows swapped places as
            ordered.Sort((a, b) => string.CompareOrdinal(a.Id, b.Id));      // you walked: Step sorts the screens by distance)
            foreach (Screen sc in ordered)
            {
                string state = sc.Paused ? "paused" : !play ? "frozen (Play all GIFs)" : $"{sc.Rate:0.#} shown/s";
                list.Add(new Row
                {
                    Text = $"{sc.W}x{sc.H}, {sc.Frames.Length} frames at {sc.Fps:0.#} fps ({state})" +
                           (sc.Lit ? ", lit by the scene" : ", glowing") + (sc.Hidden.Count > 1 ? $", covers {sc.Hidden.Count} signs" : ""),
                    Sign = sc.Sign,
                    Paused = sc.Paused,
                });
            }
            return list;
        }

        public string Stats(float now)
        {
            var sb = new StringBuilder("[");
            for (int i = 0; i < screens.Count; i++)
            {
                Screen sc = screens[i];
                if (i > 0) sb.Append(',');
                sb.Append("{\"id\":").Append(Json.Str(sc.Id))
                  .Append(",\"w\":").Append(sc.W).Append(",\"h\":").Append(sc.H)
                  .Append(",\"frames\":").Append(sc.Frames.Length)
                  .Append(",\"fps\":").Append(Json.F(sc.Fps))
                  .Append(",\"shown\":").Append(sc.Shown)
                  .Append(",\"realFps\":").Append(Json.F(sc.Rate))
                  .Append(",\"paused\":").Append(sc.Paused ? "true" : "false")
                  .Append(",\"skipped\":").Append(sc.Skipped)
                  .Append(",\"avgMs\":").Append(Json.F(sc.Shown > 0 ? sc.SumMs / sc.Shown : 0))
                  .Append(",\"maxMs\":").Append(Json.F(sc.MaxMs))
                  .Append(",\"mb\":").Append(Json.F(sc.Bytes / 1048576.0))
                  .Append(",\"lit\":").Append(sc.Lit ? "true" : "false")
                  .Append('}');
            }
            return sb.Append(']').ToString();
        }
    }
}
