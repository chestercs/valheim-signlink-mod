using System;
using System.Collections.Generic;
using SignLink.Pictures;
using SignLink.Signs;
using SignLink.UI.Sources;
using UnityEngine;
using PetyusPolisz.Kit;

namespace SignLink.UI
{
    /// <summary>2 Content: what goes on the sign. A toolbar of sources (Art, Picture, Text), the picked source's
    /// body (its list, settings and light), and a footer that leads on to 3 Place, where the content is placed: nothing
    /// goes on the sign from here, so the place, the pose and the frame in the world are seen before it does.</summary>
    internal sealed class ContentTab : Tab
    {
        readonly Source[] sources;
        string[] names;
        int at;
        /// <summary>The player's own source, back with their own slot (SourceFollow).</summary>
        readonly SourceFollow follow = new SourceFollow();

        public ContentTab(SignLinkWindow w) : base(w)
        {
            // Art first, the one the strip opens on, then pictures, and the text editor last (2026-10-16, the author after a
            // player's test: the text editor last and Art first, in the strip as in use; Text led from 2026-10-12). Saved
            // went in 0.20.63: a frozen copy of a sign could not change its resolution for the multiplayer limits; a
            // picture's file and Copy / Paste do what it did
            sources = new Source[] { new ArtSource(), new PictureSource(), new TextSource() };
            w.Session.LoadInto = (r, into) =>
            {
                for (int i = 0; i < sources.Length; i++)
                    if (sources[i].Kind == r.Kind)
                    {
                        if (!sources[i].Load(r, into, w.Session)) return false;
                        follow.Leaving(w.Session.Editing, at);              // leaving the player's own choice (Editing is the slot before this load)
                        if (at != i) sources[at].OnLeave();
                        at = i;
                        return true;
                    }
                return false;
            };
        }

        public override string Title => "2 Content";
        public override bool UsesTarget => true;
        public override void OnOpen() { foreach (Source s in sources) s.OnOpen(); }

        public override void OnClose()
        {
            foreach (Source s in sources) s.OnClose();
        }

        public Source Current => sources[at];

        KeyCode arrowHeld;
        float arrowAt;

        /// <summary>Arrows (from the window's Update) while 2 Content is open and no text field has the keyboard: up and down
        /// step the source's list (Source.Step; held, they repeat), left and right shut and open the pick's group
        /// (Source.Fold). On 3 Place the arrows move the sign.</summary>
        public void Keys(Session s)
        {
            if (GUIUtility.keyboardControl != 0 || Console.IsVisible() || (Chat.instance && Chat.instance.HasFocus()) || !Source.Ready(s))
            {
                arrowHeld = KeyCode.None;
                return;
            }
            if (Input.GetKeyDown(KeyCode.LeftArrow)) sources[at].Fold(s, false);     // the pick's group shut
            else if (Input.GetKeyDown(KeyCode.RightArrow)) sources[at].Fold(s, true);   // ... or open
            KeyCode k = Input.GetKeyDown(KeyCode.UpArrow) ? KeyCode.UpArrow : Input.GetKeyDown(KeyCode.DownArrow) ? KeyCode.DownArrow : KeyCode.None;
            if (k != KeyCode.None)
            {
                arrowHeld = k;
                arrowAt = Time.unscaledTime + 0.4f;
            }
            else if (arrowHeld != KeyCode.None && Input.GetKey(arrowHeld) && Time.unscaledTime >= arrowAt)
            {
                k = arrowHeld;
                arrowAt = Time.unscaledTime + 0.12f;
            }
            else if (arrowHeld != KeyCode.None && !Input.GetKey(arrowHeld)) arrowHeld = KeyCode.None;
            if (k != KeyCode.None) sources[at].Step(s, k == KeyCode.UpArrow ? -1 : 1);
        }

        /// <summary>The source strip, in view whatever the body's scroll; a new source starts its body at the top.</summary>
        public override void Pinned()
        {
            Session s = Window.Session;
            if (names == null) names = Array.ConvertAll(sources, x => x.Title);
            int pick = Widgets.Toolbar(at, names, Theme.Tab, Widgets.W(Widgets.ContentWidth - 30));
            if (pick != at)
                Window.Later(() =>
                {
                    Current.OnLeave();                                      // its buffers go with the pick
                    at = pick;
                    s.Chosen.Recipe = SignRecipe.None;
                    // a picture's glow does not carry over to a piece; back on the source of what the sign holds, its own light
                    // (0.20.148, review: Text and back to Art showed As made and offered an update that put it unlit)
                    s.Chosen.Light = s.Editing && sources[pick].Kind == s.Held.Recipe.Kind ? Change.LightOf(s.Held) : GlowMode.AsMade;
                    Window.ScrollToTop();
                });

        }

