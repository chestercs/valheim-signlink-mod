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
    internal sealed class Ghost
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
        /// <summary>The hover frame's colour (Pick in view, a row of Signs nearby under the cursor): the content as the
        /// selection's, and the sign's board a light cool white instead of the soft grey, so the two frames read alike and
        /// the board's colour alone says "under the cursor" against "selected".</summary>
        public static readonly Color HoverBoard = new Color(0.95f, 0.97f, 1f, 0.85f);
        /// <summary>The content's frame where something stands in front of it (a floor it went under, a wall): a deep red
        /// instead of the green (0.20.86, the author: the frame showed through everything, so a picture slipping under the
        /// floor did not show; 0.20.88: the rose of before was too bright and close to the amber; 0.20.89 a darker violet;
        /// 0.20.94, the author: the content layer reddish instead of violet; 0.20.100: a deep crimson, shown as
        /// chosen).</summary>
        public static readonly Color Behind = new Color(0.80f, 0.12f, 0.14f, 0.85f);

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
            public bool Stack, HasTarget, HasHost, HasRevision, Edge, Sized, Content, Lit, Screen;
            public ZDOID Target, Host;
            public double Scale, Right, Up, Out, Tilt, Turn, Wwu, Hwu, C0x, C0y;
            public Vector3 Position, Pending;
            public Quaternion Rotation;
            public int Count;
            public uint Revision;

            /// <summary>Exactly the same (Unity's == on vectors and rotations is approximate: Equals is not).</summary>
            public bool Same(Inputs o) =>
                Stack == o.Stack && HasTarget == o.HasTarget && HasHost == o.HasHost && HasRevision == o.HasRevision && Edge == o.Edge &&
                Sized == o.Sized && Content == o.Content && Lit == o.Lit && Screen == o.Screen && Target == o.Target && Host == o.Host &&
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

        /// <summary>The bars drawn as ordinary quads, not UI elements, with the game's own Hidden/Internal-Colored
        /// shader (in every build: GL drawing uses it) set to test no depth and draw at render queue 3100, after every
        /// canvas (they sit at 3000): the frame shows whole on any stack (the copies' canvases draw in an order of their
        /// own, which hid a UI fill under one of them, 0.20.12) and through walls and floors, so a sign under a floor or
        /// behind a wall still shows where it is. Sprites/Default is the fallback (whole on a stack, but hidden by
        /// walls); with neither the RawImage bars stay (the fill over a single sign only). Colours are per-vertex: one
        /// mesh per colour.</summary>
        static Material frameMaterial, seenMaterial, behindMaterial, boardMaterial;
        static bool frameLooked;

        /// <summary>The same shader as FrameMaterial testing depth: drawn where nothing stands in front (LessEqual), or only
        /// where something does (Greater): the content's frame in two colours (0.20.86).</summary>
        static Material DepthMaterial(Shader shader, string name, UnityEngine.Rendering.CompareFunction test)
        {
            var m = new Material(shader) { name = name, renderQueue = 3100 };
            m.SetInt("_ZTest", (int)test);
            m.SetInt("_ZWrite", 0);
            m.SetInt("_Cull", (int)UnityEngine.Rendering.CullMode.Off);
            m.SetFloat("_ZBias", -1f);                                      // a hair towards the camera: on a surface it shows (0.20.105)
            m.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            m.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            return m;
        }
        static readonly Dictionary<Color, Mesh> quads = new Dictionary<Color, Mesh>();

        /// <summary>The materials and meshes every frame shares go (the plugin unloads: a reload in a running game would
        /// leave them behind, 0.20.135, the author: now let us do the OnDestroy solution mentioned).</summary>
        public static void ReleaseShared()
        {
            foreach (Material m in new[] { frameMaterial, seenMaterial, behindMaterial, boardMaterial })
                if (m) Object.Destroy(m);
            frameMaterial = seenMaterial = behindMaterial = boardMaterial = null;
            frameLooked = false;
            foreach (Mesh q in quads.Values)
                if (q) Object.Destroy(q);
            quads.Clear();
        }

        static Material FrameMaterial()
        {
            if (frameLooked) return frameMaterial;
            frameLooked = true;
            Shader shader = Shader.Find("Hidden/Internal-Colored");
            Camera cam = Camera.main;
            Plugin.Log.LogInfo($"the highlight: colour space {QualitySettings.activeColorSpace} (its colours converted to show as chosen), " +
                               $"camera HDR {(cam ? cam.allowHDR.ToString() : "unknown")} (the board glows with HDR)");
            if (shader != null)
            {
                frameMaterial = new Material(shader) { name = "SignLinkFrame", renderQueue = 3100 };
                frameMaterial.SetInt("_ZTest", (int)UnityEngine.Rendering.CompareFunction.Always);
                frameMaterial.SetInt("_ZWrite", 0);
                frameMaterial.SetInt("_Cull", (int)UnityEngine.Rendering.CullMode.Off);
                frameMaterial.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                frameMaterial.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                // the board's outline after the content's veil, which lies in front of the board (the text's plane) and
                // drowned the outline under its green (0.20.127, the author: I can barely see the sign's border now)
                boardMaterial = new Material(frameMaterial) { name = "SignLinkFrameBoard", renderQueue = 3150 };
                seenMaterial = DepthMaterial(shader, "SignLinkFrameSeen", UnityEngine.Rendering.CompareFunction.LessEqual);
                behindMaterial = DepthMaterial(shader, "SignLinkFrameBehind", UnityEngine.Rendering.CompareFunction.Greater);
                return frameMaterial;
            }
            shader = Shader.Find("Sprites/Default");
            if (shader != null)
            {
                Plugin.Log.LogWarning("the highlight: no Hidden/Internal-Colored shader in this build; Sprites/Default draws it, hidden by walls");
                frameMaterial = new Material(shader) { name = "SignLinkFrame", renderQueue = 3100 };
                return frameMaterial;
            }
            Plugin.Log.LogWarning("the highlight: no shader for its quads in this build, so it is drawn as UI bars (the fill over a single sign only)");
            return null;
        }

        /// <summary>A unit quad in this colour (both windings: seen from either side), one per colour; glow over 1 makes
        /// it that many times brighter than the colour on screen (BoardGlow).</summary>
        static Mesh QuadMesh(Color color, float glow = 1f)
        {
            if (glow != 1f) color = new Color(color.r * glow, color.g * glow, color.b * glow, color.a);   // its own key: over 1
            if (quads.TryGetValue(color, out Mesh m) && m) return m;
            m = new Mesh { name = "SignLinkFrameQuad" };
            m.vertices = new[] { new Vector3(-0.5f, -0.5f, 0f), new Vector3(0.5f, -0.5f, 0f), new Vector3(0.5f, 0.5f, 0f), new Vector3(-0.5f, 0.5f, 0f) };
            m.uv = new[] { Vector2.zero, Vector2.right, Vector2.one, Vector2.up };
            Color shown = OnScreen(new Color(color.r / glow, color.g / glow, color.b / glow, color.a));
            shown = new Color(shown.r * glow, shown.g * glow, shown.b * glow, shown.a);   // brighter in the linear space
            m.colors = new[] { shown, shown, shown, shown };
            m.triangles = new[] { 0, 1, 2, 0, 2, 3, 0, 2, 1, 0, 3, 2 };
            m.RecalculateNormals();
            m.RecalculateBounds();
            quads[color] = m;
            return m;
        }

        /// <summary>The selection's frame.</summary>
        public Ghost() : this(Fill, Line, Board) { }

        /// <summary>A frame in these colours (Pick in view's hover frame).</summary>
        public Ghost(Color fill, Color line, Color board)
        {
            fillColor = fill;
            lineColor = line;
            boardColor = board;
            boardGlow = board == Board ? BoardGlow : 1f;                     // the selection's board glows, the hover's not
        }

        /// <summary>Layout events only: shows the frame for the session's choice on the selected sign, or hides it.</summary>
        public void Update(Session s, Source src)
        {
            if (!PluginConfig.Highlight.Value || !s.HasTarget || src == null)
            {
                if (!PluginConfig.Highlight.Value && s.HasTarget) Note("hidden: disabled (Settings › Sign options › Highlight signs)");
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
            bool content = sized && PluginConfig.HighlightContent.Value;    // Settings › Sign options › Highlight sign contents
            bool litPreview = Plugin.Preview != null && Plugin.Preview.LitPicture;
            var inputs = new Inputs
            {
                Scale = fit.Scale, Right = fit.Right, Up = fit.Up, Out = fit.Out, Tilt = fit.Tilt, Turn = fit.Turn, Edge = fit.Edge,
                Wwu = wWu, Hwu = hWu, C0x = c0x, C0y = c0y, Position = target.transform.position, Rotation = target.transform.rotation,
                Pending = SignMove.Pending(target), Sized = sized, Content = content, Lit = litPreview,
            };
            inputs.Target = Uid(target, out inputs.HasTarget);
            inputs.Host = Uid(host, out inputs.HasHost);
            if (shownKey != null && root && ReferenceEquals(sign, target) && inputs.Same(shownInputs))
            {
                if (!root.activeSelf) root.SetActive(true);                 // nothing changed: no key built (0.20.132)
                Side();
                return;
            }
            string key = $"{SignFinder.Id(target)}|{SignFinder.Id(host)}|{fit.Scale}|{fit.Right}|{fit.Up}|{fit.Out}|{fit.Tilt}|{fit.Turn}|{fit.Edge}|{wWu}|{hWu}|{c0x}|{c0y}|" +
                         $"{target.transform.position}|{target.transform.rotation}|{SignMove.Pending(target)}|{sized}|{content}|{litPreview}";
            shownInputs = inputs;                                           // what the key below is made of
            if (key == shownKey && root && ReferenceEquals(sign, target))
            {
                if (!root.activeSelf) root.SetActive(true);
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
            PlaceBoard(target);
            root.SetActive(true);
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
                                              "c0x", "c0y", "position", "rotation", "pending", "sized", "content" };

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

        /// <summary>The board's outline alone, at the sign's own pose: the faint grey of every sign a click could select
        /// (AllBoards, 0.20.172). Built again only when the sign moved.</summary>
        public void ShowBoard(Sign target)
        {
            if (!target || !target.m_textWidget)
            {
                Hide();
                return;
            }
            Transform t = target.transform;
            if (root && ReferenceEquals(boardFor, target) && boardAt == t.position && boardTurn == t.rotation)
            {
                if (!root.activeSelf) root.SetActive(true);
                return;
            }
            if (!Ensure(target, target))
            {
                Hide();
                return;
            }
            boardFor = target;
            boardAt = t.position;
            boardTurn = t.rotation;
            shownKey = null;                                                // not a frame ShowStack or Show built
            var rt = (RectTransform)root.transform;
            rt.SetPositionAndRotation(t.position, t.rotation);
            Vector3 ls = rt.parent.lossyScale;
            rt.localScale = new Vector3(Safe(ls.x), Safe(ls.y), Safe(ls.z));
            PlaceFill(Vector3.zero, Vector3.up, 0f, 0f, false, true);
            for (int i = 0; i < 4; i++) Place(edge[i], Vector3.zero, Vector3.up, 0f, 0f, false);
            Place(mark, Vector3.zero, Vector3.up, 0f, 0f, false);
            PlaceBoard(target);
            root.SetActive(true);
        }

        /// <summary>The frame around a stack as it is: the union of its signs' texts (TMP's bounds, what is drawn) in
        /// the first sign's plane, in front of the frontmost band, with the top mark, and the board's outline, at the
        /// sign's own pose. Built like the selection's frame (0.20.21 dropped the board from this one and the author lost
        /// track of which rectangle was which): the content green and red in both, the board neon lime when selected and
        /// cool white under the cursor (Pick in view, a row of Signs nearby), so a click's target shows before the click.
        /// An empty sign gets its board alone.</summary>
        public void ShowStack(Sign target, Sign host, List<Sign> stack)
        {
            if (!target || !target.m_textWidget || !host || !host.m_textWidget)
            {
                Hide();
                return;
            }
            // the first sign's data revision too (0.20.90, review): a stack put on again or refit by another player keeps its
            // place, and Show all kept the frame of what it held before
            Transform screen = Plugin.Screens.ScreenOver(stack);            // a moving GIF's screen (0.20.116)
            ZDO tz = SignFinder.Zdo(target);
            var inputs = new Inputs
            {
                Stack = true, Count = stack.Count, Position = target.transform.position, Rotation = target.transform.rotation,
                HasRevision = tz != null, Revision = tz != null ? tz.DataRevision : 0u, Content = PluginConfig.HighlightContent.Value, Screen = screen != null,
            };
            inputs.Target = Uid(target, out inputs.HasTarget);
            inputs.Host = Uid(host, out inputs.HasHost);
            if (shownKey != null && root && ReferenceEquals(sign, target) && inputs.Same(shownInputs))
            {
                if (!root.activeSelf) root.SetActive(true);                 // nothing changed: no key built (0.20.132)
                Side();
                return;
            }
            string key = $"stack|{SignFinder.Id(target)}|{SignFinder.Id(host)}|{stack.Count}|{target.transform.position}|{target.transform.rotation}|" +
                         $"{SignFinder.Zdo(target)?.DataRevision}|{PluginConfig.HighlightContent.Value}|{screen != null}";
            shownInputs = inputs;                                           // what the key below is made of
            if (key == shownKey && root && ReferenceEquals(sign, target))
            {
                if (!root.activeSelf) root.SetActive(true);
                Side();
                return;
            }
            if (!Ensure(target, host))
            {
                Hide();
                return;
            }
            shownKey = key;
            var rt = (RectTransform)root.transform;
            rt.SetPositionAndRotation(target.transform.position, target.transform.rotation);
            Vector3 ls = rt.parent.lossyScale;
            rt.localScale = new Vector3(Safe(ls.x), Safe(ls.y), Safe(ls.z));

            float minX = float.MaxValue, minY = float.MaxValue, maxX = float.MinValue, maxY = float.MinValue, front = float.MinValue;
            bool texts = false, stale = false;
            foreach (Sign s in stack)
            {
                TMP_Text t = s ? s.m_textWidget : null;
                if (!t || string.IsNullOrEmpty(t.text)) continue;
                texts = true;
                if (TextBehind(s)) stale = true;
                if (!Ink.Rect(t, out Vector2 lo, out Vector2 hi))             // where its glyphs draw (0.20.155: not TMP's textBounds)
                {
                    stale = true;
                    continue;
                }
                Matrix4x4 m = target.transform.worldToLocalMatrix * t.transform.localToWorldMatrix;   // text-local -> sign-local (metres)
                for (int i = 0; i < 4; i++)
                {
                    Vector3 c = m.MultiplyPoint3x4(new Vector3((i & 1) == 0 ? lo.x : hi.x, (i & 2) == 0 ? lo.y : hi.y, 0f));
                    minX = Mathf.Min(minX, c.x);
                    maxX = Mathf.Max(maxX, c.x);
                    minY = Mathf.Min(minY, c.y);
                    maxY = Mathf.Max(maxY, c.y);
                    front = Mathf.Max(front, c.z);
                }
            }
            // a moving GIF's screen counts as it shows (0.20.116, the author: I cleared the sign, then put the GIF in again
            // ... Show all ... shows only the sign's border): it hides the text, which a GIF put on anew has never laid out,
            // so there was nothing to measure; and it is a pixel larger on every side than the still
            if (screen is RectTransform sr)
            {
                sr.GetWorldCorners(screenCorners);
                Matrix4x4 w2s = target.transform.worldToLocalMatrix;
                foreach (Vector3 corner in screenCorners)
                {
                    Vector3 c = w2s.MultiplyPoint3x4(corner);
                    minX = Mathf.Min(minX, c.x);
                    maxX = Mathf.Max(maxX, c.x);
                    minY = Mathf.Min(minY, c.y);
                    maxY = Mathf.Max(maxY, c.y);
                    front = Mathf.Max(front, c.z);
                }
                texts = true;
            }
            // a built-in piece or a picture SignLink made: framed as the selection frames it, from what made it (a piece's size
            // and middle as made, NAME.fit; a picture's grid of whole pixels), with the fit the sign keeps, so picking a sign
            // from Show all or the hover does not move its frame (0.20.154, 0.20.155: the author saw a picture's frame jump)
            if (screen == null && ModelRect(target, maxX - minX, maxY - minY, out Vector3 pc, out float pw, out float ph))
            {
                minX = pc.x - pw / 2;
                maxX = pc.x + pw / 2;
                minY = pc.y - ph / 2;
                maxY = pc.y + ph / 2;
                if (front == float.MinValue) front = pc.z;                 // no text measured: the first sign's text plane
                texts = true;
            }
            bool has = maxX > minX && maxY > minY && PluginConfig.HighlightContent.Value;   // the content's frame: a setting of its own
            // text the game has not laid out yet (a sign that just loaded): measured again at the next look, not kept as
            // a frame without its content (0.20.90, review)
            if (!has && texts && PluginConfig.HighlightContent.Value) shownKey = null;
            if (stale) shownKey = null;                                     // a sign's text not laid out as its data says yet: measured again
            Vector3 centre = has ? new Vector3((minX + maxX) / 2, (minY + maxY) / 2, front + OnText) : Vector3.zero;
            float w = has ? maxX - minX : 0f, h = has ? maxY - minY : 0f;
            plane = centre;
            PlaceFill(centre, Vector3.up, w, h, has, stack.Count <= 1);
            Place(edge[0], centre + Vector3.up * (h / 2), Vector3.up, w + Thick, Thick, has);
            Place(edge[1], centre - Vector3.up * (h / 2), Vector3.up, w + Thick, Thick, has);
            Place(edge[2], centre - Vector3.right * (w / 2), Vector3.up, Thick, h, has);
            Place(edge[3], centre + Vector3.right * (w / 2), Vector3.up, Thick, h, has);
            Place(mark, centre + Vector3.up * (h / 2 + 0.03f), Vector3.up, 0.12f, 0.02f, has);   // the same frame as the selection's
            PlaceBoard(target);                                              // the board's outline too, as the selection's: one language
            root.SetActive(true);
            Side();
        }

        // the held stack's ink, measured once a stack and data revision (HeldInk)
        Sign inkSign;
        uint inkRevision;
        int inkCount = -1;
        Fit inkFit;
        bool inkHas;
        Vector2 inkM;
        double inkX, inkY;

        /// <summary>What the selected stack holds, measured where its glyphs draw (Ink.Rect over its signs, in the first sign's
        /// text-local frame): its size as made (metres, the held fit's scale taken out) and its middle where it shows now
        /// (wu, text-local), which Show moves as Refit moves the texts (Fit.Between). Once a data revision; false while
        /// nothing is laid out yet.</summary>
        bool HeldInk(Session s, Sign target, out Vector2 m, out double px, out double py)
        {
            m = Vector2.zero;
            px = py = 0;
            List<Sign> stack = s.Stack.Count > 0 ? s.Stack : new List<Sign> { target };
            ZDO z = SignFinder.Zdo(target);
            uint revision = z != null ? z.DataRevision : 0u;
            if (!ReferenceEquals(inkSign, target) || inkRevision != revision || inkCount != stack.Count || !inkFit.Same(s.Held.Fit))
            {                                                               // no key string a Layout: compared field by field
                inkHas = false;
                float x0 = float.MaxValue, y0 = float.MaxValue, x1 = float.MinValue, y1 = float.MinValue;
                bool complete = true;
                Matrix4x4 toFirst = target.m_textWidget.transform.worldToLocalMatrix;
                foreach (Sign sign in stack)
                {
                    TMP_Text t = sign ? sign.m_textWidget : null;
                    if (!t) continue;
                    if (TextBehind(sign)) complete = false;
                    if (!Ink.Rect(t, out Vector2 lo, out Vector2 hi))
                    {
                        if (!string.IsNullOrEmpty(t.text)) complete = false;
                        continue;
                    }
                    Matrix4x4 mm = toFirst * t.transform.localToWorldMatrix;
                    for (int i = 0; i < 4; i++)
                    {
                        Vector3 c = mm.MultiplyPoint3x4(new Vector3((i & 1) == 0 ? lo.x : hi.x, (i & 2) == 0 ? lo.y : hi.y, 0f));
                        x0 = Mathf.Min(x0, c.x);
                        x1 = Mathf.Max(x1, c.x);
                        y0 = Mathf.Min(y0, c.y);
                        y1 = Mathf.Max(y1, c.y);
                    }
                }
                if (!(x1 > x0 && y1 > y0)) return false;                      // not laid out yet: measured at a later look
                inkSign = complete ? target : null;                             // all laid out as their data says: kept, else measured again
                inkRevision = revision;
                inkCount = stack.Count;
                inkFit = s.Held.Fit;
                Fit held = s.Held.Fit;
                double k = held.Scale > 0 ? held.Scale : 1;
                inkM = new Vector2((float)((x1 - x0) * 0.05 / k), (float)((y1 - y0) * 0.05 / k));   // 1 local unit = 2 wu = 0.05 m
                inkX = x0 + x1;                                                 // the middle in wu: (x0 + x1) / 2 * 2
                inkY = y0 + y1;
                inkHas = true;
            }
            if (!inkHas) return false;
            m = inkM;
            px = inkX;
            py = inkY;
            return true;
        }

        /// <summary>The sign's text widget still shows an older data revision than its ZDO (another player's write reaches
        /// the widget up to 2 s later, at the game's UpdateText): what it measures is not what the data says (0.20.157,
        /// review: such a measure was kept until the next revision).</summary>
        static bool TextBehind(Sign s)
        {
            ZDO z = s ? SignFinder.Zdo(s) : null;
            return z != null && s.m_lastRevision != z.DataRevision;
        }

        /// <summary>A stack whose fit the sign keeps (SignFit.Read) and that holds a built-in piece with a known size (NAME.fit)
        /// or a picture with its pixel (the recipe's Pitch): its content's middle in the sign's own frame (metres, sign-local)
        /// and its width and height, as Show frames the selection (Fit.Centre; ArtSource.Size; a picture's plan, W x H whole
        /// pixels: the ink measured, inkW x inkH metres, a fraction of a pixel over it where seamless pixels overlap, gives
        /// the count); false for anything else (the ink then).</summary>
        static bool ModelRect(Sign first, float inkW, float inkH, out Vector3 centre, out float w, out float h)
        {
            centre = Vector3.zero;
            w = h = 0f;
            SignRecipe r = SignRecipe.Parse(SignMarks.Source(first));
            double wWu, hWu;
            Fit fit = SignFit.Read(first, out double c0x, out double c0y, out bool known);
            if (!known) return false;
            if (r.Kind == SignRecipe.Kinds.Art)
            {
                List<Preset> items = ArtLibrary.Items;
                int i = Preset.Find(items, r.Path);
                if (i < 0 || !items[i].Size(out wWu, out hWu)) return false;
            }
            else if (r.Kind == SignRecipe.Kinds.Picture && r.Pitch > 0 && inkW > 0f && inkH > 0f)
            {
                double pitch = r.Pitch / fit.Scale;                          // made at its own size: wWu * Scale below is the plan's
                double cell = r.Pitch * 0.025;                               // one pixel, metres
                wWu = System.Math.Max(1, System.Math.Round(inkW / cell)) * pitch;
                hWu = System.Math.Max(1, System.Math.Round(inkH / cell)) * pitch;
            }
            else return false;
            fit.Centre(c0x, c0y, out double cx, out double cy);
            Matrix4x4 toSign = first.transform.worldToLocalMatrix * first.m_textWidget.transform.localToWorldMatrix;
            centre = toSign.MultiplyPoint3x4(new Vector3((float)(cx / 2), (float)(cy / 2), 0f));   // 1 local unit = 2 wu
            w = (float)(wWu * fit.Scale * 0.025);
            h = (float)(hWu * fit.Scale * 0.025);
            return w > 0f && h > 0f;
        }

        /// <summary>The board: its collider's rectangle in the sign's plane, at the board's own depth.</summary>
        void PlaceBoard(Sign target)
        {
            BoxCollider c = target.GetComponentInChildren<BoxCollider>();
            if (!c)
            {
                foreach (Bar b in board) b.Go.SetActive(false);
                return;
            }
            Vector3 bc = target.transform.InverseTransformPoint(c.transform.TransformPoint(c.center));
            Vector3 bs = Vector3.Scale(c.size, c.transform.lossyScale);
            Vector3 bAhead = Vector3.forward * (bs.z / 2 + Ahead);
            float bw = Mathf.Abs(bs.x), bh = Mathf.Abs(bs.y);
            Place(board[0], bc + Vector3.up * (bh / 2) + bAhead, Vector3.up, bw + BoardThick, BoardThick, true);
            Place(board[1], bc - Vector3.up * (bh / 2) + bAhead, Vector3.up, bw + BoardThick, BoardThick, true);
            Place(board[2], bc - Vector3.right * (bw / 2) + bAhead, Vector3.up, BoardThick, bh, true);
            Place(board[3], bc + Vector3.right * (bw / 2) + bAhead, Vector3.up, BoardThick, bh, true);
        }

        static float Safe(float scale) => Mathf.Abs(scale) < 1e-6f ? 1f : 1f / scale;

        /// <summary>The bars exist under the canvas of the stack's frontmost sign (host), last among its children: that
        /// canvas draws last of the stack's, so the frame shows over every band. A new sign, a stack made again or one
        /// that grew gets new bars. The frame's pose is set in the world, so the host only draws it.</summary>
        bool Ensure(Sign target, Sign host)
        {
            Canvas canvas = host.m_textWidget.GetComponentInParent<Canvas>();
            if (!canvas) return false;
            if (root && ReferenceEquals(sign, target) && root.transform.parent == canvas.transform) return true;
            Destroy();
            sign = target;
            root = new GameObject("SignLinkGhost", typeof(RectTransform));
            root.layer = host.m_textWidget.gameObject.layer;
            root.transform.SetParent(canvas.transform, false);
            root.transform.SetAsLastSibling();                               // drawn over that sign's text
            fill = Make("fill", new Color(fillColor.r, fillColor.g, fillColor.b, fillColor.a * 0.35f), content: true);   // the bar's veil, faint
            for (int i = 0; i < 4; i++) edge[i] = Make("edge", lineColor, content: true);
            mark = Make("mark", lineColor, content: true);
            for (int i = 0; i < 4; i++) board[i] = Make("board", boardColor, glow: boardGlow);
            back = false;                                                   // new bars wear the front look
            return true;
        }

        /// <summary>The fill: on any stack as a quad, an even veil, green where it shows and red where something stands in
        /// front of it, and grey stripes from behind (Hatch, its mesh made for this size: the stripes keep their width in
        /// metres); as a RawImage bar, a faint veil over a single sign only. Nothing for a clear colour.</summary>
        void PlaceFill(Vector3 centre, Vector3 yAxis, float w, float h, bool on, bool single)
        {
            bool want = on && fillColor.a > 0.001f && w > 0f && h > 0f;
            if (want && fill.Quad)
            {
                fill.Seen = QuadMesh(new Color(fillColor.r, fillColor.g, fillColor.b, SeenAlpha));   // an even veil (0.20.112)
                EnsureStripes(w, h);                                        // the stripes for this size: the grey look
                fill.Neutral = stripesNeutral;
                if (fill.Behind) fill.Behind.sharedMesh = QuadMesh(new Color(Behind.r, Behind.g, Behind.b, SeenAlpha));   // red too (0.20.113)
                Look(fill);
            }
            Place(fill, centre, yAxis, w, h, want && (fill.Quad || single));
        }

        Mesh stripesNeutral;
        float stripesW, stripesH;

        /// <summary>The grey stripes for a w x h content in the quad's unit square, both windings, in the neutral colour
        /// (NeutralOf, seen from behind); made again only when the size changes (the old mesh filled again).</summary>
        void EnsureStripes(float w, float h)
        {
            if (stripesNeutral && Mathf.Abs(stripesW - w) < 1e-4f && Mathf.Abs(stripesH - h) < 1e-4f) return;
            float pitch = Mathf.Clamp(Mathf.Max(w, h) / 24f, 0.05f, 0.4f);   // stripes a few centimetres apart, wider on big pictures
            List<double[]> bands = Hatch.Stripes(w, h, pitch, pitch * 0.35f);
            List<Vector3> verts = stripeVerts;
            List<int> tris = stripeTris;
            verts.Clear();
            tris.Clear();
            foreach (double[] p in bands)
            {
                int first = verts.Count, n = p.Length / 2;
                for (int i = 0; i < n; i++) verts.Add(new Vector3((float)p[2 * i], (float)p[2 * i + 1], 0f));
                for (int i = 1; i + 1 < n; i++)
                {
                    tris.Add(first); tris.Add(first + i); tris.Add(first + i + 1);   // seen from the front
                    tris.Add(first); tris.Add(first + i + 1); tris.Add(first + i);   // and from behind
                }
            }
            stripesNeutral = Painted(stripesNeutral, verts, tris, NeutralOf(fillColor));
            stripesW = w;
            stripesH = h;
        }

        static readonly List<Vector3> stripeVerts = new List<Vector3>();
        static readonly List<int> stripeTris = new List<int>();
        static readonly List<Color> stripeColors = new List<Color>();

        /// <summary>The stripes into a mesh of this frame's, filled again when it has one (0.20.91, review: two meshes made
        /// and destroyed every frame while a Size slider was dragged).</summary>
        static Mesh Painted(Mesh m, List<Vector3> verts, List<int> tris, Color color)
        {
            if (m) m.Clear();
            else m = new Mesh { name = "SignLinkFrameStripes" };
            stripeColors.Clear();
            Color shown = OnScreen(color);
            for (int i = 0; i < verts.Count; i++) stripeColors.Add(shown);
            m.SetVertices(verts);
            m.SetColors(stripeColors);
            m.SetTriangles(tris, 0);
            m.RecalculateBounds();
            return m;
        }

        /// <summary>A bar under the root (metres, like the root's children): a quad with FrameMaterial in this colour,
        /// or a RawImage when the build has no shader for it.</summary>
        Bar Make(string name, Color color, bool content = false, float glow = 0f)
        {
            var b = new Bar();
            Material m = FrameMaterial();
            if (m != null && glow > 0f)                                     // the board's: over the content's veil, glowing
            {
                b.Go = Renderer(name, QuadMesh(color, glow), boardMaterial ?? m, root.transform);
                b.T = b.Go.transform;
                return b;
            }
            if (m != null)
            {
                // the content's bars: green where nothing stands in front of them, and the same quad again in Behind's
                // colour where something does (0.20.86); the board's bars show through everything in one colour
                bool twice = content && seenMaterial != null && behindMaterial != null;
                b.Go = Renderer(name, QuadMesh(color), twice ? seenMaterial : m, root.transform);
                b.T = b.Go.transform;
                if (twice)
                {
                    b.Behind = Renderer(name + " behind", QuadMesh(BehindOf(color)), behindMaterial, b.T).GetComponent<MeshFilter>();
                    b.Seen = QuadMesh(color);
                    b.Neutral = QuadMesh(NeutralOf(color));
                }
                return b;
            }
            b.Go = new GameObject(name, typeof(RectTransform));
            b.Go.layer = root.layer;
            var rt = (RectTransform)b.Go.transform;
            b.T = rt;
            rt.SetParent(root.transform, false);
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            b.Img = b.Go.AddComponent<RawImage>();
            b.Img.color = color;                                            // no texture: UI/Default draws it white, tinted
            b.Img.raycastTarget = false;
            return b;
        }

        /// <summary>A quad's object under parent (its transform the parent's: a bar's second colour follows it).</summary>
        GameObject Renderer(string name, Mesh mesh, Material material, Transform parent)
        {
            var go = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
            go.layer = root.layer;
            go.transform.SetParent(parent, false);
            go.GetComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.GetComponent<MeshRenderer>();
            r.sharedMaterial = material;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            r.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
            r.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
            return go;
        }

        /// <summary>A bar centred at a sign-local point, its up along yAxis, w x h metres (the root's +z is the sign's
        /// forward, so the bar lies in the sign's plane).</summary>
        static void Place(Bar b, Vector3 centre, Vector3 yAxis, float w, float h, bool on)
        {
            b.Go.SetActive(on);
            if (!on) return;
            b.T.localPosition = centre;
            b.T.localRotation = Quaternion.LookRotation(Vector3.forward, yAxis);
            if (b.Quad) b.T.localScale = new Vector3(w, h, 1f);
            else ((RectTransform)b.T).sizeDelta = new Vector2(w, h);
        }

        public void Hide()
        {
            if (root) root.SetActive(false);
            shownKey = null;
            reported = false;
        }

        /// <summary>Whether this frame shows now on the stack whose first sign this is.</summary>
        public bool Frames(Sign first) => first && root && root.activeSelf && ReferenceEquals(sign, first);

        public void Destroy()
        {
            if (root) Object.Destroy(root);
            if (stripesNeutral) Object.Destroy(stripesNeutral);
            stripesNeutral = null;
            reported = false;
            noted = null;                                                   // a new window says it all again
            unsizedSince = -1f;
            root = null;
            sign = null;
            shownKey = null;
        }
    }
}
