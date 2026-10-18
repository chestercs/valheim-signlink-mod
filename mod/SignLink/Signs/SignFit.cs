using System;
using System.Collections.Generic;
using SignLink.Animation;
using SignLink.Pictures;
using UnityEngine;

namespace SignLink.Signs
{
    /// <summary>How the content SignLink put on a stack sits there (Fit): its size and place as made, kept on the first
    /// sign (FitKey, CentreKey) beside the stack's pose (SignPose). Put puts content made at a known centre in a fit;
    /// Refit moves what a stack holds from the fit it has to another (the Sign tab's fine-tuner). Content that cannot be
    /// resized (tags TextTransform does not cover) keeps its size and place and turns around the sign's centre.</summary>
    internal static class SignFit
    {
        /// <summary>(scale, right wu, up wu), towards you wu (OutKey) and (centre x, centre y as made, 1 when known); zero:
        /// none. No OutKey: a fit of SignLink 0.13-0.15, whose up ran along the sign's face. FitLenKey: the first sign's
        /// text length when the fit was written (since 0.20.0): a fit holds only for that text, not for one the game's
        /// editor cut to 50 characters or a tool rewrote since.</summary>
        public static readonly int FitKey = "signlink_fit".GetStableHashCode(), CentreKey = "signlink_centre".GetStableHashCode(),
                                   OutKey = "signlink_fit_out".GetStableHashCode(), FitLenKey = "signlink_fit_len".GetStableHashCode();

        /// <summary>The stack's fit: its pose as it is, its size and place as SignLink last put them (as made when
        /// unknown, or when someone else rewrote the text since), and where the content's middle sits as made.</summary>
        public static Fit Read(Sign root, out double c0x, out double c0y, out bool known)
        {
            SignPose.Read(root, out float tilt, out float turn, out bool edge);
            ZDO zdo = SignFinder.Zdo(root);
            bool has = Kept(zdo, out Vector3 f, out float o);
            Vector3 c = Fresh(zdo) ? zdo.GetVec3(CentreKey, Vector3.zero) : Vector3.zero;
            known = c.z > 0.5f && Fit.SanePoint(c.x, c.y);                  // a middle past the bounds: unknown (0.20.132, review)
            c0x = known ? c.x : 0;
            c0y = known ? c.y : 0;
            Fit fit = has ? new Fit { Scale = f.x, Right = f.y, Up = f.z, Out = float.IsNaN(o) ? 0 : o } : Fit.AsMade;
            if (has && float.IsNaN(o))                                         // up along the tipped sign's face, as before
            {
                double a = tilt * Math.PI / 180;
                fit.Up = f.z * Math.Cos(a);
                fit.Out = -f.z * Math.Sin(a);
            }
            fit.Turn = turn;
            fit.Tilt = tilt;
            fit.Edge = edge;
            return fit;
        }

        static void Write(Sign root, Fit f, double c0x, double c0y, bool known)
        {
            ZDO zdo = SignFinder.Zdo(root);
            if (zdo == null) return;
            root.m_nview.ClaimOwnership();                                  // as Sign.SetText does: the owner's copy wins
            zdo.Set(FitKey, new Vector3((float)f.Scale, (float)f.Right, (float)f.Up));
            zdo.Set(OutKey, (float)f.Out);
            zdo.Set(CentreKey, known ? new Vector3((float)c0x, (float)c0y, 1f) : Vector3.zero);
            zdo.Set(FitLenKey, zdo.GetString(ZDOVars.s_text, "").Length);  // the text this fit describes
        }

        /// <summary>A fit SignLink kept for the first sign's text as it is (Read gives it rather than as made).</summary>
        public static bool Stored(Sign root) => Kept(SignFinder.Zdo(root), out _, out _);

        /// <summary>The fit kept on the first sign (FitKey; o: OutKey, NaN when a fit of 0.13-0.15 has none) when it is for
        /// its text as it is (FitLenKey) and its numbers are sane (Fit.Sane): any player's game can write them, and NaN,
        /// infinities or huge numbers would reach the texts and the frames (0.20.132, review: Read let +Inf through as a
        /// size). Else none: as made.</summary>
        static bool Kept(ZDO zdo, out Vector3 f, out float o)
        {
            f = Vector3.zero;
            o = float.NaN;
            if (!Fresh(zdo)) return false;
            f = zdo.GetVec3(FitKey, Vector3.zero);
            o = zdo.GetFloat(OutKey, float.NaN);
            return Fit.Sane(f.x, f.y, f.z, float.IsNaN(o) ? 0 : o);
        }

