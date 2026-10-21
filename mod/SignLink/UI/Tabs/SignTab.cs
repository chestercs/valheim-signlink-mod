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

        public override string Title => Lang.T("1 Sign");
        public override bool UsesTarget => true;

        public override void Body()
        {
            Session s = Window.Session;
            bool has = s.HasTarget;
            Sign target = has ? s.Target : null;

            // folds with its quick view, as the other quick views do (0.20.171, the author); closed while there is nothing
            // to show (no sign, an empty sign), open again once there is (1.1.0, the author)
            if (Widgets.Foldout("sign.holds", Lang.T("What the sign holds"), usable: has && !s.Held.Empty))
            using (Widgets.Horizontal(Theme.Section))
            {
                using (Widgets.Vertical(Widgets.W(244)))                   // the view's box: 220 and its frame's padding
                {
                    string info = Plain(s.ViewInfo);                        // it quotes the sign's words: no markup from the world
                    // the content alone, no words about it (0.20.174, the author): the picture, or the sign's words
                    // the same box with the picture or the words (1.1.0, the author: the quick view keeps its size)
                    if (has && s.View.Has) s.View.Draw(220, 160);
                    else Thumb.DrawBox(220, 160, null, has ? info : "", Theme.ViewText);
                }
                Widgets.Space(18);                                          // air between the view and the facts (1.1.0, the author)
                using (Widgets.Vertical())
                {
                    float fw = Widgets.ContentWidth - 150 - 262;
                    if (has)                                                // none: the bar above asks for one (1.1.0)
                    {
                        int n = s.Stack.Count;
                        Widgets.Field(Lang.T("Holds"), s.Held.Empty ? Lang.T("nothing") : s.Name.Length > 0 ? s.Name
                            : n > 1 ? Lang.T("a picture or glyph art") : Lang.T("text or a small picture"), width: fw);
                        Widgets.Field(Lang.T("Made with"), s.MadeWith, width: fw);
                        if (n > 1)
                            Widgets.Field(Lang.T("Signs"), Lang.F("{0}, stacked", n), width: fw);
                        Widgets.Field(Lang.T("Fit"), FitWords(s.Held.Fit, s.NaturalM), width: fw);
                        Widgets.Field(Lang.T("Content size"), SaveCost.Label(s.SaveBytes), width: fw);   // the Summary's name (1.1.0)
                        // the multiplayer limits, only in multiplayer, and no seconds (1.1.0, the author: as the Summary; one
                        // row since 0.20.52)
                        if (SignText.Shared())
                        {
                            bool tooBig = s.Largest > SignText.NetworkLimit, overLimit = !SaveCost.Fits(s.SaveBytes, true);
                            Widgets.Field(Lang.T("Multiplayer"), tooBig ? Lang.F("too big: {0} on a sign", SaveCost.Label(s.Largest))
                                : overLimit ? Lang.F("over {0}", SaveCost.Label(SaveCost.SignLimit)) : Lang.T("fits"),
                                tooBig || overLimit ? Theme.Warn : Theme.Good, fw);
                        }
                        if (s.HasAnim) Widgets.Field(Lang.T("Animation"), Lang.F("{0} frames, {1:0.#} fps", s.Frames, s.Fps), width: fw);
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
            return how.Length > 0 ? how.Substring(Lang.Comma.Length) : Lang.T("as made");
        }

        /// <summary>Clear, Copy, Paste and Delete for the whole stack (SignBuilder.SetContent: as many signs as the content
        /// needs; Clear leaves one empty sign). What cannot be undone asks twice (Armed).</summary>
        void Actions(bool has, Sign target, Session s)
        {
            Widgets.Header(Lang.T("Actions"));
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
                    if (Armed.Button(Window, "sign.clear|" + id, Lang.T("Clear"), Lang.T("Sure? Click again"), Theme.Button, Widgets.W(bw), Widgets.H(bh)))
                    {
                        int copies = s.Stack.Count - 1;
                        Window.Act(() =>
                        {
                            SignBuilder.SetContent(target, new[] { "" });
                            return copies <= 0 ? Lang.T("the sign is empty.") : Lang.F("the sign is empty, and its {0} stacked copies are gone.", copies);
                        });
                    }
                    GUI.enabled = true;
                    GUILayout.Label(Lang.T("Clears the sign's content"), Theme.SideNote, Widgets.W(tw), Widgets.H(bh));
                }
                Widgets.Space(6);
                using (Widgets.Horizontal())
                {
                    GUI.enabled = has && !s.Held.Empty && !s.Hidden;
                    if (Widgets.Button(Lang.T("Copy"), Theme.Button, Widgets.W(bw), Widgets.H(bh)))
                        Window.Act(() =>
                        {
                            SignClip clip = Copy(target);
                            GUIUtility.systemCopyBuffer = clip.Pack();
                            if (clip.Anim != null) return Lang.T("copied the moving piece: Paste puts it on another sign, moving.");
                            return clip.Texts.Count > 1 ? Lang.F("copied the picture ({0} signs): Paste puts it on another sign.", clip.Texts.Count)
                                                        : Lang.T("copied the sign's text: Paste puts it on another sign.");
                        });
                    GUI.enabled = true;
                    GUILayout.Label(Lang.T("Copies all it holds, moving art and GIFs too"), Theme.SideNote,
                                    Widgets.W(tw), Widgets.H(bh));
                }
                Widgets.Space(6);
                using (Widgets.Horizontal())
                {
                    GUI.enabled = editable;
                    bool paste = s.Held.Empty ? Widgets.Button(Lang.T("Paste"), Theme.Button, Widgets.W(bw), Widgets.H(bh))
                                              : Armed.Button(Window, "sign.paste|" + id, Lang.T("Paste"), Lang.T("Sure? Click again"), Theme.Button, Widgets.W(bw), Widgets.H(bh));
                    if (paste)
                        Window.Act(() =>
                        {
                            string clip = GUIUtility.systemCopyBuffer ?? "";
                            if (clip.Trim().Length == 0) throw new InvalidOperationException(Lang.T("the clipboard holds no text (Copy a sign first)"));
                            SignClip got = SignClip.Unpack(clip);
                            int n = Paste(target, got, out bool moving);    // in a shared world: 512 KiB a sign, the 300 / 600 KB limits
                            if (moving) return Lang.T("the moving piece is on the sign.");
                            if (got.Anim != null && got.Still) return Lang.T("the piece is on the sign, not moving (Playing › Moving starts it).");
                            return n > 1 ? Lang.F("a picture over {0} signs is on the sign.", n) : Lang.T("the text is on the sign.");
                        });
                    GUI.enabled = true;
                    GUILayout.Label(Lang.T("Pastes a copied sign's content onto this sign"), Theme.SideNote,
                                    Widgets.W(tw), Widgets.H(bh));
                }
                Widgets.Space(6);
                using (Widgets.Horizontal())
                {
                    GUI.enabled = editable;
                    if (Armed.Button(Window, "sign.delete|" + id, Lang.T("Delete"), Lang.T("Sure? Click again"), Theme.Button, Widgets.W(bw), Widgets.H(bh)))
                        Window.Act(() =>
                        {
                            SignBuilder.Guard();
                            // gone meanwhile (unloaded, or removed by another player): said, not a Unity error (1.1.0, review)
                            if (!target || SignFinder.Zdo(target) == null) throw new InvalidOperationException(Lang.T("the sign is gone: select it again"));
                            SignFinder.RequireLoaded(target);
                            if (Location.IsInsideNoBuildLocation(target.transform.position))   // as the hammer (0.20.151, review)
                                throw new InvalidOperationException(Lang.T("signs here cannot be removed: a place where nothing may be built or removed"));
                            int n = SignBuilder.Delete(SignFinder.Stack(target));
                            // a ward or another player's hold left them all in place: not "deleted" (1.1.0, review)
                            if (n == 0) throw new InvalidOperationException(Lang.T("nothing was deleted: a ward you have no access to protects the sign"));
                            return n > 1 ? Lang.F("deleted the sign and the {0} copies stacked on it (nothing drops).", n - 1) : Lang.T("deleted the sign (nothing drops).");
                        });
                    GUI.enabled = true;
                    GUILayout.Label(Lang.T("Deletes the sign completely"), Theme.SideNote, Widgets.W(tw), Widgets.H(bh));
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
            if (!root) throw new InvalidOperationException(Lang.T("the sign is gone (unloaded or removed)"));
            if (!root.m_isViewable)                                         // the game hides it here: not copied out of it (0.20.151)
                throw new InvalidOperationException(Lang.T("the game hides this sign's content here (a blocked or muted author, or user content off)"));
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

        public override bool HasFooter => true;

        /// <summary>The way on (2 Content). A GIF's Pause went (0.20.117, the author: this Pause GIF on 1 Sign is not needed, take it out): it is on the
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
                if (Widgets.NextButton(Lang.T("Next ▸")))
                    Window.Later(() => Window.ShowTab(Lang.T("2 Content")));   // the tab's Title, in the language shown
                GUI.enabled = true;
            }
        }
    }
}
