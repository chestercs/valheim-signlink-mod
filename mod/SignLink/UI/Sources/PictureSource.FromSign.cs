using System;
using System.Collections.Generic;
using System.IO;
using SignLink.Pictures;
using SignLink.Signs;
using UnityEngine;
using PetyusPolisz.Kit;

namespace SignLink.UI.Sources
{
    /// <summary>The sign's own picture: taken into the rows when it is selected, read back from the sign when its file is not here.</summary>
    internal partial class PictureSource
    {
        /// <summary>The rows take the choice slot's recipe when the session switched slots (a sign holding a picture was
        /// selected, or one that holds none and the player's own choice is back): its settings into the options, its
        /// file picked. The player's own settings and pick are kept while a sign's own show, and come back with the
        /// own slot (a sign holding nothing, or something no source knows). PNG transparency and
        /// moving GIFs come from Settings (Tick) whatever the recipe says: a picture made otherwise shows Update.</summary>
        void Take(Session s)
        {
            maxSteps = 0;                                                   // Max was for the last choice (0.20.130, review)
            if (shownOwn && s.Editing)                                      // leaving the player's own choice: kept
            {
                ownOptions = o.Clone();
                ownFile = selected >= 0 && selected < files.Count ? files[selected] : null;
            }
            shownFor = s.Chosen;
            shownOwn = !s.Editing;
            browsing = false;                                               // a sign's own picture, or the player's pick back: the list waits
            bool restored = false;
            if (!s.Editing && ownOptions != null)                           // back to it: as it was
            {
                o = ownOptions;
                ownOptions = null;
                selected = ownFile != null ? files.IndexOf(ownFile) : -1;
                cursor = selected >= 0 ? FileRow(selected) : -1;           // the highlight follows the pick (review)
                restored = true;
            }
            SignRecipe r = s.Chosen.Recipe;
            LeaveSign();
            if (r.Kind != SignRecipe.Kinds.Picture)
            {
                if (s.Editing) selected = cursor = -1;                      // a sign holding something else: no pick of
                return;                                                     // before on it (0.20.89); the own one is kept
            }
            if (!restored)
            {
                r.Apply(o);                                                 // restored: the player's Resolution, not the plan's pixels
                checkDetail = r.Detail > 0;
            }
            int i = IndexOf(r.File);
            if (i < 0)
            {
                Relist();
                i = IndexOf(r.File);
            }
            selected = i;
            cursor = i >= 0 ? FileRow(i) : -1;                              // the highlight follows the pick (review)
            if (i < 0 && s.Editing)                                         // its file is not here: the sign's own pixels
            {
                fromSign = true;
                signHeld = r.Copy();
                signFileCols = r.Pitch > 0 ? (int)Math.Round(r.WidthM * 40 / r.Pitch) : 0;
                signError = null;
                source = null;
                decodeJob.Abandon();                                        // a file still decoding must not land here
                thumbFile = null;                                           // another sign's quick view goes at once
            }
        }

        /// <summary>Out of sign mode (another choice, a file picked from the list): a read still running is dropped,
        /// so it cannot land on the next choice's settings.</summary>
        void LeaveSign()
        {
            fromSign = false;
            keptHashSet = false;                                            // another choice: its own settings
            signJob.Abandon();
            signRead = null;
            signHeader = null;
        }

        /// <summary>The choice's pose and place into the options. The sign's own picture keeps where its canvas's middle
        /// sits from the fit's when its box could not be padded (SignPicture.Canvas), along the face as the chosen tilt
        /// lays it.</summary>
        void ApplyFit(Fit f)
        {
            o.TiltDeg = (float)f.Tilt;
            o.TurnDeg = (float)f.Turn;
            o.EdgePivot = f.Edge;
            double right = f.Right, up = f.Up, outward = f.Out;
            if (fromSign && signRead != null && (signRead.ShiftR != 0 || signRead.ShiftU != 0))
            {
                double a = f.Tilt * Math.PI / 180;
                right += signRead.ShiftR;
                up += signRead.ShiftU * Math.Cos(a);
                outward -= signRead.ShiftU * Math.Sin(a);
            }
            o.RightM = (float)(right / 40);
            o.UpM = (float)(up / 40);
            o.OutM = (float)(outward / 40);
        }

