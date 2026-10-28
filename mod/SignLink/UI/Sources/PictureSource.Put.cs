using System;
using System.Collections.Generic;
using System.IO;
using SignLink.Pictures;
using SignLink.Signs;
using UnityEngine;
using PetyusPolisz.Kit;

namespace SignLink.UI.Sources
{
    /// <summary>What stops the put (limits, counts), Multiplayer max, and the put itself.</summary>
    internal partial class PictureSource
    {
        public override string Problem(Session s) => Why(out _);

        /// <summary>Still reading or counting: Problem's waiting reasons, or a press waiting for its last count (the button
        /// says "Calculating ...", 0.20.134).</summary>
        public override bool Calculating(Session s) => pendingAct != null || (Why(out bool waiting) != null && waiting);

        /// <summary>Why the button is grey, or null; waiting: only because something is still read or counted.</summary>
        string Why(out bool waiting)
        {
            waiting = false;
            if (fromSign)
            {
                if (signError != null) return Lang.F("The sign's picture cannot be read back: {0}", signError);
                if (!known) return Waiting(Lang.T("Reading the picture from the sign ..."), out waiting);
                if (job.Busy) return Lang.T("Working ...");
                if (GridFailed) return Lang.F("These settings do not work: {0}", gridError);
                if (counted && costError != null) return Lang.F("These settings do not work: {0}", costError);
                if (!counted && !Estimated) return Waiting(Lang.T("Counting what it costs ..."), out waiting);
                return null;
            }
            if (selected < 0) return Lang.T("Pick a picture above");
            if (header == null) return Waiting(Lang.T("Reading the folder ..."), out waiting);
            if (header.Error != null) return Lang.T(header.Error);          // Unreadable
            if (!known) return Lang.T(Unreadable);
            if (readErrorKey != null && readErrorKey == decodeKey) return readError;
            if (job.Busy) return Lang.T("Working ...");
            if (!decoded) return Waiting(Lang.T("Reading the picture ..."), out waiting);
            if (GridFailed) return Lang.F("These settings do not work: {0}", gridError);
            if (counted && costError != null) return Lang.F("These settings do not work: {0}", costError);
            if (!counted && !Estimated) return Waiting(Lang.T("Counting what it costs ..."), out waiting);   // the red level needs its bytes (download time)
            return null;
        }

        static string Waiting(string why, out bool waiting)
        {
            waiting = true;
            return why;
        }

        /// <summary>A picture is made centred; the sign's own one whose box could not be padded sits off by its shift
        /// (ApplyFit), so the frame goes there too.</summary>
        public override bool Centre(Session s, out double x, out double y)
        {
            x = y = 0;
            if (fromSign && signRead != null)
            {
                double k = Math.Max(1e-6, s.Chosen.Fit.Scale);
                x = signRead.ShiftR / k;
                y = signRead.ShiftU / k;
            }
            return true;
        }

        public override string What(Session s) => Lang.F("This picture ({0} x {1} pixels)", plan.W, plan.H);

        /// <summary>The live preview (0.20.129): the quick view's texture, the very grid Place encodes, at the plan's size
        /// and the chosen place (stretched while a new size's grid is drawn); lit by the scene when its light is Lit and no
        /// pixel is see-through (the screens' lit material has no alpha), else glowing.</summary>
        public override PreviewContent Preview(Session s)
        {
            if (!known || !thumb.Has || thumbFile != file || plan.W <= 0 || plan.H <= 0) return null;
            if (!Centre(s, out double x, out double y)) x = y = 0;
            bool solid = cost != null && costError == null && cost.File == file && cost.Glyphs >= cost.Pixels;
            return new PreviewContent { Picture = thumb.Texture, Lit = !o.Glow && solid, Wwu = plan.W * (double)plan.Pitch,
                                        Hwu = plan.H * (double)plan.Pitch, C0x = x, C0y = y, Fit = s.Chosen.Fit, Key = "picture|" + file };
        }

        public override string Lighter(Session s) => Lang.T("Lighter: lower the Resolution");