        /// <summary>The fit and middle kept on the first sign are for its text as it is (FitLenKey; none kept: they are),
        /// not for one someone else wrote since.</summary>
        static bool Fresh(ZDO zdo)
        {
            if (zdo == null) return false;
            int len = zdo.GetInt(FitLenKey, -1);
            return len < 0 || len == zdo.GetString(ZDOVars.s_text, "").Length;
        }

        /// <summary>Keeps a fit for the stack's texts as they are now (Paste: the copied sign's, in this sign's pose).</summary>
        public static void Keep(Sign root, Fit f, double c0x, double c0y, bool known) => Write(root, f, c0x, c0y, known);

        /// <summary>Puts texts made with their middle at c0 on the stack in a fit: sized and moved (TextTransform), the
        /// stack posed (SignPose), the fit kept. fps &gt; 0: the texts are an animation's frames, saved on the sign.
        /// Returns the stack, its first sign first, and the size it really got (pixel pictures take only some).</summary>
        public static List<Sign> Put(Sign target, IList<string> texts, double c0x, double c0y, bool known, Fit fit, string name,
                                     float fps = 0f, bool centred = false)
        {
            FitTexts.Result r = FitTexts.ForPut(texts, c0x, c0y, known, fit, centred);   // the very texts the preview shows (0.20.129)
            List<Sign> made = Place(target, r.Texts, fps, name, r.Fit);
            Write(made[0], r.Fit, r.C0x, r.C0y, r.Known);
            return made;
        }

        /// <summary>Texts already placed for a fit (a picture encoded at its offset, PictureOptions.Origin), made centred.</summary>
        public static List<Sign> PutPlaced(Sign target, IList<string> texts, Fit fit, string name)
        {
            List<Sign> made = SignPose.Set(target, (float)fit.Tilt, (float)fit.Turn, Shift(fit), fit.Edge, texts, name);
            Write(made[0], fit, 0, 0, true);
            return made;
        }

        /// <summary>Gives what the stack holds a new fit and light (AsMade: as it is): from the fit it has to `to`
        /// (Fit.Between), its texts or saved frames rewritten, the stack posed. Returns the stack and the fit it got.</summary>
        public static List<Sign> Refit(Sign any, Fit to, GlowMode light, out Fit got)
        {
            List<Sign> stack = SignFinder.Stack(any);
            Sign root = stack.Count > 0 ? stack[0] : any;
            Fit from = Read(root, out double c0x, out double c0y, out bool known);
            string name = SignMarks.Name(root);
            bool anim = StoredAnims.TryUnpack(StoredAnims.Blob(root), out string[] frames, out float fps);
            // Moving off (Playing) stays off: storing the frames again clears it (1.0.2, review: a reset or a resize set a
            // piece moving for everyone again)
            bool still = anim && StoredAnims.Still(SignFinder.Zdo(root));
            IList<string> texts = anim ? frames : SignBuilder.Content(root);
            FitTexts.Result r = FitTexts.ForRefit(texts, from, c0x, c0y, known, to, light);   // the very texts the preview shows (0.20.129)
            List<Sign> made = Place(root, r.Texts, anim ? fps : 0f, name, r.Fit);
            if (still) Plugin.Stored.SetMoving(made[0], false);
            Write(made[0], r.Fit, r.C0x, r.C0y, r.Known);
            got = r.Fit;
            return made;
        }

        /// <summary>New texts (or frames) and a pose for the stack: an animation's frames are saved first (one sign), then
        /// the sign posed; texts go with the pose in one rebuild (SignPose.Set).</summary>
        static List<Sign> Place(Sign target, IList<string> texts, float fps, string name, Fit fit)
        {
            float tilt = (float)fit.Tilt, turn = (float)fit.Turn, shift = Shift(fit);
            if (fps <= 0f) return SignPose.Set(target, tilt, turn, shift, fit.Edge, texts, name);
            if (texts != null)
            {
                SignPose.Check(target, tilt, turn, shift, fit.Edge);          // a pose that may not happen throws before
                                                                               // the frames change

                Plugin.Stored.Store(target, texts, fps, name);
                List<Sign> stack = SignFinder.Stack(target);
                target = stack.Count > 0 ? stack[0] : target;
            }
            return SignPose.Set(target, tilt, turn, shift, fit.Edge);
        }

        /// <summary>How far the stack moves along its normal for a fit: not at all (0.18.0). The sign only turns, as the
        /// player wants it where they built it; the picture takes the part of its place along the sign's face, and the
        /// part off the face (Fit.OnSign's normal) cannot show.</summary>
        static float Shift(Fit fit) => 0f;
    }
}