        /// <summary>The sign's picture read back once the stack is loaded (a worker), again whenever it changes: its
        /// canvas stands in for the decoded file (the grid, the cost and Update work from it), at every pixel the sign
        /// holds and the width it shows.</summary>
        void ReadSign(Session s, float now)
        {
            Sign root = s.Stack.Count > 0 ? s.Stack[0] : null;
            string key = root && s.Loaded && !s.Hidden ? s.StackKey : null;   // nothing of a text the game hides (0.20.132)
            if (signJob.Wants(key, now, 0f)) StartRead(s, root, key, now);  // the delegates only when work starts (0.20.132)
        }

        void StartRead(Session s, Sign root, string key, float now)
        {
            signJob.Want(key, now, 0f,
                () =>
                {
                    List<string> texts = SignBuilder.Content(root);
                    Fit held = s.Held.Fit;
                    SignRecipe recipe = s.Held.Recipe.Copy();
                    held.Centre(0, 0, out double fx, out double fy);         // where the fit puts a picture's middle
                    thumb.Info = Lang.T("reading the picture from the sign ...");
                    return () =>
                    {
                        if (!SignPicture.Read(texts, fx, fy, out SignPicture p, out string why)) throw new InvalidOperationException(why);
                        Fit.TurnPoint(p.ShiftX, p.ShiftY, held.Turn, out double r, out double u);
                        return new FromSign { P = p, ShiftR = r, ShiftU = u, Held = recipe, Hash = SignPicture.TextsHash(texts) };
                    };
                },
                got =>
                {
                    if (!fromSign) return;                                  // left meanwhile (LeaveSign drops it too)
                    bool same = (signRead != null && signRead.Hash == got.Hash) || (keptHashSet && keptHash == got.Hash);   // moved, not changed
                    keptHash = got.Hash;
                    keptHashSet = true;
                    if (got.Held.Kind == SignRecipe.Kinds.Picture && got.Held.SameItem(signHeld))
                    {
                        signHeld = got.Held;                                // made again since (an Update here, or another
                        signFileCols = signHeld.Pitch > 0 ? (int)Math.Round(signHeld.WidthM * 40 / signHeld.Pitch) : 0;   // player's)
                    }
                    signRead = got;
                    signError = null;
                    signHeader = new Header { W = got.P.W, H = got.P.H, Stamp = signJob.Done };
                    source = new Decoded { File = SignFile, Stamp = signJob.Done, Rgba = got.P.Rgba, W = got.P.W, H = got.P.H,
                                           SrcW = got.P.W, SrcH = got.P.H };
                    signNativeM = (float)(got.P.W * got.P.Pitch / 40);        // as it is: every pixel, the width it shows
                    if (same) return;                                       // the player's Resolution and Width stay
                    o.Detail = got.P.W;
                    o.DetailAuto = false;
                    o.WidthM = signNativeM;
                    maxSteps = 0;
                },
                e =>
                {
                    if (!fromSign) return;
                    signRead = null;
                    signHeader = null;
                    signError = e.Message;
                    thumb.Clear();
                    thumb.Info = Lang.F("no quick view: {0}", e.Message);
                });
        }

        /// <summary>The sign's picture as it is (every pixel, the width it shows, the same seamlessness and light): its
        /// own recipe, so the button says As it is (or moves it) and nothing is made again.</summary>
        bool AsOnSign() => signRead != null && plan.W == signRead.P.W && Math.Abs(o.WidthM - signNativeM) < 1e-4f &&
                           Math.Abs(o.Cover - signHeld.Cover) < 1e-4f && o.Glow == signHeld.Glow;

        /// <summary>What Update writes for the sign's picture made again from its own pixels: these settings, its file's
        /// name and how it was made (see-through, moving: whoever has the file still sees it move), the columns counted
        /// on the file's full width (SignPicture.FileColumns), so the file makes it again at the same size.</summary>
        SignRecipe SignRecipeFor(PicturePlan p)
        {
            PictureOptions made = o.Clone();
            made.Transparent = signHeld.Transparent;
            made.Animate = signHeld.Animate;
            SignRecipe r = SignRecipe.ForPicture(made, p, signHeld.File);
            r.WidthM = signRead.P.RecipeColumns(p.W, p.Pitch, signFileCols) * p.Pitch / 40f;
            return r;
        }
    }
}
