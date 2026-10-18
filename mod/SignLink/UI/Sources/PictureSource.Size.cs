using System;
using System.Collections.Generic;
using System.IO;
using SignLink.Pictures;
using SignLink.Signs;
using UnityEngine;
using PetyusPolisz.Kit;

namespace SignLink.UI.Sources
{
    /// <summary>Width and Height in metres (3 Place › Content size), Fine Width steps, and what the sign holds to go back to.</summary>
    internal partial class PictureSource
    {
        public override bool Size(Session s, out Vector2 metres)
        {
            metres = known ? new Vector2(plan.WidthM, plan.HeightM) : Vector2.zero;
            if (known && Screened(s)) metres += Vector2.one * (2f * plan.Pitch / 40f);   // a pixel more on every side
            return known;
        }

        /// <summary>Does this picture show on a screen (a moving GIF: PictureImporter.PlayOver), a pixel larger on every
        /// side than its still? A picked GIF while moving GIFs are on; the sign's own when one plays over it now (0.20.116,
        /// the author: the frame was a pixel short of the GIF all round).</summary>
        bool Screened(Session s)
        {
            if (fromSign) return Plugin.Screens.ScreenOver(s.Stack.Count > 0 ? s.Stack : new List<Sign> { s.Target }) != null;
            return o.Animate && file != null && file.EndsWith(".gif", StringComparison.OrdinalIgnoreCase);
        }

        Func<float, float> snapWidth, snapHeight;

        /// <summary>Fine Width steps (Settings › Pictures): a picture's size is the size asked for, to half a pixel, dragged
        /// and typed too (0.20.126); the plan takes the pixels nearest the Resolution that make it (PictureOptions.Fine,
        /// 0.20.128). Off, the next size it takes for the same data (Planned).</summary>
        bool FineSteps() => PluginConfig.PictureFineWidth.Value && known && plan.W > 0 && plan.Pitch > 0;

        /// <summary>A Shift + wheel notch with Fine Width: the next size up or down, about a pixel (0.5-4 cm), the Resolution
        /// kept (0.20.128, the author: when I edit and lower the size, its density gets very bad: a pixel
        /// more or less at the same pixel size, 0.20.122-0.20.127, took the Resolution away with the size).</summary>
        void Finer(int dir)
        {
            float next = NextWidth(dir);
            if (!float.IsNaN(next)) o.WidthM = next;
        }

        /// <summary>The width Finer goes to that way, NaN at the end of the range.</summary>
        float NextWidth(int dir)
        {
            float now = plan.WidthM, px = plan.Pitch / 40f;
            // the fewest pixels' worth away that the plan takes as a new size: about one where the size is fine, the next
            // step where it steps (PictureOptions.FineSpread); doubled until it moves, then halved back to the nearest
            int lo = 0, hi = 1;
            while (hi <= 4096 && !Moves(now + dir * hi * px, now, dir))
            {
                lo = hi;
                hi *= 2;
            }
            if (hi > 4096) return float.NaN;                                // at the end of the range
            while (hi - lo > 1)
            {
                int mid = (lo + hi) / 2;
                if (Moves(now + dir * mid * px, now, dir)) hi = mid;
                else lo = mid;
            }
            return Mathf.Clamp(now + dir * hi * px, 0.25f, 60f);
        }

        /// <summary>Does a picture asked to be m metres wide take a size past now, in the direction dir?</summary>
        bool Moves(float m, float now, int dir)
        {
            PictureOptions probe = o.Clone();
            probe.WidthM = Mathf.Clamp(m, 0.25f, 60f);
            float got = probe.PlanFor(header.W, header.H).WidthM;
            return dir > 0 ? got > now + 1e-4f : got < now - 1e-4f;
        }

