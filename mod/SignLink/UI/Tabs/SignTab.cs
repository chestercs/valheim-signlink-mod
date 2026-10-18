using System;
using System.Collections.Generic;
using System.IO;
using SignLink.Animation;
using SignLink.Pictures;
using SignLink.Presets;
using SignLink.Signs;
using UnityEngine;
using PetyusPolisz.Kit;

namespace SignLink.UI
{
    /// <summary>1 Sign: the selected sign with the copies stacked on it, as one thing: what it holds (its quick view and
    /// the facts, from Session) and the actions on the whole stack: Clear, Copy, Paste, Delete.
    /// What goes on it is 2 Content; how it sits, and where the sign itself stands, 3 Place.</summary>
    internal sealed class SignTab : Tab
    {
        public SignTab(SignLinkWindow w, ContentTab content) : base(w) { }

        public override string Title => "1 Sign";
        public override bool UsesTarget => true;

        public override void Body()
        {
            Session s = Window.Session;
            bool has = s.HasTarget;
            Sign target = has ? s.Target : null;

            // folds with its quick view, as the other quick views do (0.20.171, the author)
            if (Widgets.Foldout("sign.holds", "What the sign holds"))
            using (Widgets.Horizontal(Theme.Section))
            {
                using (Widgets.Vertical(Widgets.W(230)))
                {
                    string info = Plain(s.ViewInfo);                        // it quotes the sign's words: no markup from the world
                    // the content alone, no words about it (0.20.174, the author): the picture, or the sign's words
                    if (has && s.View.Has) s.View.Draw(220, 160);
                    else GUILayout.Label(has ? info : "", Theme.ViewText, Widgets.W(220), Widgets.H(160));
                }
                using (Widgets.Vertical())
                {
                    float fw = Widgets.ContentWidth - 150 - 240;
                    if (!has) GUILayout.Label("No sign", Theme.Label);
                    else
                    {
                        int n = s.Stack.Count;
                        Widgets.Field("Holds", s.Held.Empty ? "nothing" : s.Name.Length > 0 ? s.Name
                            : n > 1 ? "a picture or glyph art" : "text or a small picture", width: fw);
                        Widgets.Field("Made with", s.MadeWith, width: fw);
                        if (n > 1)
                            Widgets.Field("Signs", $"{n}, stacked", width: fw);
                        Widgets.Field("Fit", FitWords(s.Held.Fit, s.NaturalM), width: fw);
                        Widgets.Field("World save", SaveCost.Label(s.SaveBytes) + "", width: fw);
                        // the multiplayer facts in one row (0.20.52, the author: the Download row was too much; the load time goes
                        // beside the rest): the limit, then how long every player who arrives waits for it (download and build)
                        bool tooBig = s.Largest > SignText.NetworkLimit, overLimit = !SaveCost.Fits(s.SaveBytes, true);
                        double wait = SaveCost.WaitSeconds(s.GlyphsKnown ? SaveCost.LoadSeconds(s.Glyphs) : 0, s.SaveBytes);
                        int level = s.Held.Empty || !s.GlyphsKnown ? 0 : SaveCost.Level(wait);
                        string loads = s.Held.Empty ? "" : s.GlyphsKnown ? $" · {wait:0.#} s to load" : " · counting ...";
                        Widgets.Field("Multiplayer", (tooBig ? $"too big: {SaveCost.Label(s.Largest)} on a sign"
                            : overLimit ? $"over {SaveCost.Label(SaveCost.SignLimit)}"
                            : "fits") + loads,
                            tooBig || overLimit || level == 2 ? Theme.Warn : level == 1 ? Theme.Notice : Theme.Good, fw);
                        if (s.HasAnim) Widgets.Field("Animation", $"{s.Frames} frames, {s.Fps:0.#} fps", width: fw);
                    }
                }
            }

            Actions(has, target, s);
        }

        /// <summary>The quick view's words without '&lt;', '&gt;' and control characters (0.20.132, review): "It reads" quotes
        /// the sign's text (TextReadback.Words drops tags in one pass, so nested brackets leave one behind), and the window's
        /// labels read rich text. As SignRecipe.Clean, without its 80-character cut (the words run to 140).</summary>
        static string Plain(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            if (ReferenceEquals(s, plainOf)) return plain;                  // the same words: made once, not every event
            var sb = new System.Text.StringBuilder(s.Length);
            foreach (char c in s)
                if (c != '<' && c != '>' && c >= ' ' && c != '\u007f') sb.Append(c);
            plainOf = s;
            return plain = sb.ToString();
        }

        static string plainOf, plain;

        /// <summary>"2 x 1.5 m, 1 m right, lying face up" when the size is known, else "at 150 %, ..." or "as made".</summary>
        static string FitWords(Fit f, Vector2 naturalM)
        {
            if (naturalM.x > 0 && naturalM.y > 0)
            {
                string rest = new Fit { Scale = 1, Right = f.Right, Up = f.Up, Out = f.Out, Turn = f.Turn, Tilt = f.Tilt }.Describe();
                return $"{f.Scale * naturalM.x:0.##} x {f.Scale * naturalM.y:0.##} m{rest}";
            }
            string how = f.Describe();
            return how.Length > 0 ? how.Substring(2) : "as made";
        }

