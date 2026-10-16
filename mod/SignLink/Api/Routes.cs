using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using SignLink.Animation;
using SignLink.Pictures;
using SignLink.Signs;
using UnityEngine;

namespace SignLink.Api
{
    /// <summary>The local API: every handler runs on the main thread (MainThread.Run). The endpoints are listed in
    /// mod/README.md (API); this class only maps them.</summary>
    internal sealed class Routes
    {
        const char FrameSep = '\u001e', SignSep = '\u001d';
        readonly TextAnimator text;
        readonly ScreenPlayer screens;

        public Routes(TextAnimator text, ScreenPlayer screens)
        {
            this.text = text;
            this.screens = screens;
        }

        public string Handle(Request r)
        {
            var a = r.Query;
            switch (r.Method + " " + r.Path)
            {
                case "GET /ping":
                    if (!r.Authenticated) return "{\"ok\":true,\"version\":\"" + Plugin.Version + "\"}";
                    return MainThread.Run(Ping);
                case "GET /signs": return MainThread.Run(() => Signs(Num(a, "radius", 50f)));
                case "GET /hovered": return MainThread.Run(Hovered);
                case "GET /sign": return MainThread.Run(() => ReadSign(Req(a, "id")));
                case "POST /sign":
                    string body = r.BodyText;
                    return MainThread.Run(() => WriteSign(Req(a, "id"), body, Local(a)));
                case "POST /anim":
                    string[] ids = (a.TryGetValue("ids", out string list) ? list : Req(a, "id"))
                        .Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
                    string anim = r.BodyText;
                    int count = HttpRules.Parts(anim, FrameSep);                // counted before the per-frame arrays are made (0.20.132, review)
                    if (count > HttpRules.MaxAnimFrames)
                        throw new ArgumentException($"{count} frames: at most {HttpRules.MaxAnimFrames} in one animation");
                    string[] raw = anim.Split(FrameSep);
                    var frames = new string[raw.Length][];
                    for (int k = 0; k < raw.Length; k++)
                    {
                        frames[k] = ids.Length == 1 ? new[] { raw[k] } : raw[k].Split(SignSep);
                        if (frames[k].Length != ids.Length)
                            throw new ArgumentException($"frame {k} has {frames[k].Length} texts for {ids.Length} signs");
                    }
                    if (Opt(a, "store", "0") != "0")
                        return MainThread.Run(() => StoreAnim(ids, frames, Num(a, "fps", 10f)));
                    return MainThread.Run(() => text.Start(ids, frames, Num(a, "fps", 10f), Local(a), Opt(a, "loop", "1") != "0",
                                                           Time.unscaledTime));
                case "POST /anim/unstore":
                    return MainThread.Run(() => "{\"ok\":true,\"removed\":" +
                                                (Plugin.Stored.Remove(SignFinder.Find(Req(a, "id"))) ? "true" : "false") + "}");
                case "POST /anim/stop":
                    return MainThread.Run(() => "{\"ok\":true,\"stopped\":" + text.Stop(OptSign(a)) + "}");
                case "GET /anim":
                    return MainThread.Run(() => "{\"ok\":true,\"anims\":" + text.Stats(Time.unscaledTime) +
                                                ",\"stored\":" + Plugin.Stored.Count + "}");
                case "POST /screen":
                    byte[] rgb = r.Body;
                    return MainThread.Run(() => screens.Start(Req(a, "id"), (int)Num(a, "w", 0), (int)Num(a, "h", 0), Num(a, "fps", 10f),
                        Num(a, "cx", 0f), Num(a, "cy", 0f), Num(a, "pitch", 2f), Opt(a, "loop", "1") != "0",
                        Opt(a, "hide", "1") != "0", Opt(a, "filter", "point") == "point", Opt(a, "lit", "0") != "0", rgb, Time.unscaledTime));
                case "POST /screen/stop":
                    return MainThread.Run(() =>
                    {
                        Sign only = OptSign(a);
                        Plugin.Restore.Stopped(only);                       // a moving GIF's screen is not started again
                        return "{\"ok\":true,\"stopped\":" + screens.Stop(only, keep: false) + "}";
                    });
                case "POST /signs/clone":
                    // 0 (the default): the spacing the window's copies take (0.3 mm on a tipped sign, else 0.5 mm; 0.20.130, review);
                    // else 0.3-2 mm, and the copies must fit one stack at it, checked here, before anything runs on the main
                    // thread and before the texts are split (0.20.132, review: a step of 1e-9 m let millions through)
                    float step = Num(a, "step", 0f);
                    string copies = r.BodyText;
                    string why = StackMath.CloneProblem(step, HttpRules.Parts(copies, SignSep), SignFinder.MaxGap, SignFinder.StackDepth);
                    if (why != null) throw new ArgumentException(why);
                    string[] texts = copies.Split(SignSep);
                    return MainThread.Run(() =>
                    {
                        SignBuilder.Guard();
                        Sign src = SignFinder.Find(Req(a, "id"));
                        if (src) SignText.RequireRoom(src, SignPose.Stored(src) + SaveCost.Bytes(texts), "the stack with these copies");   // the multiplayer limits
                        List<Sign> made = SignBuilder.Clone(src, texts, step);
                        List<Sign> stack = SignFinder.Stack(src);
                        ZDO first = stack.Count > 0 ? SignFinder.Zdo(stack[0]) : null;
                        if (first != null)
                        {
                            stack[0].m_nview.ClaimOwnership();              // as every other write (0.20.152, review)
                            first.Set(SignFinder.CountKey, stack.Count);    // as SetContent: what a client must see
                        }
                        return "{\"ok\":true,\"ids\":" + Ids(made) + "}";
                    });
                case "POST /signs/delete":
                    string[] gone = Req(a, "ids").Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
                    return MainThread.Run(() =>
                    {
                        SignBuilder.Guard();
                        var all = SignFinder.ById();
                        var signs = new List<Sign>();
                        foreach (string id in gone)                             // not in a no-build place, as the hammer (0.20.151, review)
                            if (all.TryGetValue(id, out Sign s) && !Location.IsInsideNoBuildLocation(s.transform.position)) signs.Add(s);
                        return "{\"ok\":true,\"deleted\":" + SignBuilder.Delete(signs) + "}";
                    });
                case "GET /stack":
                    return MainThread.Run(() => StackInfo(SignFinder.Find(Req(a, "id"))));
                case "POST /pose":
                    return MainThread.Run(() =>
                    {
                        Sign s = SignFinder.Find(Req(a, "id"));
                        Sign root = SignFinder.Root(s);
                        Fit to = SignFit.Read(root, out _, out _, out _);
                        // as the window's rows: the content keeps its place on the face (Fit.Posed), not Up and Out as
                        // built, which a tilt sent into the face's normal, where a picture cannot be (0.20.157, review)
                        double tilt = HasTilt(a) ? TiltArg(a) : to.Tilt, turn = a.ContainsKey("turn") ? Num(a, "turn", 0f) : to.Turn;
                        to = to.Posed(turn, tilt, HasTilt(a) ? false : to.Edge);
                        string recipe = SignMarks.Source(root);               // rewritten texts lose it: put it back, as the window does
                        List<Sign> made = SignFit.Refit(s, to, GlowMode.AsMade, out _);
                        if (recipe.Length > 0 && made.Count > 0) SignMarks.SetSource(made[0], recipe);
                        return "{\"ok\":true,\"ids\":" + Ids(made) + "}";
                    });
                case "POST /image":
                    return Image(a, r.Body);
                case "GET /screens":
                    return MainThread.Run(() => "{\"ok\":true,\"screens\":" + screens.Stats(Time.unscaledTime) + "}");
                default: throw new ArgumentException($"unknown endpoint {r.Method} {r.Path}");
            }
        }