        // the Max button: the most Resolution whose sign texts stay within maxTarget bytes, found from the counted bytes
        int maxSteps, maxLastW = -1;
        /// <summary>Max was pressed (not a new pick's own start), and it found nothing bigger that fits: said under its
        /// row until the Resolution changes (0.20.193, test: a press that changed nothing said nothing).</summary>
        bool maxPressed, maxNothing;
        int maxNothingW = -1;                                               // the pixels it was said for: another Resolution, file or sign hides it
        long maxTarget;

        /// <summary>What Max aims at: 300 KB (SaveCost.SignLimit), and in multiplayer no more than the area has room for
        /// with this content in place of the sign's.</summary>
        static long MaxTarget(Session s) =>
            SignText.Shared() && s.HasTarget ? Math.Min(SaveCost.SignLimit, Math.Max(0, SaveCost.AreaLimit - s.AreaOthers)) : SaveCost.SignLimit;

        /// <summary>One step of Max (PictureOptions.MaxStep), each time the bytes of the Resolution set last are counted;
        /// it stops when it fits and a bigger one would not, after six counts, or on an error.</summary>
        bool StepMax()
        {
            if (maxSteps <= 0 || !known || !counted) return false;
            if (!Have)
            {
                maxSteps = 0;
                return false;
            }
            // the pixels did not move with the last step: this size takes no more (0.20.189, review: Max set an uncounted
            // Resolution there, six times the same)
            bool still = plan.W == maxLastW;
            if (still || PictureOptions.MaxStep(plan.W, cost.Bytes, maxTarget, MostDetail(header), out int next))
            {
                maxNothing = maxPressed && (still || maxLastW == -1);       // no step moved the pixels
                maxNothingW = plan.W;
                maxPressed = false;
                maxSteps = 0;                                               // the most that fits
                return false;
            }
            maxLastW = plan.W;
            o.Detail = next;
            o.DetailAuto = false;
            maxSteps--;
            return true;                                                    // a new Resolution: counted again
        }

        /// <summary>The most Detail a picture can have (PictureOptions.MaxDetail: its own pixels, within MaxPixels).</summary>
        static int MostDetail(Header h) => PictureOptions.MaxDetail(h.W, h.H);

        /// <summary>The red level (SaveCost.Heavy): a long download for every player who arrives on a server, or a sign
        /// close to the 512 KiB a player can receive.</summary>
        public override bool Heavy(Session s, out long saveBytes, out long largest)
        {
            saveBytes = SaveBytes();
            // estimated: a stack's largest sign stays about as large (MaxKb splits it), one sign grows with the picture
            largest = Have ? cost.Largest : Estimated ? (cost.Pic.Bands.Count > 1 ? Math.Min(cost.Largest, saveBytes) : saveBytes) : 0;
            return SaveCost.Heavy(saveBytes, largest);
        }