        public override void OnDestroy()
        {
            foreach (Source src in sources) src.Dispose();
        }

        public override void Body()
        {
            Session s = Window.Session;
            if (Event.current.type == EventType.Layout)
            {
                Follow(s);
                Current.Tick(s, Time.unscaledTime);
            }
            Current.Draw(s, Window, Time.unscaledTime);
        }

        /// <summary>Layout events only, every frame whatever tab shows (the window calls it): when the player's own choice
        /// is back (an empty sign was selected, or none), the strip goes back to its own source (SourceFollow, which keeps
        /// it while the own slot shows); done only on 2 Content before, so ticking the sign's source on another tab
        /// overwrote the player's pick.</summary>
        /// <summary>A source is still working on a put (a picture being encoded): nothing else goes on meanwhile, or it
        /// would land under the picture (0.20.148, review). Set every Layout by Follow.</summary>
        public static bool SourcesBusy;

        public void Follow(Session s)
        {
            SourcesBusy = Array.Exists(sources, x => x.Busy);
            int want = follow.Follow(s.Chosen, s.Editing, at);
            if (want == at) return;
            Current.OnLeave();
            at = want;
        }

        /// <summary>The way on: 3 Place, where the verb button is; greyed with the reason while nothing can go on yet
        /// (nothing picked, the picture still being read, a ward).</summary>
        public override void Footer()
        {
            Session s = Window.Session;
            Source src = Current;
            Verb v = s.Verb;
            bool has = s.HasTarget;
            bool puts = v == Verb.Place || v == Verb.Put || v == Verb.Update;   // a pose or a refit needs no pick
            string problem = !has ? null : puts ? src.Problem(s) ?? src.Limit(s) ?? s.Reason : s.Reason;
            GUI.enabled = has && problem == null;
            // in the way-on yellow whenever it can be pressed, as the bottom button of every tab (1.0.2, the author: editing
            // a sign, Next was grey; before, 0.20.56, it glowed only when something would go on or change)
            string label = !has ? "Select a sign first" : problem != null && src.Calculating(s) ? "Calculating ..."   // grey while it counts (0.20.134)
                : "Next ▸";                                                 // the clearest word (1.0.2, the author)
            if (Widgets.NextButton(label))
                Window.Later(() => Window.ShowTab("3 Place"));
            GUI.enabled = true;
            // the area limit under the button, where a line on what 3 Place does was (0.20.72, the author: delete this
            // "On 3 place: its size its place" blah text ... and put the area KB indicator there)
            AreaMeter.Draw(s, Change.AreaBytes(v, src.Bytes(s)), underButton: true);   // multiplayer: the area limit, with the pick in place
            if (has && problem != null) Widgets.Hint(problem);              // only what stops it (1.0.2, the author: no long texts)
            else if (has && Plugin.Preview.Showing) Widgets.Hint("Preview: only you");   // ContentPreview (0.20.129)
        }

        /// <summary>What putting something on also does to the selected sign: " and lay it face up", " and stand it up",
        /// " and tilt it to 30°", " and rotate it", or nothing (the choice's pose against the sign's).</summary>
        /// <summary>The button's words (0.20.180, the author: just write update sign, update content (whichever it is,
        /// or both)): what changes, the sign (its pose) or the content (a new item, its size, place, light or
        /// settings), or both.</summary>
        static string VerbLabel(Session s, Verb v)
        {
            Fit c = s.Chosen.Fit;
            bool sign = Math.Abs(c.Tilt - s.SignTilt) > 0.05 || Math.Abs(Mathf.DeltaAngle((float)c.Turn, s.SignTurn)) > 0.05;
            bool content = v == Verb.Place || v == Verb.Put || v == Verb.Update ||
                           Change.SizeChanged(s.Held, s.Chosen) || Change.PlaceChanged(s.Held, s.Chosen) || Change.LightChanged(s.Held, s.Chosen);
            return sign && content ? "Update sign and content" : content ? "Update content" : "Update sign";
        }