        static bool HasTilt(Dictionary<string, string> a) => a.ContainsKey("tilt") || a.ContainsKey("pose") || a.ContainsKey("lying");

        /// <summary>tilt=-90..90 (degrees over the sign's bottom edge, + face up), or pose=standing|floor|ceiling (0, 90,
        /// -90), or the older lying=1|0.</summary>
        static float TiltArg(Dictionary<string, string> a)
        {
            if (a.ContainsKey("tilt"))
            {
                float t = Num(a, "tilt", 0f);
                if (t < -90f || t > 90f) throw new ArgumentException("tilt is -90..90 degrees");
                return t;
            }
            string p = Opt(a, "pose", Opt(a, "lying", "0") != "0" ? "floor" : "standing").ToLowerInvariant();
            if (p == "floor" || p == "lying" || p == "up") return 90f;
            if (p == "ceiling" || p == "down") return -90f;
            if (p == "standing") return 0f;
            throw new ArgumentException("pose is standing, floor or ceiling (or tilt=-90..90)");
        }

        /// <summary>POST /image: decode on the main thread, encode here (the request's own thread), place on the main
        /// thread.</summary>
        static string Image(Dictionary<string, string> a, byte[] file)
        {
            string id = Req(a, "id");
            var o = new PictureOptions
            {
                WidthM = Num(a, "width", 0f),
                Pixels = (int)Num(a, "pixels", 200000),
                Pitch = Num(a, "pitch", 1f),
                Detail = (int)Num(a, "detail", 0f),                         // pixels across (with width=): the window's model
                Cover = Num(a, "cover", 2f),
                RightM = Num(a, "x", 0f),
                OutM = Num(a, "out", 0f),
                Animate = Opt(a, "animate", "1") != "0",
                MaxKb = Num(a, "maxkb", 400f),
                Glow = Opt(a, "glow", "1") != "0",
                Transparent = Opt(a, "alpha", "1") != "0",
            };
            if (o.Pixels < 100 || o.Pixels > 2000000) throw new ArgumentException("pixels must be 100..2000000");
            if (!(o.MaxKb >= 16 && o.MaxKb <= 400))
                throw new ArgumentException("maxkb is 16..400: a sign stays under the 512 KiB other players can receive (0.20.39: no uncapped mode)");
            if (!(o.Cover == 0f || o.Cover == 2f)) throw new ArgumentException("cover is 0 or 2 (seamless pixels)");
            if (!PictureOptions.ValidPitch(o.Pitch))
                throw new ArgumentException("pitch must be a multiple of 0.125 up to 40 whose half TMP reads exactly (0.125, 0.25, 0.5, 0.625, 0.75, 1, 1.25, 1.5, 2 ...)");
            if (o.Detail != 0 && (o.Detail < 1 || o.Detail > 4000 || o.WidthM <= 0))
                throw new ArgumentException("detail is 1..4000 pixels across, with width=");
            string place = Opt(a, "place", "above");
            if (a.ContainsKey("up")) { o.Place = Placement.Custom; o.UpM = Num(a, "up", 0f); }
            else if (a.ContainsKey("above")) o.BottomM = Num(a, "above", 1f);
            else if (place == "centred" || place == "centered") o.Place = Placement.Centred;
            else if (place == "below") o.Place = Placement.Below;
            else if (place != "above") throw new ArgumentException("place is above, centred or below");
            MainThread.Run(() =>
            {
                SignPose.Read(SignFinder.Find(id), out float tilt, out float turn);
                o.TiltDeg = HasTilt(a) ? TiltArg(a) : tilt;
                o.TurnDeg = a.ContainsKey("turn") ? Num(a, "turn", 0f) : turn;
                return true;
            });
            if (Math.Abs(o.TiltDeg) > 0.05f && !a.ContainsKey("up") && !a.ContainsKey("above") && !a.ContainsKey("place"))
                o.Place = Placement.Centred;                               // tipped: centred on the sign unless asked
            if (Math.Abs(o.RightM) > 300 || Math.Abs(o.UpM) > 300 || Math.Abs(o.OutM) > 300 || (o.BottomM ?? 0) < -300 || (o.BottomM ?? 0) > 300 || o.WidthM < 0 || o.WidthM > 200)
                throw new ArgumentException("x, up and above must be within 300 m of the sign; width 0..200 m");
            EncodedPicture pic;
            if (GifDecoder.IsGif(file)) pic = PictureImporter.EncodeGif(file, o);
            else
            {
                int w = 0, h = 0;
                byte[] rgba = MainThread.Run(() => PictureImporter.Decode(file, out w, out h));
                pic = PictureImporter.Encode(rgba, w, h, o);
            }
            pic.Name = SafeName(Opt(a, "name", ""));                        // a recipe on the sign: the window edits it
            var sb = new StringBuilder("{\"ok\":true,\"w\":" + pic.W + ",\"h\":" + pic.H + ",\"pitch\":" + Json.F(pic.Pitch) + ",\"signs\":" + pic.Bands.Count +
                                       ",\"kb\":[");
            for (int i = 0; i < pic.Bands.Count; i++) sb.Append(i > 0 ? "," : "").Append(pic.Bands[i].Bytes / 1024);
            sb.Append("],\"saveKb\":").Append(pic.SaveBytes / 1024).Append(",\"encodeMs\":").Append(Json.F(pic.Ms));
            if (Opt(a, "dry", "0") != "0") return sb.Append('}').ToString();
            return MainThread.Run(() =>
            {
                List<Sign> placed = PictureImporter.Place(SignFinder.Find(id), pic, o);
                return sb.Append(",\"ids\":").Append(Ids(placed)).Append('}').ToString();
            }, 60000);
        }

