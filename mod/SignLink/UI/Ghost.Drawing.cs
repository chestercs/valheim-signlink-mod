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
    /// <summary>The bars themselves: materials, meshes, where each bar goes, and their end.</summary>
    internal partial class Ghost
    {
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
            unheldShown = false;                                            // and the board its own colour
            return true;
        }

        bool unheldShown;
        /// <summary>The red glows less than the lime: at BoardGlow (2.2) the bloom turned it coral, read as orange.</summary>
        const float UnheldGlow = 1.4f;

        /// <summary>The board's bars in the frame's own colour, or in Unheld's while the game would not hold the sign where
        /// the steps or the pose to come put it (the same quads, in the other colour, glowing the same).</summary>
        void Hold(bool unheld)
        {
            if (unheld == unheldShown || !root) return;
            unheldShown = unheld;
            Color c = unheld ? Unheld : boardColor;
            foreach (Bar b in board)
            {
                if (b == null || !b.Go) continue;
                if (b.Quad) b.Go.GetComponent<MeshFilter>().sharedMesh = QuadMesh(c, unheld ? Mathf.Min(boardGlow, UnheldGlow) : boardGlow);
                else b.Img.color = c;
            }
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