        /// <summary>Clear, Copy, Paste and Delete for the whole stack (SignBuilder.SetContent: as many signs as the content
        /// needs; Clear leaves one empty sign). What cannot be undone asks twice (Armed).</summary>
        void Actions(bool has, Sign target, Session s)
        {
            Widgets.Header("Actions");
            using (Widgets.Vertical(Theme.Section))
            {
                // one action a row, its button and what it does beside it (0.20.65, the author: the buttons one under the
                // other, each with its explanation on the same row ... pleasant, airy, readable)
                bool editable = has && s.CanEdit;
                string id = has ? SignFinder.Id(target) ?? "" : "";
                const float bw = 150f, bh = 34f;
                float tw = Widgets.ContentWidth - 30 - bw - 20;
                using (Widgets.Horizontal())
                {
                    GUI.enabled = editable && !s.Held.Empty;
                    if (Armed.Button(Window, "sign.clear|" + id, "Clear", "Sure? Click again", Theme.Button, Widgets.W(bw), Widgets.H(bh)))
                    {
                        int copies = s.Stack.Count - 1;
                        Window.Act(() =>
                        {
                            SignBuilder.SetContent(target, new[] { "" });
                            return copies <= 0 ? "the sign is empty." : $"the sign is empty, and its {copies} stacked copies are gone.";
                        });
                    }
                    GUI.enabled = true;
                    GUILayout.Label("Clears the sign's content", Theme.SideNote, Widgets.W(tw), Widgets.H(bh));
                }
                Widgets.Space(6);
                using (Widgets.Horizontal())
                {
                    GUI.enabled = has && !s.Held.Empty && !s.Hidden;
                    if (Widgets.Button("Copy", Theme.Button, Widgets.W(bw), Widgets.H(bh)))
                        Window.Act(() =>
                        {
                            SignClip clip = Copy(target);
                            GUIUtility.systemCopyBuffer = clip.Pack();
                            if (clip.Anim != null) return "copied the moving piece: Paste puts it on another sign, moving.";
                            return clip.Texts.Count > 1 ? $"copied the picture ({clip.Texts.Count} signs): Paste puts it on another sign."
                                                        : "copied the sign's text: Paste puts it on another sign.";
                        });
                    GUI.enabled = true;
                    GUILayout.Label("Copies all it holds, moving art and GIFs too", Theme.SideNote,
                                    Widgets.W(tw), Widgets.H(bh));
                }
                Widgets.Space(6);
                using (Widgets.Horizontal())
                {
                    GUI.enabled = editable;
                    bool paste = s.Held.Empty ? Widgets.Button("Paste", Theme.Button, Widgets.W(bw), Widgets.H(bh))
                                              : Armed.Button(Window, "sign.paste|" + id, "Paste", "Sure? Click again", Theme.Button, Widgets.W(bw), Widgets.H(bh));
                    if (paste)
                        Window.Act(() =>
                        {
                            string clip = GUIUtility.systemCopyBuffer ?? "";
                            if (clip.Trim().Length == 0) throw new InvalidOperationException("the clipboard holds no text (Copy a sign first)");
#if SIGNLINK_DEVAPI
                            // the API password (Settings › Copy) is not written where every player reads it (0.20.132, review)
                            if (Api.Password.In(clip, PluginConfig.ApiPassword.Value))
                                throw new InvalidOperationException("the clipboard holds the API password: it is not pasted onto a sign, where every player " +
                                                                    "could read it (Copy a sign first)");
#endif
                            SignClip got = SignClip.Unpack(clip);
                            int n = Paste(target, got, out bool moving);    // in a shared world: 512 KiB a sign, the 300 / 600 KB limits
                            if (moving) return "the moving piece is on the sign.";
                            if (got.Anim != null && got.Still) return "the piece is on the sign, not moving (Playing › Moving starts it).";
                            return n > 1 ? $"a picture over {n} signs is on the sign." : "the text is on the sign.";
                        });
                    GUI.enabled = true;
                    GUILayout.Label("Pastes a copied sign's content onto this sign", Theme.SideNote,
                                    Widgets.W(tw), Widgets.H(bh));
                }
                Widgets.Space(6);
                using (Widgets.Horizontal())
                {
                    GUI.enabled = editable;
                    if (Armed.Button(Window, "sign.delete|" + id, "Delete", "Sure? Click again", Theme.Button, Widgets.W(bw), Widgets.H(bh)))
                        Window.Act(() =>
                        {
                            SignBuilder.Guard();
                            SignFinder.RequireLoaded(target);
                            if (Location.IsInsideNoBuildLocation(target.transform.position))   // as the hammer (0.20.151, review)
                                throw new InvalidOperationException("signs here cannot be removed: a place where nothing may be built or removed");
                            int n = SignBuilder.Delete(SignFinder.Stack(target));
                            return n > 1 ? $"deleted the sign and the {n - 1} copies stacked on it (nothing drops)." : "deleted the sign (nothing drops).";
                        });
                    GUI.enabled = true;
                    GUILayout.Label("Deletes the sign completely", Theme.SideNote, Widgets.W(tw), Widgets.H(bh));
                }
                GUI.enabled = true;

            }
        }

