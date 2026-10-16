using System;
using System.Collections.Generic;
using SignLink.Animation;
using SignLink.Pictures;
using SignLink.Signs;
using SignLink.UI.Sources;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace SignLink.UI
{
    /// <summary>What a live preview shows: the texts the put writes (one a sign, the first sign's first), or a picture.</summary>
    internal sealed class PreviewContent
    {
        /// <summary>The texts as the put writes them (FitTexts); null for a picture.</summary>
        public IList<string> Texts;
        /// <summary>A picture's pixels: the quick view's texture, the very grid Place encodes.</summary>
        public Texture Picture;
        /// <summary>The picture lit by the scene, as the sign's text material is (else glowing).</summary>
        public bool Lit;
        /// <summary>The picture's size and where its middle sits as made, in wu from the sign's centre.</summary>
        public double Wwu, Hwu, C0x, C0y;
        /// <summary>The fit it goes on with: the pose, and a picture's Right and Up.</summary>
        public Fit Fit;
        /// <summary>What it shows: the texts are set again only when this changes.</summary>
        public string Key;
        /// <summary>Built whatever its size (the Text source's text, as its on-sign preview was before 0.20.129; it
        /// holds at most TextSource.MaxChars), not only up to ContentPreview.MostGlyphs.</summary>
        public bool AnySize;
        /// <summary>The fit the texts were made for, from the point TextsC0 as made (FitTexts.Result: Fit, C0), and whether
        /// they were moved for it (Movable). Fit may be a newer pose than TextsFit: a big text is made again only once the
        /// rows stand still, and meanwhile its texts shift by what the new Rotate moves them (0.20.153, the author: while
        /// the Rotate slider was held the picture showed beside where it falls).</summary>
        public Fit TextsFit;
        public double TextsC0x, TextsC0y;
        public bool TextsMove;
        /// <summary>The whole fit the rows ask for now (place and size too, not only the pose): where the texts' middle
        /// should show (0.20.156, the author: while the Tilt slider was held the picture went the other way; the rows keep
        /// the content where it sits on the face as the tilt changes, Up and Out changing with it, FitPanel.Fit, and the
        /// texts' own Up and Out in the new pose moved it).</summary>
        public Fit Want;
        /// <summary>A moving piece's frames, each one sign's text as the put writes it (Frames[0] is Texts[0]), and how many
        /// a second: the preview plays them on the sign as SignLink players will see it move (0.20.165, the author: they should
        /// move at once not only in the quick view but in the sign's content too). Null: a still.</summary>
        public IList<string> Frames;
        public float Fps;

        /// <summary>The same texts while the rows ask for another fit (a big one while its rows move).</summary>
        public PreviewContent Posed(Fit want) => new PreviewContent
        {
            Texts = Texts, Fit = TextsFit.WithPose(want), Key = Key, AnySize = AnySize,
            TextsFit = TextsFit, TextsC0x = TextsC0x, TextsC0y = TextsC0y, TextsMove = TextsMove, Want = want,
            Frames = Frames, Fps = Fps,
        };

        /// <summary>Texts as FitTexts made them.</summary>
        public static PreviewContent Made(IList<string> texts, FitTexts.Result r, string key, bool anySize = false) => new PreviewContent
        {
            Texts = texts, Fit = r.Fit, Key = key, AnySize = anySize,
            TextsFit = r.Fit, TextsC0x = r.C0x, TextsC0y = r.C0y, TextsMove = r.Movable, Want = r.Fit,
        };

        /// <summary>A moving piece's frames as FitTexts made them (r.Texts: one text a frame), shown from its first.</summary>
        public static PreviewContent Moving(IList<string> frames, FitTexts.Result r, string key, float fps)
        {
            PreviewContent c = Made(new[] { frames[0] }, r, key);
            c.Frames = frames;
            c.Fps = fps;
            return c;
        }
    }

    /// <summary>The result before the button puts it on (0.20.129, the author: the content should react and update on
    /// every change too, so we see the end result instead of the half-done old picture; useful when making a new
    /// picture as well, to show the end result and not only the layer). On this computer only, copies of the selected
    /// sign's text canvas stand where the stack's signs will stand in the chosen pose (SignPose.Preview; the copies
    /// Step apart, as SignPose.Set makes them) and show the texts the put writes (a text, a piece, or what the sign
    /// holds moved or relit: FitTexts, the texts SignFit itself writes), or a picture's quick view at its size and
    /// place (a RawImage, glowing or lit, as a GIF's screen is). A pose, a place or a picture's size only moves them;
    /// texts are set again when they change (a big one once the rows stand still). The sign itself shows nothing
    /// meanwhile (HeldVeil). Nothing goes into the world: other players see the sign as it is until the button, and
    /// once it is put on (nothing new to put) the preview goes.</summary>
    internal sealed class ContentPreview
    {
        /// <summary>The most glyphs a text preview builds (about half a second of TMP mesh, SaveCost.GlyphSeconds); a
        /// bigger content shows its frame only, as before.</summary>
        public const long MostGlyphs = 40000;

        /// <summary>A text preview of more glyphs than this (about 40 ms) is set again only when the rows have stood still
        /// for Settle seconds and no control is held: not a rebuild a slider step.</summary>
        const long QuickGlyphs = 3000;
        const float Settle = 0.3f;

        GameObject root;
        Sign from;                                                          // the sign whose text canvas the copies are
        Canvas canvasLooked;                                                // the last canvas Ensure looked at, and whether it is
        bool canvasOwn;                                                     // the text's own (no sign, no ZNetView in it)
        readonly List<GameObject> copies = new List<GameObject>();
        readonly List<TMP_Text> widgets = new List<TMP_Text>();
        readonly List<string> shown = new List<string>();
        readonly List<Vector3> bases = new List<Vector3>();                // each copy's text where the canvas had it
        RawImage image;
        // the texts shown were made for this fit from this point (PreviewContent.TextsFit), and the shift they have now
        Fit shownFit;
        double shownC0x, shownC0y;
        bool shownMove;
        double shiftX, shiftY;
        bool imageLit;
        string wantKey, shownKey, heldKey, heldWant, heldStack, glyphsKey, noted, meshFor;
        float wantAt, heldWantAt;
        long glyphs;
        PreviewContent held;
        IList<string> heldTexts;                                            // the held stack's texts as read (HeldMoved)
        string[] heldFrames;                                                // a saved animation's frames, and its pace
        float heldFps;
        Fit heldWas;
        double heldC0x, heldC0y;
        bool heldKnown;

        /// <summary>A preview shows on the selected sign now (HeldVeil hides the sign's own content meanwhile).</summary>
        public bool Showing => root && on && shownKey != null;

        /// <summary>The copies' canvases are on: shown or hidden by them, never by switching objects off (a text switched
        /// back on has its whole mesh built again; a canvas keeps it).</summary>
        bool on;

        void SetOn(bool want)
        {
            on = want;
            foreach (GameObject c in copies)
            {
                Canvas cv = c ? c.GetComponent<Canvas>() : null;
                if (cv && cv.enabled != want) cv.enabled = want;
            }
        }

        /// <summary>A picture lit by the scene shows (its material writes depth on the text's plane): the frame keeps
        /// clear of it as of a GIF's lit screen (Ghost: OverScreen; 0.20.130, review).</summary>
        public bool LitPicture => Showing && image && image.gameObject.activeSelf && imageLit;

        /// <summary>The first sign's copy's text as shown (TextSource measures it for the frame), or null.</summary>
        public TMP_Text First => Showing && widgets.Count > 0 && widgets[0] && widgets[0].enabled ? widgets[0] : null;

        /// <summary>Layout events only, after the source's Tick and the frame: the preview for the session's choice on the
        /// selected sign, or none. Nothing changes while a put runs (busy: as it was until the new content is in).</summary>
        public void Follow(Session s, Source src, float now, bool busy)
        {
            if (busy) return;
            PreviewContent c = Want(s, src, now);
            Sign target = c == null ? null : s.First;
            if (c == null || !target || !target.m_textWidget)
            {
                Hide();
                return;
            }
            int n = c.Picture != null ? 1 : c.Texts.Count;
            if (!Ensure(target, n))
            {
                Note("none: the sign's text has no canvas");
                Hide();
                return;
            }
            Pose(s, target, c.Fit);
            if (c.Picture != null) ShowPicture(c);
            else
            {
                ShowTexts(c, now);
                Play(c, now, s.Verb == Verb.Refit || s.Verb == Verb.Apply ? target : null);   // a new pick is not the paused one
            }
            Shift(c.Picture == null ? c.Want : (Fit?)null);
            if (shownKey == null)                                           // nothing yet: a big text waits for the rows
            {
                if (on) SetOn(false);
                return;
            }
            if (!on) SetOn(true);
            bool own = Change.SamePose(s.Held.Fit, c.Fit), picture = c.Picture != null;
            if (!ReferenceEquals(notedOn, target) || notedPicture != picture || notedLit != imageLit || notedOwn != own || notedN != n)
            {
                notedOn = target;
                notedPicture = picture;
                notedLit = imageLit;
                notedOwn = own;
                notedN = n;
                Note($"shown on {SignFinder.Id(target)}: {(picture ? $"a picture, {(imageLit ? "lit" : "glowing")}" : $"{n} text(s)")}, " +
                     $"{(own ? "as the sign stands" : "at the pose to come")}");
            }
        }

        // what the log said last ("shown on"): said again only when one of these changes, not built every Layout
        Sign notedOn;
        bool notedPicture, notedLit, notedOwn;
        int notedN;

        /// <summary>What to preview: the source's own result (a picture, a text, a piece), else what the sign holds moved or
        /// relit (Refit, Apply); nothing when nothing new would go on, the sign cannot be changed, or it is too big.</summary>
        PreviewContent Want(Session s, Source src, float now)
        {
            if (!s.HasTarget || s.Verb == Verb.None || s.Reason != null) return null;
            PreviewContent c = src?.Preview(s);
            if (c == null && (s.Verb == Verb.Refit || s.Verb == Verb.Apply)) c = HeldMoved(s, now);
            if (c == null || c.Picture != null) return c;
            if (c.Texts == null || c.Texts.Count == 0) return null;
            if (c.Key != glyphsKey)
            {
                glyphsKey = c.Key;
                glyphs = SaveCost.Glyphs(c.Texts);
            }
            if (glyphs <= MostGlyphs || c.AnySize) return c;
            Note($"none: {glyphs} glyphs is over the {MostGlyphs} a preview builds (the frame shows where it goes)");
            return null;
        }

        /// <summary>What the stack holds as Refit would leave it (FitTexts.ForRefit, the chosen fit and light); a saved
        /// animation by its first frame. Worked out again only when the fit, the light or the stack changes; a big one once
        /// the rows stand still (its last texts meanwhile, in the pose to come).</summary>
        PreviewContent HeldMoved(Session s, float now)
        {
            if (s.Hidden || !s.GlyphsKnown || s.Glyphs > MostGlyphs) return null;   // a text the game hides: not shown (0.20.132)
            Sign root0 = s.First;
            if (!root0) return null;
            Fit to = s.Chosen.Fit;
            GlowMode light = s.Chosen.Light;
            string key = $"held|{s.StackKey}|{FitKey(to)}|{light}";
            if (key == heldKey) return held;
            if (key != heldWant)
            {
                heldWant = key;
                heldWantAt = now;
            }
            if (held != null && heldStack == s.StackKey && s.Glyphs > QuickGlyphs && (now - heldWantAt < Settle || GUIUtility.hotControl != 0))
                return held.Posed(to);
            heldKey = key;
            if (heldStack != s.StackKey || heldTexts == null)               // read once a stack, not at every step of a row
            {
                heldWas = SignFit.Read(root0, out heldC0x, out heldC0y, out heldKnown);
                heldFrames = StoredAnims.TryUnpack(StoredAnims.Blob(root0), out string[] frames, out heldFps) && frames.Length > 0
                    ? frames : null;
                heldTexts = heldFrames != null ? new[] { heldFrames[0] } : (IList<string>)SignBuilder.Content(root0);
            }
            heldStack = s.StackKey;
            if (heldFrames != null && heldFrames.Length > 1 && heldFps > 0f)  // its frames moved and relit alike, played (0.20.165)
            {
                FitTexts.Result m = FitTexts.ForRefit(heldFrames, heldWas, heldC0x, heldC0y, heldKnown, to, light);
                held = PreviewContent.Moving(m.Texts ?? heldFrames, m, key, heldFps);
                return held;
            }
            FitTexts.Result r = FitTexts.ForRefit(heldTexts, heldWas, heldC0x, heldC0y, heldKnown, to, light);
            held = PreviewContent.Made(r.Texts ?? heldTexts, r, key);
            return held;
        }

        public static string FitKey(Fit f) => $"{f.Scale}|{f.Right}|{f.Up}|{f.Out}|{f.Turn}|{f.Tilt}|{f.Edge}";

        /// <summary>n copies of the target's text canvas under the preview's root (another sign, or a copy gone: made
        /// anew). A copy carries none of SignLink's own children (the frame, a GIF's screen) and starts empty, so the
        /// sign's text is not built again for it.</summary>
        bool Ensure(Sign target, int n)
        {
            if (!ReferenceEquals(from, target) || copies.Exists(c => !c)) Clear();
            Canvas canvas = target.m_textWidget.GetComponentInParent<Canvas>();
            if (!canvas) return false;
            // only the text's own canvas is copied: one that holds the sign itself would copy a networked piece; looked at
            // once a canvas (0.20.132, review: its whole tree searched every Layout)
            if (!ReferenceEquals(canvas, canvasLooked))
            {
                canvasLooked = canvas;
                canvasOwn = !canvas.GetComponent<ZNetView>() && !canvas.GetComponentInChildren<Sign>(true);
            }
            if (!canvasOwn) return false;
            if (!root) root = new GameObject("SignLinkPreview");
            from = target;
            while (copies.Count < n)
            {
                GameObject go = Object.Instantiate(canvas.gameObject, root.transform, false);
                go.name = "SignLinkPreviewCanvas";
                copies.Add(go);                                             // tracked at once: whatever follows, it goes with the rest
                widgets.Add(null);
                shown.Add("");
                posed = false;
                if (copies.Count == 1)
                    Note($"copies of '{canvas.name}': {string.Join(", ", Array.ConvertAll(go.GetComponentsInChildren<Component>(true), k => k ? k.GetType().Name : "(missing script)"))}");
                Strip(go.transform);
                TMP_Text t = go.GetComponentInChildren<TMP_Text>(true);
                if (!t)
                {
                    Clear();
                    return false;
                }
                t.text = "";
                t.enabled = true;                                           // the sign's own may be hidden (HeldVeil, a screen)
                Canvas cv = go.GetComponent<Canvas>();
                if (cv) cv.enabled = on;                                    // copied from a veiled sign: off; shown with the rest
                widgets[widgets.Count - 1] = t;
                bases.Add(t.transform.localPosition);
            }
            while (copies.Count > n)
            {
                int i = copies.Count - 1;
                Object.Destroy(copies[i]);
                copies.RemoveAt(i);
                widgets.RemoveAt(i);
                shown.RemoveAt(i);
                bases.RemoveAt(i);
            }
            return true;
        }

        static readonly List<Transform> stripped = new List<Transform>();

        /// <summary>SignLink's own objects under a copied canvas go: the frame's bars, a GIF's screen.</summary>
        static void Strip(Transform t)
        {
            stripped.Clear();
            foreach (Transform c in t.GetComponentsInChildren<Transform>(true))
                if (c != t && (c.name == "SignLinkGhost" || c.name == "SignLinkScreen")) stripped.Add(c);
            foreach (Transform c in stripped)
                if (c) Object.DestroyImmediate(c.gameObject);
            stripped.Clear();
        }

        /// <summary>The copies where the stack's signs will stand: the sign's own pose while the chosen one is its own (as
        /// the frame does: a pose worked out again lies a fraction of a degree off), else the pose to come; the copies
        /// FloorPlan.Step (tipped) or SignBuilder.DefaultStep apart in front of the first, as SignPose.Set makes them.</summary>
        void Pose(Session s, Sign target, Fit fit)
        {
            bool own = Change.SamePose(s.Held.Fit, fit);
            Transform t0 = target.transform;
            Vector3 at = t0.position, pending = SignMove.Pending(target);
            Quaternion facing = t0.rotation;
            if (posed && ReferenceEquals(posedOn, target) && posedOwn == own && posedTilt == fit.Tilt && posedTurn == fit.Turn &&
                posedEdge == fit.Edge && posedAt == at && posedFacing == facing && posedPending == pending) return;
            posed = true;
            posedOn = target;
            posedOwn = own;
            posedTilt = fit.Tilt;
            posedTurn = fit.Turn;
            posedEdge = fit.Edge;
            posedAt = at;
            posedFacing = facing;
            posedPending = pending;
            s.PendingPose(target, fit, out Vector3 pos, out Quaternion rot);
            Transform canvas = target.m_textWidget.GetComponentInParent<Canvas>().transform;
            Vector3 lp = t0.InverseTransformPoint(canvas.position);
            Quaternion lr = Quaternion.Inverse(t0.rotation) * canvas.rotation;
            float step = Math.Abs(fit.Tilt) > 0.05 ? (float)FloorPlan.Step : SignBuilder.DefaultStep;
            for (int i = 0; i < copies.Count; i++)
            {
                Transform t = copies[i].transform;
                t.SetPositionAndRotation(pos + rot * (lp + Vector3.forward * (step * i)), rot * lr);
                t.localScale = canvas.lossyScale;
            }
        }

        // the pose the copies stand in (Pose): placed again only when one of these changes
        bool posed, posedOwn, posedEdge;
        Sign posedOn;
        double posedTilt, posedTurn;
        Vector3 posedAt, posedPending;
        Quaternion posedFacing;

        /// <summary>The texts on the copies, each set only when it changed; a big one once the rows stand still.</summary>
        void ShowTexts(PreviewContent c, float now)
        {
            if (image) image.gameObject.SetActive(false);
            if (c.Key != wantKey)
            {
                wantKey = c.Key;
                wantAt = now;
            }
            if (c.Key == shownKey) return;
            if (glyphs > QuickGlyphs && (now - wantAt < Settle || GUIUtility.hotControl != 0)) return;   // the last one shows meanwhile
            bool first = false;
            for (int i = 0; i < widgets.Count; i++)
            {
                string t = c.Texts[i] ?? "";
                widgets[i].enabled = true;
                if (shown[i] == t) continue;
                widgets[i].text = t;
                shown[i] = t;
                if (i == 0) first = true;
            }
            // its bounds at once, for the Text source's frame, only when its text changed (a pose alone changes no text)
            if (first || !ReferenceEquals(meshFor, shown[0])) widgets[0].ForceMeshUpdate();
            meshFor = shown[0];
            shownKey = c.Key;
            frameFor = null;                                                // a moving one starts again from what shows
            shownFit = c.TextsFit;
            shownC0x = c.TextsC0x;
            shownC0y = c.TextsC0y;
            shownMove = c.TextsMove;
        }

        // the frame a moving preview shows, and for which frames
        int frameAt;
        IList<string> frameFor;

        /// <summary>A moving piece's frames on the preview's sign, on the shared clock as the saved animation will play
        /// (frame = time x fps): only once its own texts show, while glyph animations play and this one is not paused (the Playing tab),
        /// and when the frames are light enough to build at their pace (StoredAnims' 100 ms a second, SaveCost.GlyphSeconds);
        /// else its first frame stays.</summary>
        void Play(PreviewContent c, float now, Sign target)
        {
            if (c.Frames == null || c.Frames.Count < 2 || c.Fps <= 0f || shownKey != c.Key || widgets.Count == 0 || !widgets[0]) return;
            bool afford = glyphs * SaveCost.GlyphSeconds * c.Fps <= 0.1;
            double t = ZNet.instance ? ZNet.instance.GetTimeSeconds() : now;
            // a sign paused on the Playing tab too (0.20.182, the author: I paused it but it keeps moving until I close the window)
            bool paused = target && Plugin.Stored != null && Plugin.Stored.Paused(target);
            int i = PluginConfig.PlayAnimations.Value && afford && !paused ? (int)(t * c.Fps % c.Frames.Count) : 0;
            if (i == frameAt && ReferenceEquals(frameFor, c.Frames)) return;
            frameAt = i;
            frameFor = c.Frames;
            string text = c.Frames[i] ?? "";
            if (shown[0] == text) return;
            widgets[0].text = text;
            shown[0] = text;
        }

        /// <summary>The texts shown, made for shownFit, while the rows ask for another fit: moved along the sign's face so
        /// their middle shows where the wanted fit puts it (Fit.Centre: the place, the tilt and the Rotate, as the frame
        /// draws it), so a big text whose new texts wait for the rows to stand still stays with its frame (0.20.153 Rotate,
        /// 0.20.156 Tilt, Right, Up). Its size stays as shown until then. Text-local units are half a wu (ShowPicture). A
        /// picture: none.</summary>
        void Shift(Fit? want)
        {
            double sx = 0, sy = 0;
            if (want.HasValue && shownKey != null && shownMove)
            {
                want.Value.Centre(shownC0x, shownC0y, out double tx, out double ty);
                shownFit.Centre(shownC0x, shownC0y, out double fx, out double fy);
                sx = tx - fx;
                sy = ty - fy;
            }
            if (Math.Abs(sx - shiftX) < 1e-4 && Math.Abs(sy - shiftY) < 1e-4) return;
            shiftX = sx;
            shiftY = sy;
            var by = new Vector3((float)(sx / 2), (float)(sy / 2), 0f);
            for (int i = 0; i < widgets.Count; i++)
            {
                Transform t = widgets[i] ? widgets[i].transform : null;
                if (t) t.localPosition = bases[i] + t.localRotation * Vector3.Scale(t.localScale, by);
            }
        }

        /// <summary>The picture over the first copy's text (hidden), at its size and place: 1 text-local unit = 2 wu, as
        /// a GIF's screen (ScreenPlayer.StartTimed); lit by the scene with the screens' material, else glowing.</summary>
        void ShowPicture(PreviewContent c)
        {
            TMP_Text w = widgets[0];
            w.enabled = false;
            if (!image)
            {
                var go = new GameObject("SignLinkPreviewPicture", typeof(RectTransform));
                var rt = (RectTransform)go.transform;
                rt.SetParent(w.transform, false);
                rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
                image = go.AddComponent<RawImage>();
                image.raycastTarget = false;
                imageLit = false;
            }
            if (!image.gameObject.activeSelf) image.gameObject.SetActive(true);
            if (image.texture != c.Picture) image.texture = c.Picture;
            Material lit = c.Lit ? ScreenPlayer.LitMaterial() : null;
            if (imageLit != (lit != null))
            {
                image.material = lit;                                       // null: the default UI material, unlit
                imageLit = lit != null;
            }
            Fit f = c.Fit;
            // its size is its own (Wwu, Hwu); its middle is where the frame and the put have it: a sign's own picture whose
            // box could not be padded sits off by its shift at the chosen scale (PictureSource.Centre gives shift / scale;
            // 0.20.157, review: forced to scale 1 it showed at shift / scale)
            f.Centre(c.C0x, c.C0y, out double cx, out double cy);
            var irt = (RectTransform)image.transform;
            irt.sizeDelta = new Vector2((float)(c.Wwu / 2), (float)(c.Hwu / 2));
            irt.anchoredPosition = new Vector2((float)(cx / 2), (float)(cy / 2));
            shownKey = c.Key;
        }

        public void Hide()
        {
            if (on) SetOn(false);
            noted = null;
            notedOn = null;
        }

        /// <summary>The copies go (another sign, the window shut): made anew when needed.</summary>
        void Clear()
        {
            foreach (GameObject c in copies)
                if (c) Object.Destroy(c);
            copies.Clear();
            widgets.Clear();
            shown.Clear();
            bases.Clear();
            shiftX = shiftY = 0;
            image = null;
            imageLit = false;
            from = null;
            shownKey = wantKey = meshFor = null;
            posed = false;
            notedOn = null;
            on = false;
        }

        public void Destroy()
        {
            Clear();
            if (root) Object.Destroy(root);
            root = null;
            heldKey = heldWant = heldStack = null;
            held = null;
            heldTexts = null;
            noted = null;
        }

        void Note(string what)
        {
            if (what == noted) return;
            noted = what;
            Plugin.Log.LogInfo("the preview: " + what);
        }
    }
}