        /// <summary>The Place tab's footer: the verb button. Place, Put and Update go through the source (and its
        /// red-level question); Refit and Apply move what the sign holds (Perform).</summary>
        public static void DrawVerbButton(SignLinkWindow w, Source src, Confirm confirm)
        {
            Session s = w.Session;
            Verb v = s.Verb;
            string problem = src.Problem(s) ?? src.Limit(s) ?? s.Reason;
            string key = s.Chosen.Recipe.ToString() + "|" + s.Chosen.Fit.Describe(s.Chosen.Light);
            string pose = PoseProblem(s);                                 // in red: the sliders took it, the button may not
            if (!s.HasTarget) Grey("Select a sign first", null);
            else if (v == Verb.None)                                        // the label stays short; why, under it
                Grey("As it is", s.Chosen.Recipe.Kind == SignRecipe.Kinds.None
                    ? null : problem);
            else if (v == Verb.Apply || v == Verb.Refit)
            {
                GUI.enabled = s.Reason == null && pose == null && !w.Busy && !SourcesBusy;
                string label = VerbLabel(s, v);
                if (Widgets.NextButton(w.Busy ? "Working ..." : label))
                {
                    w.CloseAfterPut();                                      // done: the window closes (an error keeps it)
                    Perform(w, src, v);
                }
                GUI.enabled = true;
                if (s.Reason != null) Widgets.Hint(s.Reason);
                else if (pose != null) GUILayout.Label(pose, Theme.Warn);
            }
            else if (!src.Busy && confirm.Armed(key))
            {
                if (confirm.Ask(w, "Put it on anyway", problem))
                {
                    if (confirm.KeepsOpen) w.KeepOpen();
                    else w.CloseAfterPut();
                    src.Act(s, w, v);
                }
            }
            else
            {
                bool busy = src.Busy || w.Busy || SourcesBusy;                // another tab's picture still being made too (0.20.148)
                GUI.enabled = problem == null && pose == null && !busy;
                // grey because it still reads or counts (a press waiting for its count too): "Calculating ..." (0.20.134)
                bool calculating = (problem != null || busy) && src.Calculating(s);
                Stuck(calculating, problem);
                if (Widgets.NextButton(calculating ? "Calculating ..." : busy ? "Working ..." : VerbLabel(s, v)))
                {
                    if (src.Heavy(s, out double load, out long save, out long largest))
                        confirm.Arm(w, key, src.What(s), load, save, largest, src.Lighter(s));   // its numbers as they are now
                    else
                    {
                        w.CloseAfterPut();
                        src.Act(s, w, v);
                    }
                }
                GUI.enabled = true;
                if (problem != null && !busy) Widgets.Hint(problem);
                else if (pose != null) GUILayout.Label(pose, Theme.Warn);
            }
        }

        static string poseKey, poseWhy;
        static float poseAt;

        /// <summary>Why the sign may not take the chosen pose (SignPose.Check: the game would not hold it up there, a ward,
        /// a spot where nothing is built), or null: the Tilt and Rotate rows take any pose and show it, and only the button
        /// is refused (1.0.2, the author: allow it, just do not let it apply, with the red text that it has no support).
        /// Steps not applied that the game would not hold up refuse it too. Weighed again when the pose or the sign's place
        /// changes, and a reason each second.</summary>
        internal static string PoseProblem(Session s)
        {
            PoseUnheld = false;
            if (!s.HasTarget) return null;
            // steps not applied that the game would not hold up: nothing else may write the sign either (SignMove.Write)
            string unheld = SignMove.AnyUnheld ? SignMove.Unheld(s.First) : null;
            if (unheld != null)
            {
                PoseUnheld = true;
                return "No support: " + unheld;
            }
            Fit c = s.Chosen.Fit;
            if (Math.Abs(c.Tilt - s.SignTilt) <= 0.05 && Math.Abs(Mathf.DeltaAngle((float)c.Turn, s.SignTurn)) <= 0.05) return null;
            Sign t = s.Target;
            if (!t) return null;
            string key = $"{SignFinder.Id(t)}|{c.Tilt}|{c.Turn}|{c.Edge}|{t.transform.position.ToString("F4")}|{t.transform.rotation.ToString("F5")}";
            // a reason weighed again each second: some pass by themselves (the sign's copies still loading)
            if (key == poseKey && (poseWhy == null || Time.unscaledTime - poseAt < 1f))
            {
                PoseUnheld = poseWeak;
                return poseWhy;
            }
            poseKey = key;
            poseAt = Time.unscaledTime;
            try
            {
                SignPose.Check(t, (float)c.Tilt, (float)c.Turn, 0f, c.Edge);
                poseWhy = null;
                poseWeak = false;
            }
            catch (Exception e)
            {
                poseWhy = "The sign cannot take this pose: " + e.Message + ".";
                poseWeak = SignMove.WeakReason(e.Message);
            }
            PoseUnheld = poseWeak;
            return poseWhy;
        }