        /// <summary>Fine Width's cap (0.20.128, the author: as I set the size the KB grows and I cannot save it): a size
        /// may take a few pixels more than the Resolution, never so many that this picture's counts say it goes over what
        /// Max aims at (MaxTarget: the multiplayer limits, 2 % short); never under the Resolution itself (the player's;
        /// over the limit, Limit says so). 0 before a count. Kept for the picture, its Resolution and the aim: each count
        /// is taken in once and may only lower it (PictureOptions.NextCap, 0.20.134: made fresh from every count, the cap
        /// and the plan fed each other, 109, 110, 109, ... for good, and a press waiting for the last count never went on).</summary>
        int FineCapFor(Session s)
        {
            if (!o.Fine || !known || !SignText.Shared()) return 0;          // single player: no multiplayer limit (0.20.149, review)
            int own = Math.Min(o.Detail, MostDetail(header));
            long target = MaxTarget(s);
            if (target <= 0) return Math.Max(1, own);                       // the area is full: no pixel more than the Resolution
            if (capFile != file || capStamp != header.Stamp || capOwn != own || capTarget != target || capTransparent != o.Transparent ||
                capGlow != o.Glow || !Same(capCover, o.Cover))
            {
                capFile = file;                                             // another picture, Resolution or aim: found again
                capStamp = header.Stamp;
                capOwn = own;
                capTarget = target;
                capTransparent = o.Transparent;
                capGlow = o.Glow;
                capCover = o.Cover;
                fineCap = 0;
                capFrom = null;
            }
            if (cost == null || costError != null || cost.File != file || cost.Stamp != header.Stamp || ReferenceEquals(cost, capFrom))
                return fineCap;
            capFrom = cost;                                                 // each count once
            fineCap = PictureOptions.NextCap(fineCap, own, cost.Bytes, cost.Pixels, (long)(target * 0.98), header.H / (double)header.W);
            return fineCap;
        }

        // the cap as the counts made it, and what it is for (FineCapFor)
        int fineCap, capOwn;
        string capFile, capStamp;
        long capTarget;
        bool capTransparent, capGlow;
        float capCover;
        Costed capFrom;

        /// <summary>The width (high: the height) a picture asked to be m metres wide (high) really takes: its plan's, whole
        /// pixels TMP adds up exactly, so Shift + wheel on the size rows goes to the next one (0.20.98, the author: Shift +
        /// wheel does not work on Content size width and height: a centimetre snapped back to the same width).</summary>
        /// <summary>The Resolution row's snap (PictureOptions.Achievable), made once.</summary>
        Func<float, float> snapDetail;

        float Planned(float m, bool high)
        {
            if (!known || plan.W <= 0 || plan.H <= 0) return m;
            PictureOptions probe = o.Clone();
            probe.WidthM = high ? m * plan.W / plan.H : m;
            PicturePlan p = probe.PlanFor(header.W, header.H);
            return high ? p.HeightM : p.WidthM;
        }

        /// <summary>A width asked by the slider or a typed number: as asked when the plan moves that way, else the next size it
        /// takes that way (0.20.149, the author's test: between the sizes a Resolution takes, a drag or a typed 3 m snapped back
        /// to 2.54 m and the row seemed stuck).</summary>
        /// slid: the slider's way (Widgets.Slid): a drag goes the mouse's way, and to the next size only once the mouse is
        /// past halfway to it (0.20.175, the author: a slow drag flickered between two sizes).
        void SizeTo(float m, float now, bool high, int slid)
        {
            int dir = slid != 0 ? slid : Math.Sign(m - now);
            if (dir == 0 || Math.Abs(m - now) < 1e-4f) return;
            if (!known || plan.W <= 0) { o.WidthM = m; return; }
            float got = Planned(m, high);
            if (Math.Sign(got - now) == dir && Math.Abs(got - now) > 1e-4f) o.WidthM = m;
            else if (slid == 0) Finer(dir);
            else
            {
                float next = NextWidth(dir);
                if (!float.IsNaN(next) && (m - (now + Planned(next, false)) / 2f) * dir >= 0) o.WidthM = next;
            }
        }