        /// <summary>One stored animation per sign, all on the world clock (so a group stays in step).</summary>
        static string StoreAnim(string[] ids, string[][] frames, float fps)
        {
            var all = SignFinder.ById();
            var stacks = new HashSet<Sign>();
            foreach (string id in ids)                                      // all of them, or none changes
            {
                if (!all.TryGetValue(id, out Sign s)) throw new ArgumentException($"no loaded sign with id {id}");
                SignText.RequireEdit(s);
                if (!stacks.Add(SignFinder.Root(s)))                        // storing on one would clear the other's stack
                    throw new ArgumentException($"sign {id} is in the same stack as another of these: one stored animation a stack");
            }
            var sb = new StringBuilder("{\"ok\":true,\"stored\":[");
            for (int i = 0; i < ids.Length; i++)
            {
                Sign s = all[ids[i]];
                var mine = new string[frames.Length];
                for (int k = 0; k < frames.Length; k++) mine[k] = frames[k][i];
                sb.Append(i > 0 ? "," : "").Append(Plugin.Stored.Store(s, mine, fps));
            }
            return sb.Append("]}").ToString();
        }

        static string StackInfo(Sign s)
        {
            var sb = new StringBuilder("{\"ok\":true,\"signs\":[");
            List<Sign> stack = SignFinder.Stack(s);
            for (int i = 0; i < stack.Count; i++)
            {
                string t = SignText.SavedText(stack[i]);
                sb.Append(i > 0 ? "," : "").Append("{\"id\":").Append(Json.Str(SignFinder.Id(stack[i])))
                  .Append(",\"len\":").Append(t.Length).Append(",\"bytes\":").Append(Encoding.UTF8.GetByteCount(t)).Append('}');
            }
            return sb.Append("]}").ToString();
        }