        /// <summary>Was the last PoseProblem the sign's support? The frame turns red for that alone (review: a ward, a spot
        /// taken or copies still loading made it red too, and the colour says "nothing would hold it").</summary>
        internal static bool PoseUnheld;
        static bool poseWeak;

        static float calculatingSince = -1f;
        static bool calculatingSaid;

        /// <summary>A button that has said "Calculating ..." for 20 s says why in the log, once a time (0.20.134: what to
        /// look for if a count ever hangs again).</summary>
        static void Stuck(bool calculating, string problem)
        {
            if (Event.current.type != EventType.Repaint) return;
            float now = Time.unscaledTime;
            if (!calculating)
            {
                calculatingSince = -1f;
                calculatingSaid = false;
                return;
            }
            if (calculatingSince < 0f) calculatingSince = now;
            if (calculatingSaid || now - calculatingSince < 20f) return;
            calculatingSaid = true;
            Plugin.Log.LogWarning($"the button has said Calculating for 20 s: {problem ?? "a press waits for its count"}");
        }

        static void Grey(string label, string hint)
        {
            GUI.enabled = false;
            Widgets.Button(label, Theme.Button, Widgets.Wide(36));
            GUI.enabled = true;
            if (hint != null) Widgets.Hint(hint);
        }

        /// <summary>The verb's work. Refit and Apply move the texts the sign holds (SignFit.Refit); a Refit of a known
        /// item writes its recipe back with the new light, so the sign stays editable (Mark cleared it). The new first
        /// sign is selected as the same content: the view and the rows stay.</summary>
        public static void Perform(SignLinkWindow w, Source src, Verb v)
        {
            Session s = w.Session;
            if (v == Verb.Place || v == Verb.Put || v == Verb.Update ||
                (v == Verb.Refit && s.Chosen.Recipe.Kind == SignRecipe.Kinds.Art))
            {
                // a piece is put again from the library (a few KB) instead of its old texts moved, so a moved or relit
                // piece is the shipped one: one put before 0.20.145 got its layers' anchors then (0.20.146, the author:
                // at 0.25 there is a position where it is still buggy)
                src.Act(s, w, v == Verb.Refit ? Verb.Update : v);
                return;
            }
            Fit to = s.Chosen.Fit;
            GlowMode light = s.Chosen.Light;
            // an Apply moves what the sign holds: a known item keeps its recipe, as 3 Place's own Apply does (0.20.148,
            // review: a nudge with another source showing, or Undo changes there, left a piece or a picture without it)
            SignRecipe recipe = v == Verb.Refit ? s.Chosen.Recipe.Copy()
                : s.Held.Recipe.Kind != SignRecipe.Kinds.None ? s.Held.Recipe.Copy() : null;
            Sign target = s.Target;
            w.Act(() =>
            {
                List<Sign> made = SignFit.Refit(target, to, light, out Fit got);
                if (recipe != null && made.Count > 0)
                {
                    if (recipe.Kind == SignRecipe.Kinds.Picture && light != GlowMode.AsMade) recipe.Glow = light == GlowMode.Glowing;
                    if (recipe.Kind == SignRecipe.Kinds.Art && light != GlowMode.AsMade) recipe.Light = light;
                    SignMarks.SetSource(made[0], recipe.ToString());
                }
                s.Chosen.Fit = got;
                if (made.Count > 0) w.Select(made[0], sameContent: true);
                string how = got.Describe(light);
                return how.Length > 0 ? $"now {how.Substring(2)}." : "back as made.";
            });
        }
    }
}