        /// <summary>Width and Height in metres (3 Place › Content size): they only make the pixels bigger or smaller, for free.</summary>
        public override void DrawSize(Session s, SignLinkWindow w)
        {
            bool was = GUI.enabled;
            GUI.enabled = was && Source.Ready(s) && (selected >= 0 || fromSign);
            using (Widgets.Vertical(Theme.Section))
            {
                float aspect = known ? header.H / (float)header.W : 0.75f;
                float widthNow = known ? plan.WidthM : o.WidthM;            // snapped: the pixel TMP adds up exactly
                if (snapWidth == null)
                {
                    snapWidth = m => Planned(m, false);
                    snapHeight = m => Planned(m, true);
                }
                bool fine = FineSteps();                                    // the size as asked (PictureOptions.Fine), or snapped
                // the sizes these pixels can take: a pixel is PitchStep to MaxPitch wu (0.20.149, review: from 0.25 m, the
                // slider's first metres did nothing for a fine picture and a drag there snapped back), within 0.25-30 m
                // With Fine Width the low end is 0.25 m whatever the Resolution (0.20.184, the author: width and height could have
                // one global minimum, since now it changes): a size under what these pixels make takes fewer of them
                // (PlanFor, 0.20.183), and the Height row keeps the picture's own shape, not the pixels' rounding
                float wLo = 0.25f, wHi = 30f;
                if (known && plan.W > 0)
                {
                    if (!fine) wLo = Mathf.Clamp(plan.W * PictureOptions.PitchStep / 40f, 0.25f, 30f);
                    wHi = Mathf.Clamp(plan.W * PictureOptions.MaxPitch / 40f, wLo, 30f);
                }
                float k = !fine && known && plan.W > 0 && plan.H > 0 ? plan.H / (float)plan.W : aspect;
                float wm = Widgets.Number("pic.width", "Width (free)", widthNow, wLo, wHi, 0.05f, "m", snap: fine ? null : snapWidth);
                int wSlid = Widgets.Slid;
                if (Widgets.Notched != 0 && fine) Finer(Widgets.Notched);
                else if (Math.Abs(wm - widthNow) > 0.0001f) SizeTo(wm, widthNow, false, wSlid);
                float hNow = known ? plan.HeightM : o.WidthM * aspect;
                float hm = Widgets.Number("pic.height", "Height (same shape)", hNow, wLo * k, wHi * k, 0.05f, "m", snap: fine ? null : snapHeight);
                int hSlid = Widgets.Slid;
                if (Widgets.Notched != 0 && fine) Finer(Widgets.Notched);
                else if (Math.Abs(hm - hNow) > 0.0001f)                     // the width of that many pixels at that height's pixel
                    SizeTo(Mathf.Clamp(known && plan.H > 0 ? hm * plan.W / plan.H : hm / aspect, wLo, wHi), widthNow, false, hSlid);
            }
            GUI.enabled = was;
        }

        // the width asked, not the one planned: a Resolution moves the planned width by millimetres (0.20.187: Undo lit for
        // a Resolution change and did nothing)
        public override bool SizeDiffers(Session s, SignRecipe r) =>
            r.Kind == SignRecipe.Kinds.Picture && s.Chosen.Recipe.SameItem(r) && Math.Abs(o.WidthM - r.WidthM) > 1e-3f;

        public override void SizeAs(Session s, SignRecipe r)
        {
            if (SizeDiffers(s, r)) o.WidthM = r.WidthM;
        }

        public override bool Reverts(Session s)
        {
            SignRecipe h = s.Held.Recipe, c = s.Chosen.Recipe;
            if (!s.HasTarget || c.Kind != SignRecipe.Kinds.Picture) return false;
            if (h.Kind != SignRecipe.Kinds.Picture || !h.SameItem(c)) return true;   // another picture picked
            return Math.Abs(h.WidthM - c.WidthM) > 1e-4f || Math.Abs(h.Pitch - c.Pitch) > 1e-6f || Math.Abs(h.Cover - c.Cover) > 1e-4f;
        }

        /// <summary>The sign's own picture as it holds it: its recipe taken again (Take: its file, or the sign itself), or
        /// none picked when the sign holds something else.</summary>
        public override void Revert(Session s)
        {
            if (s.Held.Recipe.Kind == SignRecipe.Kinds.Picture)
            {
                s.Chosen.Recipe = s.Held.Recipe.Copy();
                shownFor = null;                                            // Take applies it, the file or the sign's own pixels
                maxSteps = 0;
                if (fromSign && signRead != null)
                {
                    o.Detail = signRead.P.W;                                // the sign's own: every pixel, the width it shows
                    o.DetailAuto = false;
                    o.WidthM = signNativeM;
                }
            }
            else
            {
                LeaveSign();
                selected = cursor = -1;
            }
        }
    }
}