        static string Ids(List<Sign> signs)
        {
            var sb = new StringBuilder("[");
            for (int i = 0; i < signs.Count; i++) sb.Append(i > 0 ? "," : "").Append(Json.Str(SignFinder.Id(signs[i])));
            return sb.Append(']').ToString();
        }

        static string Ping()
        {
            Player p = Player.m_localPlayer;
            var sb = new StringBuilder("{\"ok\":true,\"version\":\"" + Plugin.Version + "\",\"inWorld\":" + (p ? "true" : "false"));
            if (p) sb.Append(",\"player\":").Append(Json.Vec(p.transform.position));
            return sb.Append('}').ToString();
        }

        static string Signs(float radius)
        {
            Player p = Player.m_localPlayer;
            if (!p) throw new InvalidOperationException("not in a world");
            Vector3 me = p.transform.position;
            var near = new List<Sign>();
            var dist = new Dictionary<Sign, float>();
            foreach (Sign s in SignFinder.Loaded())
            {
                float d = Vector3.Distance(me, s.transform.position);
                if (d <= radius && SignFinder.Id(s) != null) { near.Add(s); dist[s] = d; }
            }
            near.Sort((x, y) => dist[x].CompareTo(dist[y]));
            var sb = new StringBuilder("{\"ok\":true,\"signs\":[");
            for (int i = 0; i < near.Count; i++)
            {
                if (i > 0) sb.Append(',');
                sb.Append(Describe(near[i], dist[near[i]]));
            }
            return sb.Append("]}").ToString();
        }

