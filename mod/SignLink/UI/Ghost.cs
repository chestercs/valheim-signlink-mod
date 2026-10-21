using System.Collections.Generic;
using SignLink.Pictures;
using SignLink.Presets;
using SignLink.Signs;
using SignLink.UI.Sources;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SignLink.UI
{
    /// <summary>A frame in the world where the content ends up: the content's rectangle (an outline, a mark at the
    /// middle of its top edge and a veil over it, green where it shows and red where hidden, grey stripes seen from behind;
    /// 0.20.76, 0.20.112-0.20.113) and the outline of the sign's board in the pending pose.
    /// Drawn as quads under a sign's own world-space canvas (see FrameMaterial: no depth test, drawn after every canvas,
    /// so it shows through walls and floors, 0.20.30; RawImage bars when the build lacks the shader, the path the GIF
    /// screens use, verified in game, and then the fill only over a single sign), added last so it draws over that
    /// sign's text, unlit (it glows at night, fine for a marker). Its maths is placement's: SignPose.Preview for the
    /// sign in the pending pose, Fit.Centre for the content's middle, the text widget's transform for text-local units.
    /// Rebuilt only when what it shows changes. The bars hang under the canvas of the stack's frontmost copy: world-space
    /// canvases draw nearest last, so under the first sign's canvas the frame was drawn under the text of the copies in
    /// front of it and showed over one band only (0.20.0-0.20.1); a canvas of its own with a higher sorting order
    /// (0.20.2-0.20.3) did not show in game at all.</summary>
    internal sealed partial class Ghost
    {
        /// <summary>The selection's colours: the content in green where it shows, the board in a soft warm grey (0.20.88, the
        /// author: I do not like the yellow either, let us have better colours, a smoother colour for the sign's border
        /// too: amber and bone before, then teal; 0.20.94, the author: what is in the right place greenish instead of
        /// yellowish: green in place, red behind; 0.20.100, the author: I like neither the green nor the red, these
        /// washed-out milky colours: a deep emerald, shown as chosen: OnScreen). The fill is one even veil, green over
        /// what shows and red over what is hidden (SeenAlpha, 0.20.112-0.20.113); seen from behind it is grey stripes
        /// (Hatch, 0.20.76: a third of the content under them, the picture plain between them). The colours are as meant
        /// on screen (sRGB); the board a neon lime that glows (0.20.114-0.20.115 yellow; 0.20.127, BoardGlow).</summary>
        public static readonly Color Fill = new Color(0.16f, 0.72f, 0.34f, 0.25f), Line = new Color(0.16f, 0.72f, 0.34f, 0.92f),
                                     Board = new Color(0.80f, 1.00f, 0.05f, 1f);   // a neon lime (0.20.127, the author: I can barely see the sign's
                                                                                    // border now, it should be a very bright neon green or
                                                                                    // yellow; a neon yellow 0.20.114-0.20.115)
        /// <summary>The content seen from behind (Ghost.Side): the soft warm grey the board's outline was before 0.20.114.</summary>
        static readonly Color Neutral = new Color(0.89f, 0.88f, 0.85f, 1f);
        /// <summary>The hover frame's colour (Select sign, a row of Signs nearby under the cursor): the content as the
        /// selection's, and the sign's board a light cool white instead of the soft grey, so the two frames read alike and
        /// the board's colour alone says "under the cursor" against "selected".</summary>
        public static readonly Color HoverBoard = new Color(0.95f, 0.97f, 1f, 0.85f);
        /// <summary>The content's frame where something stands in front of it (a floor it went under, a wall): a deep red
        /// instead of the green (0.20.86, the author: the frame showed through everything, so a picture slipping under the
        /// floor did not show; 0.20.88: the rose of before was too bright and close to the amber; 0.20.89 a darker violet;
        /// 0.20.94, the author: the content layer reddish instead of violet; 0.20.100: a deep crimson, shown as
        /// chosen).</summary>
        public static readonly Color Behind = new Color(0.80f, 0.12f, 0.14f, 0.85f);
        /// <summary>The board's frame while nothing holds the sign where the steps or the pose to come put it (1.0.2, the
        /// author: red instead of the white border when the sign has the "nothing holds it" problem, a red a little apart
        /// from the picture's, well seen): a hot scarlet, lighter and warmer than the content's crimson, glowing as the
        /// lime does but less (UnheldGlow; a warmer red, or this one at the lime's glow, read as orange). The same words stand in red on 2 Content and 3 Place (ContentTab.PoseProblem).</summary>
        public static readonly Color Unheld = new Color(1.00f, 0.12f, 0.06f, 1f);

        /// <summary>A colour as the bars' vertex colours need it (0.20.100): in a linear colour space Unity takes vertex
        /// colours as they are, so a colour meant for the screen showed lighter and paler (the author: washed-out milky
        /// colours; the rose of 0.20.86 too bright, the violet to be darker); converted, it shows as chosen.</summary>
        static Color OnScreen(Color c) => QualitySettings.activeColorSpace == ColorSpace.Linear ? c.linear : c;

        static Color BehindOf(Color c) => new Color(Behind.r, Behind.g, Behind.b, Mathf.Min(c.a, Behind.a));

        /// <summary>The fill over what shows: one even green veil, not stripes (0.20.112, the author: a green layer should
        /// be solid, not striped, it would show much better; chose a see-through one, the picture showing under it).
        /// Hidden parts the same in red (0.20.113, the author: make the red solid too instead of striped); from behind it
        /// stays grey stripes.</summary>
        const float SeenAlpha = 0.125f;                                     // 45 % until 0.20.136 (the author: make the red and green layer over the picture more see-through), 25 % until 0.20.174 (twice as see-through)

        /// <summary>The content's colour seen from behind the sign: the board's, as it was (0.20.106).</summary>
        static Color NeutralOf(Color c) => new Color(Neutral.r, Neutral.g, Neutral.b, c.a);
        const float Thick = 0.012f, Ahead = 0.01f;
        /// <summary>The board's outline a little thicker than the content's (0.20.114, the author: the sign's border a tiny
        /// bit thicker too).</summary>
        const float BoardThick = 0.022f;

        /// <summary>The selected sign's board drawn this many times brighter than its colour (in the linear space the game
        /// lights in): over 1 the game's bloom makes it glow where the camera renders in HDR, else it shows at full
        /// strength (0.20.127). The hover frame's board does not glow.</summary>
        const float BoardGlow = 2.2f;
        readonly float boardGlow;
        /// <summary>How far the content's bars sit in front of the text's plane (0.20.101): green and red are decided by
        /// depth where the bars are, so they must lie where the text does. A centimetre in front (Ahead, as the board's
        /// outline still is) said "shows" for a picture sunk just under a floor's surface, which showed only through the
        /// floor's gaps (the author: the picture shows between the cube floor pieces, yet the layer says it shows); a
        /// millimetre keeps them over the text and off the sign's own board.</summary>
        const float OnText = 0.001f;
        /// <summary>The same over a moving GIF's screen (0.20.105): one lit by the scene writes depth where it shows, so the
        /// bars lie a few millimetres in front of where it is measured (the author: while editing, my existing picture
        /// still flickers).</summary>
        const float OverScreen = 0.004f;

        readonly Color fillColor, lineColor, boardColor;
        GameObject root;
        Sign sign;
        Bar fill, mark;
        readonly Bar[] edge = new Bar[4], board = new Bar[4];
        string shownKey;
        Inputs shownInputs;

        /// <summary>What the frame shown was made from, compared field by field before its key is built (0.20.132, review:
        /// the key, up to nineteen values formatted, some 3-5 KB, was built every Layout to find that nothing changed). The
        /// same fields make the same key, so the key is built only when one changed, and then decides as before (a change
        /// finer than the key writes it, a fraction of a centimetre, makes nothing again; KeyDiff still reads its parts).</summary>
        struct Inputs
        {
            public bool Stack, HasTarget, HasHost, HasRevision, Edge, Sized, Content, Lit, Screen, Board;
            public ZDOID Target, Host;
            public double Scale, Right, Up, Out, Tilt, Turn, Wwu, Hwu, C0x, C0y;
            public Vector3 Position, Pending;
            public Quaternion Rotation;
            public int Count;
            public uint Revision;

            /// <summary>Exactly the same (Unity's == on vectors and rotations is approximate: Equals is not).</summary>
            public bool Same(Inputs o) =>
                Stack == o.Stack && HasTarget == o.HasTarget && HasHost == o.HasHost && HasRevision == o.HasRevision && Edge == o.Edge &&
                Sized == o.Sized && Content == o.Content && Lit == o.Lit && Screen == o.Screen && Board == o.Board && Target == o.Target && Host == o.Host &&
                Scale == o.Scale && Right == o.Right && Up == o.Up && Out == o.Out && Tilt == o.Tilt && Turn == o.Turn && Wwu == o.Wwu &&
                Hwu == o.Hwu && C0x == o.C0x && C0y == o.C0y && Position.Equals(o.Position) && Pending.Equals(o.Pending) &&
                Rotation.Equals(o.Rotation) && Count == o.Count && Revision == o.Revision;
        }

        static ZDOID Uid(Sign s, out bool has)
        {
            ZDO z = SignFinder.Zdo(s);
            has = z != null;
            return has ? z.m_uid : ZDOID.None;
        }

        /// <summary>One bar of the frame: a quad with its own renderer (the usual path), or a RawImage under the canvas
        /// (the fallback when the build has no shader for the quads).</summary>
        sealed class Bar
        {
            public GameObject Go;
            public Transform T;
            public RawImage Img;
            /// <summary>The content's bars: the same quad again, drawn only where something stands in front of it, in
            /// Behind's colour (null: the board's bars, and the fallbacks).</summary>
            public MeshFilter Behind;
            public bool Quad => Img == null;
            /// <summary>A content bar's looks: green or red by depth from the front (Seen, with Behind), or one neutral
            /// colour that tests no depth from behind (Neutral; 0.20.106).</summary>
            public Mesh Seen, Neutral;
        }

        /// <summary>The selection's frame.</summary>
        public Ghost() : this(Fill, Line, Board) { }

        /// <summary>A frame in these colours (Select sign's hover frame).</summary>
        public Ghost(Color fill, Color line, Color board)
        {
            fillColor = fill;
            lineColor = line;
            boardColor = board;
            boardGlow = board == Board ? BoardGlow : 1f;                     // the selection's board glows, the hover's not
        }

        /// <summary>Layout events only: shows the frame for the session's choice on the selected sign, or hides it.</summary>
        /// <summary>The frame for the selection as the window's state has it; `unheld`: the game would not hold the sign
        /// where its steps or the pose to come put it (the board's bars in Unheld's colour).</summary>
        public void Update(Session s, Source src, bool unheld = false)
        {
            // two switches, each its own part (1.1.0, the author: Highlight signs the board's frame, Highlight sign contents the
            // content's; one no longer switches the other off)
            bool boardOn = PluginConfig.Highlight.Value, contentOn = PluginConfig.HighlightContent.Value;
            if (!boardOn && !contentOn || !s.HasTarget || src == null)
            {
                if (!boardOn && !contentOn && s.HasTarget) Note("hidden: disabled (Settings › Sign options: both highlights off)");
                Hide();
                return;
            }
            Sign target = s.First;
            Sign host = s.Stack.Count > 0 ? s.Stack[s.Stack.Count - 1] : target;   // the frontmost copy: its canvas draws last
            if (!target || !target.m_textWidget || !host || !host.m_textWidget)
            {
                Note("hidden: the selected sign has no text yet");
                Hide();
                return;
            }
            Fit fit = s.Chosen.Fit;
            bool picked = s.Chosen.Recipe.Kind != SignRecipe.Kinds.None;
            // a picked item SignLink cannot resize or move goes on as made, at its own place (SignFit.Put): framed there,
            // not where the rows' size and place would put it (0.20.91, review)
            bool still = picked && !src.Movable(s);
            if (still) fit = fit.PoseOnly();
            // the content's size (wu): the chosen item's, else what the sign holds (its size as made times its scale)
            Vector2 m;
            // what the sign holds without a recipe SignLink can size from (a text written elsewhere, a picture from before
            // recipes): measured where its glyphs draw, as Show all frames it (0.20.155), else its pixels read back
            double inkX = 0, inkY = 0;
            bool inked = false;
            bool sized = picked ? src.Size(s, out m)
                                : (inked = HeldInk(s, target, out m, out inkX, out inkY)) || (m = s.NaturalM) != Vector2.zero;
            if (sized) unsizedSince = -1f;
            else if (unsizedSince < 0f) unsizedSince = Time.unscaledTime;
            else if (Time.unscaledTime - unsizedSince > 2f &&                // only the board shows: say why, once (the line made
                     !(ReferenceEquals(noted, unsizedLine) && unsizedTitle == src.Title &&   // only when it would
                       unsizedPicked == picked && unsizedKind == s.Chosen.Recipe.Kind))                       // change, 0.20.132)
            {
                unsizedTitle = src.Title;
                unsizedPicked = picked;
                unsizedKind = s.Chosen.Recipe.Kind;
                Note($"the content's size is not known ({src.Title}, {(picked ? "picked " + s.Chosen.Recipe.Kind : "what the sign holds")}): " +
                     "only the board is framed");
                unsizedLine = noted;                                        // what the log holds now (the same words: the earlier line)
            }
            double k = still || picked && !src.Resizable ? 1 : picked ? src.ShownScale(s) : fit.Scale;   // a picture's size is its own; a piece's snaps
            double wWu = m.x * 40 * k, hWu = m.y * 40 * k;
            // what the sign holds playing as a moving GIF shows on a screen a pixel larger on every side than its still
            // (PictureImporter.PlayOver), framed as it shows (0.20.116; a picked picture's Size says so itself)
            if (!picked && s.Held.Recipe.Kind == SignRecipe.Kinds.Picture && s.Held.Recipe.Pitch > 0 &&
                Plugin.Screens.ScreenOver(s.Stack.Count > 0 ? s.Stack : new List<Sign> { target }) != null)
            {
                wWu += 2 * s.Held.Recipe.Pitch * k;
                hWu += 2 * s.Held.Recipe.Pitch * k;
            }
            // where its middle shows in the chosen fit (text-local, wu), as the put or the refit leaves it (0.20.157, review:
            // the frame turned the content round its own middle under Rotate, the put round the sign's): the picked item's
            // own (Source.Middle); what the sign holds, measured, moved as Refit moves it (Fit.Between from the held fit,
            // with the centre the sign keeps, else 0); content Refit cannot move stays where it shows
            double c0x = 0, c0y = 0;
            double hx = s.CentreKnown ? s.C0x : 0, hy = s.CentreKnown ? s.C0y : 0;
            if (picked)
            {
                if (!src.Middle(s, fit, still, out c0x, out c0y)) fit.Centre(0, 0, out c0x, out c0y);
            }
            else if (inked && !s.Held.Movable)
            {
                c0x = inkX;
                c0y = inkY;
            }
            else if (inked)
            {
                Fit.Between(s.Held.Fit, fit, hx, hy, out double kk, out double dx, out double dy);
                c0x = kk * inkX + dx;
                c0y = kk * inkY + dy;
            }
            else
            {
                fit.Centre(hx, hy, out c0x, out c0y);
            }
            bool content = sized && contentOn;                              // Settings › Sign options › Highlight sign contents
            bool litPreview = Plugin.Preview != null && Plugin.Preview.LitPicture;
            var inputs = new Inputs
            {
                Scale = fit.Scale, Right = fit.Right, Up = fit.Up, Out = fit.Out, Tilt = fit.Tilt, Turn = fit.Turn, Edge = fit.Edge,
                Wwu = wWu, Hwu = hWu, C0x = c0x, C0y = c0y, Position = target.transform.position, Rotation = target.transform.rotation,
                Pending = SignMove.Pending(target), Sized = sized, Content = content, Lit = litPreview, Board = boardOn,
            };
            inputs.Target = Uid(target, out inputs.HasTarget);
            inputs.Host = Uid(host, out inputs.HasHost);
            if (shownKey != null && root && ReferenceEquals(sign, target) && inputs.Same(shownInputs))
            {
                if (!root.activeSelf) root.SetActive(true);                 // nothing changed: no key built (0.20.132)
                Hold(unheld);
                Side();
                return;
            }
            string key = $"{SignFinder.Id(target)}|{SignFinder.Id(host)}|{fit.Scale}|{fit.Right}|{fit.Up}|{fit.Out}|{fit.Tilt}|{fit.Turn}|{fit.Edge}|{wWu}|{hWu}|{c0x}|{c0y}|" +
                         $"{target.transform.position}|{target.transform.rotation}|{SignMove.Pending(target)}|{sized}|{content}|{litPreview}|{boardOn}";
            shownInputs = inputs;                                           // what the key below is made of
            if (key == shownKey && root && ReferenceEquals(sign, target))
            {
                if (!root.activeSelf) root.SetActive(true);
                Hold(unheld);
                Side();
                return;
            }
            // the sign's own pose: where it stands, as the world save keeps it (rotations to half a degree), not worked out
            // again: a pose worked out lay a fraction of a degree off the picture's plane and crossed it, red on one side
            // and flickering where they met (0.20.104, the author: the current picture still flickers); a pose still to come
            // (a tilt or a rotation not applied) is worked out (Preview)
            bool own = s.PendingPose(target, fit, out Vector3 pos, out Quaternion rot);
            if (!Ensure(target, host))
            {
                Note("hidden: the sign's text has no canvas");
                Hide();
                return;
            }
            CountRebuild(shownKey, key);
            shownKey = key;
            var rt = (RectTransform)root.transform;
            rt.SetPositionAndRotation(pos, rot);                            // the pending pose, in the world
            Vector3 ls = rt.parent.lossyScale;                              // the canvas's scale cancelled: children in metres
            rt.localScale = new Vector3(Safe(ls.x), Safe(ls.y), Safe(ls.z));

            // text-local -> sign-local (the widget's transform relative to the sign root; constant per prefab)
            Matrix4x4 toSign = target.transform.worldToLocalMatrix * target.m_textWidget.transform.localToWorldMatrix;
            double cx = c0x, cy = c0y;                                      // the middle worked out above (Middle, Between)
            Vector3 centre = toSign.MultiplyPoint3x4(new Vector3((float)(cx / 2), (float)(cy / 2), 0f));   // 1 local unit = 2 wu
            // in front of the stack's frontmost band, as the hover's frame is: its copies stand Step apart in front of the
            // first sign, and on the first sign's plane the bars lay on a copy's and flickered green and red with what
            // draws there, a moving GIF's lit screen (0.20.102, the author: could the content layer flicker with itself?)
            float textZ = centre.z;
            float frontZ = (target.transform.worldToLocalMatrix * host.m_textWidget.transform.localToWorldMatrix).MultiplyPoint3x4(Vector3.zero).z;
            if (frontZ > centre.z) centre.z = frontZ;
            Transform screen = Plugin.Screens.ScreenOver(s.Stack.Count > 0 ? s.Stack : new List<Sign> { target });
            float screenZ = screen ? target.transform.InverseTransformPoint(screen.position).z : float.NaN;
            if (screen && screenZ > centre.z) centre.z = screenZ;
            float off = screen || litPreview ? OverScreen : OnText;
            Vector3 xAxis = toSign.MultiplyVector(Vector3.right).normalized, yAxis = toSign.MultiplyVector(Vector3.up).normalized;
            float w = (float)(wWu * 0.025), h = (float)(hWu * 0.025);
            Vector3 ahead = Vector3.forward * off;                         // on the text's plane: green and red as it shows
            plane = centre + ahead;
            PlaceFill(centre + ahead, yAxis, w, h, content, s.Stack.Count <= 1);
            Place(edge[0], centre + yAxis * (h / 2) + ahead, yAxis, w + Thick, Thick, content);     // top
            Place(edge[1], centre - yAxis * (h / 2) + ahead, yAxis, w + Thick, Thick, content);     // bottom
            Place(edge[2], centre - xAxis * (w / 2) + ahead, yAxis, Thick, h, content);             // left
            Place(edge[3], centre + xAxis * (w / 2) + ahead, yAxis, Thick, h, content);             // right
            Place(mark, centre + yAxis * (h / 2 + 0.03f) + ahead, yAxis, 0.12f, 0.02f, content);    // "top", as the viewer reads it
            PlaceBoard(target, boardOn);                                    // Settings › Sign options › Highlight signs
            root.SetActive(true);
            Hold(unheld);
            Side();
            if (!reported)                                                  // once after each hide: where it went
            {
                reported = true;
                Note($"shown on {SignFinder.Id(target)}: content {(sized ? $"{w:0.##} x {h:0.##} m" : "not sized")}, " +
                     $"canvas {root.transform.parent?.name}, {s.Stack.Count} sign(s), {(own ? "as it stands" : "at the pose to come")}; " +
                     $"planes (m in front of the sign's centre): text {textZ:0.0000}, frontmost {frontZ:0.0000}" +
                     (screen ? $", screen {screenZ:0.0000}" : "") + $", frame {centre.z + off:0.0000}");
            }
        }

        // the frame's own log (Note): a line when what it does changes, to tell why a frame is missing in game
        string noted;
        bool reported;
        float unsizedSince = -1f;
        // the "size not known" line as last said, and what it said it for (made again only when that changes)
        string unsizedLine, unsizedTitle;
        bool unsizedPicked;
        SignRecipe.Kinds unsizedKind;

        // the frame made again many times a second, back and forth, says what keeps changing (0.20.105: a flicker that the
        // planes do not explain)
        float rebuildsAt;
        int rebuilds, alternations;
        string keyBefore;
        static readonly string[] KeyParts = { "sign", "host", "scale", "right", "up", "out", "tilt", "turn", "edge", "width", "height",
                                              "c0x", "c0y", "position", "rotation", "pending", "sized", "content", "lit", "board" };

        void CountRebuild(string was, string now)
        {
            float t = Time.unscaledTime;
            if (t - rebuildsAt > 1f)
            {
                rebuildsAt = t;
                rebuilds = alternations = 0;
            }
            rebuilds++;
            if (now == keyBefore) alternations++;
            keyBefore = was;
            if (alternations == 10) Note($"made again {rebuilds} times within a second, back and forth: {KeyDiff(was, now)}");
        }

        static string KeyDiff(string a, string b)
        {
            if (a == null || b == null) return "(shown anew)";
            string[] x = a.Split('|'), y = b.Split('|');
            var parts = new List<string>();
            for (int i = 0; i < Mathf.Max(x.Length, y.Length); i++)
            {
                string u = i < x.Length ? x[i] : "", v = i < y.Length ? y[i] : "";
                if (u != v) parts.Add($"{(i < KeyParts.Length ? KeyParts[i] : i.ToString())} {u} / {v}");
            }
            return parts.Count > 0 ? string.Join("; ", parts.ToArray()) : "nothing";
        }

        static readonly Vector3[] screenCorners = new Vector3[4];
        Vector3 plane;                                                      // the content's bars' plane, sign-local
        bool back;                                                          // the bars wear their neutral look

        /// <summary>Seen from behind the sign (the camera on the far side of the content's plane), the content's bars do
        /// not judge what shows: one neutral colour, no depth test (0.20.106, the author: I may have been looking at the
        /// picture's back ... that may have confused it and made it flicker; from behind the frame lay behind the
        /// picture). Each Layout.</summary>
        void Side()
        {
            Camera cam = Camera.main;
            if (!root || !cam) return;
            Transform t = root.transform;
            bool now = Vector3.Dot(cam.transform.position - t.TransformPoint(plane), t.forward) < 0f;
            if (now == back) return;
            back = now;
            Look(fill);
            foreach (Bar b in edge) Look(b);
            Look(mark);
        }

        /// <summary>A content bar in the look the side calls for.</summary>
        void Look(Bar b)
        {
            if (b == null || !b.Go || b.Seen == null || b.Neutral == null) return;
            b.Go.GetComponent<MeshFilter>().sharedMesh = back ? b.Neutral : b.Seen;
            b.Go.GetComponent<MeshRenderer>().sharedMaterial = back ? frameMaterial : seenMaterial;
            if (b.Behind) b.Behind.gameObject.SetActive(!back);
        }

        void Note(string what)
        {
            if (what == noted) return;
            noted = what;
            Plugin.Log.LogInfo("the highlight: " + what);
        }

        // the sign and pose an outline alone was built for (ShowBoard)
        Sign boardFor;
        Vector3 boardAt;
        Quaternion boardTurn;
    }
}