        /// <summary>What Copy takes (0.20.49): every sign's text, and for content SignLink made (a picture, a piece)
        /// what made it and how it sits, so the pasted sign edits as this one does (a plain text copies as itself). A
        /// moving piece takes its saved frames along (0.20.178, the author: pasted back it stood still): its texts alone
        /// were its first frame.</summary>
        static SignClip Copy(Sign target)
        {
            Sign root = SignFinder.Root(target);
            if (!root) throw new InvalidOperationException("the sign is gone (unloaded or removed)");
            if (!root.m_isViewable)                                         // the game hides it here: not copied out of it (0.20.151)
                throw new InvalidOperationException("the game hides this sign's content here (a blocked or muted author, or user content off)");
            var clip = new SignClip();
            clip.Texts.AddRange(SignBuilder.Content(root));
            SignRecipe r = SignRecipe.Parse(SignMarks.Source(root));
            byte[] frames = StoredAnims.Blob(root);
            if (frames != null && frames.Length > 0 && StoredAnims.TryUnpack(frames, out _, out _))
            {
                clip.Anim = frames;
                clip.Still = StoredAnims.Still(SignFinder.Zdo(root));      // Moving off on Playing goes with it
            }
            if (r.Kind == SignRecipe.Kinds.Picture || r.Kind == SignRecipe.Kinds.Art)
            {
                clip.Recipe = r.ToString();
                clip.Name = SignMarks.Name(root);
                if (SignFit.Stored(root))
                {
                    clip.Fit = SignFit.Read(root, out clip.C0x, out clip.C0y, out clip.C0Known);
                    clip.HasFit = true;
                }
            }
            return clip;
        }

        /// <summary>Paste: the texts on the sign's stack, then what made them and how they sit, the fit moved to this
        /// sign's pose (Fit.Repose: the content shows where it did on the copied sign's face). Returns the signs.</summary>
        static int Paste(Sign target, SignClip clip, out bool moving)
        {
            moving = false;
            string name = clip.Name.Length > 120 ? clip.Name.Substring(0, 120) : clip.Name;
            List<Sign> made;
            if (clip.Anim != null && StoredAnims.TryUnpack(clip.Anim, out string[] frames, out float fps))
            {
                Plugin.Stored.Store(target, frames, fps, name.Length > 0 ? name : null);   // the frames saved again, the limits checked
                moving = true;
                made = SignFinder.Stack(target);
                if (made.Count == 0) made.Add(target);
            }
            else made = SignBuilder.SetContent(target, clip.Texts, name.Length > 0 ? name : null);
            Sign root = made[0];
            if (moving && clip.Still)                                       // switched off on Playing: off on the copy too
            {
                Plugin.Stored.SetMoving(root, false);
                moving = false;
            }
            if (clip.Texts.TrueForAll(t => string.IsNullOrEmpty(t))) return made.Count;   // nothing on it: nothing made it
            SignRecipe r = SignRecipe.Parse(clip.Recipe);
            if (r.Kind == SignRecipe.Kinds.Picture || r.Kind == SignRecipe.Kinds.Art)
                SignMarks.SetSource(root, r.ToString());
            if (clip.HasFit)
            {
                SignPose.Read(root, out float tilt, out float turn, out bool edge);
                SignFit.Keep(root, Fit.Repose(clip.Fit, turn, tilt, edge, clip.C0x, clip.C0y), clip.C0x, clip.C0y, clip.C0Known);
            }
            return made.Count;
        }

        /// <summary>The way on (2 Content), and a stop button while a tool's animation plays on the stack (only this computer
        /// shows it). A GIF's Pause went (0.20.117, the author: this Pause GIF on 1 Sign is not needed, take it out): it is on the
        /// Playing tab, with the saved animations.</summary>
        public override void Footer()
        {
            Session s = Window.Session;
            bool has = s.HasTarget;
            using (Widgets.Horizontal())
            {
                GUI.enabled = has;
                // "Next", the clearest word (1.0.2, the author: a friend new to the window did not understand what to do;
                // the lines that told what the next tab does read as a lure)
                if (Widgets.NextButton("Next ▸"))
                    Window.Later(() => Window.ShowTab("2 Content"));
                GUI.enabled = true;
                if (has && s.Tools > 0)
                {
                    if (Widgets.Button("Stop the tool's animation", Theme.Button, Widgets.H(36), Widgets.W(250)))
                    {
                        var mine = new List<Sign>(s.Stack);
                        Window.Act(() =>
                        {
                            int n = 0;
                            foreach (Sign x in mine) n += Plugin.Text.Stop(x);
                            return n > 0 ? "stopped: the sign shows its own text again." : "nothing was playing.";
                        });
                    }
                }
            }
        }
    }
}