        static string Hovered()
        {
            Sign s = SignFinder.Hovered();
            if (!s) return "{\"ok\":true,\"sign\":null}";
            Player p = Player.m_localPlayer;
            return "{\"ok\":true,\"sign\":" + Describe(s, Vector3.Distance(p.transform.position, s.transform.position)) + "}";
        }

        static string Describe(Sign s, float dist)
        {
            string t = SignText.SavedText(s);
            return "{\"id\":" + Json.Str(SignFinder.Id(s)) + ",\"dist\":" + Json.F(dist) + ",\"pos\":" + Json.Vec(s.transform.position) +
                   ",\"yaw\":" + Json.F(s.transform.rotation.eulerAngles.y) + ",\"len\":" + t.Length +
                   ",\"preview\":" + Json.Str(t.Length > 60 ? t.Substring(0, 60) : t) + "}";
        }

        static string ReadSign(string id) =>
            "{\"ok\":true,\"id\":" + Json.Str(id) + ",\"text\":" + Json.Str(SignText.SavedText(SignFinder.Find(id))) + "}";

        string WriteSign(string id, string body, bool local)
        {
            Sign s = SignFinder.Find(id);
            if (!local && s)                                                // the stack with this text in place of the sign's
            {
                List<Sign> stack = SignFinder.Stack(s);
                long now = SignPose.Stored(stack.Count > 0 ? stack[0] : s);
                SignText.RequireRoom(s, now - Encoding.UTF8.GetByteCount(SignText.SavedText(s)) + Encoding.UTF8.GetByteCount(body ?? ""), "this sign");
            }
            text.StopFor(s);
            double ms = SignText.Apply(s, body, local);
            return "{\"ok\":true,\"ms\":" + Json.F(ms) + ",\"chars\":" + body.Length + "}";
        }

        /// <summary>A file name the page sends (name=): its last part, or "" when Windows would not take it.</summary>
        static string SafeName(string name)
        {
            try { return string.IsNullOrEmpty(name) ? "" : System.IO.Path.GetFileName(name); }
            catch (ArgumentException) { return ""; }
        }

        static Sign OptSign(Dictionary<string, string> a) => a.TryGetValue("id", out string id) && id.Length > 0 ? SignFinder.Find(id) : null;

        static string Req(Dictionary<string, string> a, string k) =>
            a.TryGetValue(k, out string v) && v.Length > 0 ? v : throw new ArgumentException($"missing ?{k}=");

        static string Opt(Dictionary<string, string> a, string k, string dflt) => a.TryGetValue(k, out string v) ? v : dflt;

        /// <summary>A number argument: finite, or the request fails (NaN would pass every range check).</summary>
        static float Num(Dictionary<string, string> a, string k, float dflt)
        {
            if (!a.TryGetValue(k, out string v)) return dflt;
            if (!float.TryParse(v, NumberStyles.Float, CultureInfo.InvariantCulture, out float f) || float.IsNaN(f) || float.IsInfinity(f))
                throw new ArgumentException($"?{k}= must be a number");
            return f;
        }

        static bool Local(Dictionary<string, string> a)
        {
            string m = Opt(a, "mode", "local");
            if (m != "local" && m != "zdo") throw new ArgumentException("mode is local or zdo");
            return m == "local";
        }
    }
}