        /// <summary>Place or Update: the texts the cost line made when they are for these very settings and the file is
        /// unchanged, else the decoded picture encoded again (no second decode); a moving GIF takes its frames from a screen
        /// that plays or parked them (ScreenPlayer.FramesFor), else from its file, and without the counted texts the whole
        /// GIF goes from its file. The new first sign is selected (a new pose makes the stack's signs again).</summary>
        public override void Act(Session s, SignLinkWindow w, Verb v)
        {
            if (!s.HasTarget || file == null) return;
            if (!Have && Estimated)                                         // pressed on the estimate: once it is counted
            {
                Sign pressedOn = s.Target;
                bool asked = Heavy(s, out _, out _);                        // the red-level question was asked on the estimate
                pendingIn = w;
                pendingAct = () =>
                {
                    string why = Have ? Problem(s) ?? Limit(s) : null;
                    if (!Have || !ReferenceEquals(s.Target, pressedOn))     // another pick or sign meanwhile (0.20.130: said)
                        w.Say("error: " + Lang.T("the picture or the sign changed while it was counted: press the button again"));
                    else if (why != null) w.Say("error: " + why);
                    else if (!asked && Heavy(s, out _, out _))              // counted, it is very large: asked first, as always
                        w.Say("error: " + Lang.T("counted, it is very large for every player who arrives: press the button again to decide"));
                    else Act(s, w, v);
                };
                return;
            }
            ApplyFit(s.Chosen.Fit);
            Sign target = s.Target;
            if (fromSign)
            {
                ActFromSign(s, w, target);
                return;
            }
            string f0 = file, key = optionsKey, stamp = header?.Stamp;
            Decoded d = source;
            Costed c = Have && costFor == key ? cost : null;
            bool gif = f0.EndsWith(".gif", StringComparison.OrdinalIgnoreCase);
            job.Say = w.Say;
            Action<Sign> done = x => Placed(s, w, target, x);
            w.Later(() =>
            {
                if (stamp == null || Stamp(f0) != stamp)                    // changed on disk since it was counted: read again,
                {                                                           // not placed unseen (0.20.130, review)
                    Relist();
                    w.Say("error: " + Lang.T("the file changed on disk since it was counted; it is read again: press the button once more"));
                    return;
                }
                if (gif && c != null && c.Stamp == stamp)                   // the counted texts too (0.20.132, review: an Update
                {                                                           // decoded and encoded the GIF again)
                    int planW = c.Pic.Plan.W;                               // the sign's size: Saver's screen follows it
                    ScreenFrames frames = o.Animate ? Plugin.Screens.FramesFor(stamp, (gw, gh) => PictureImporter.ScreenWidth(gw, gh, planW)) : null;   // playing or parked: shared
                    if (!o.Animate || frames != null) job.Place(target, c.Pic.With(frames), o, done);
                    else job.StartScreen(target, f0, c.Pic, o, done);       // only the frames are decoded
                }
                else if (gif) job.Start(target, f0, o, done);
                else if (c != null && c.Stamp == stamp) job.Place(target, c.Pic, o, done);
                else if (d != null && d.File == f0 && d.Stamp == stamp) job.Start(target, f0, d.Rgba, d.W, d.H, d.SrcW, d.SrcH, o, done);
                else job.Start(target, f0, o, done);
            });
        }

        /// <summary>After a placement: the choice's fit is the one now on the sign (an Update from the sign's own pixels
        /// writes the box's shift into it; a fit made with another scale comes back at 1), so the rows do not see a
        /// change that is not there and Live Apply moves nothing back. Then the new first sign is selected.</summary>
        static void Placed(Session s, SignLinkWindow w, Sign target, Sign first)
        {
            if (!ReferenceEquals(s.Target, target))
            {
                s.Touch();                                                  // another sign by now: it stays
                return;
            }
            if (first) s.Chosen.Fit = SignFit.Read(first, out _, out _, out _);
            w.Select(first);                                                // still the sign the job was for
        }

        /// <summary>Update of the sign's own picture: the texts the cost line made from its pixels (the button waits for
        /// them), on the sign only if it is still the stack they were read from (another player may have changed it);
        /// the recipe then says how its file makes it (SignRecipeFor).</summary>
        void ActFromSign(Session s, SignLinkWindow w, Sign target)
        {
            Costed c = Have && costFor == optionsKey ? cost : null;
            if (!known || signRead == null || c == null) return;
            string readFor = signJob.Done;
            ulong hash = signRead.Hash;
            SignRecipe write = SignRecipeFor(c.Pic.Plan);
            job.Say = w.Say;
            w.Later(() =>
            {
                Sign root = SignFinder.Root(target);
                if (s.StackKey != readFor && (!root || SignPicture.TextsHash(SignBuilder.Content(root)) != hash))
                {
                    signJob.Forget();                                       // another player changed its picture: read it again
                    w.Say("error: " + Lang.T("the sign's picture changed meanwhile; it is read again: press the button once more"));
                    return;
                }
                job.Place(target, c.Pic, o, x =>
                {
                    SignMarks.SetSource(x, write.ToString());               // how its file makes it again, at the same size
                    Placed(s, w, target, x);
                });
            });
        }
    }
}
