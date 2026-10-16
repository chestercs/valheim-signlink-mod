using System;

namespace SignLink.Pictures
{
    /// <summary>Where the picture goes relative to its sign: above it, centred on it, below it (the height follows the
    /// picture's size), or a position typed in.</summary>
    internal enum Placement { Above, Centred, Below, Custom }

    /// <summary>The grid a picture gets: W x H pixels of Pitch wu each.</summary>
    internal struct PicturePlan
    {
        public int W, H;
        public float Pitch;
        /// <summary>The source has fewer pixels across than asked (enlarging it would add cost, not detail): the Detail
        /// model stops at its own; the width-and-pitch model takes a coarser pixel, which keeps the width.</summary>
        public bool Coarsened;
        /// <summary>The pixel budget (MaxPixels) made the picture smaller than asked.</summary>
        public bool Capped;

        public long Pixels => (long)W * H;
        public float WidthM => W * Pitch / 40f;
        public float HeightM => H * Pitch / 40f;
        /// <summary>The time a client spends building the text meshes when the picture loads (~13.5 µs per pixel,
        /// docs/limits.md).</summary>
        public float LoadSeconds => (float)(Pixels * SaveCost.GlyphSeconds);
    }

    /// <summary>How a picture goes on a sign. In the window its detail is chosen as pixels across (Detail: that sets the
    /// data) and its size in metres (width; the height follows the picture's shape), which sets how big a pixel is; with
    /// Fine Width (on by default) a size also moves the pixels a few percent (at most FineSpread) for the exact size. The
    /// API can still give a width and a pixel size (Pitch), the pixel count following from both. Its position is its
    /// centre relative to the sign's centre, seen from the front.</summary>
    internal sealed class PictureOptions
    {
        /// <summary>The pixel sizes of the width-and-pitch model (the window before 0.20.35, the API's pitch=), in wu
        /// (2.5 cm) whose half is a line height TMP reads and adds up exactly in float32; still the ladder a coarsened
        /// older recipe climbs. The Detail model takes any such pixel on the PitchStep grid (PitchFor).</summary>
        public static readonly float[] Pitches = { 0.125f, 0.25f, 0.5f, 0.75f, 1f, 1.5f, 2f };

        /// <summary>At most this many pixels per picture (about 13.5 s of mesh building when it loads).</summary>
        public const long MaxPixels = 1000000;

        /// <summary>The Detail model's pixel grid (0.20.35): a pixel is a multiple of this many wu (3.125 mm), so its half
        /// is a line height TMP reads and adds up exactly (multiples of 1/16; PixelText.ExactLineHeight), as the
        /// Pitches are; up to MaxPitch (1 m).</summary>
        public const float PitchStep = 0.125f, MaxPitch = 40f;

        /// <summary>Pixels a picture has in a multiplayer world by default (Detail 0 in the window: AutoDetail): about
        /// 220-280 KB of sign text, under SaveCost.SignLimit for most pictures.</summary>
        public const int SharedPixels = 22000;

        /// <summary>The picture's width in metres; 0: size by Pixels instead (the API's pixels=).</summary>
        public float WidthM = 6f;
        /// <summary>The Detail model (the window, 0.20.35): the picture's pixels across. It sets the data; WidthM sets how
        /// big each pixel is (PitchFor), so a bigger picture costs nothing more, except that with Fine the exact size takes
        /// the pixels nearest Detail (at most FineSpread away). 0: the width-and-Pitch model (the API's width= and
        /// pitch=).</summary>
        public int Detail;
        /// <summary>The window picks Detail itself (AutoDetail) until the player moves the row or a sign's own recipe is
        /// loaded.</summary>
        public bool DetailAuto = true;
        /// <summary>Fine Width (0.20.128, the author chose: the Resolution is yours): WidthM is the exact size asked for
        /// and the plan takes the pixels nearest Detail that make it (FineDetail), Detail itself untouched; off, Detail is
        /// the pixels and the width snaps to the sizes they take exactly.</summary>
        public bool Fine;
        /// <summary>With Fine: the most pixels across a size may take (the multiplayer limit, MostWithin), 0 none.</summary>
        public int FineCap;
        /// <summary>With Fine: the most a size may move the pixels from Detail (10 %: the data by about a fifth); a size
        /// whose exact pixels lie further apart (small pictures of many pixels, near the smallest exact pixel) keeps Detail
        /// and steps. Measured over 1-8 m (2026-10-12): the pixels move 2.5-8 % on average, but up to 50 % for 250-320
        /// across without this; with it the size steps on 0-22 % of sizes (more pixels, more steps), fine elsewhere.</summary>
        public const double FineSpread = 0.10;
        /// <summary>With Fine: a size the pixels near Detail cannot make lands at most this far from the asked one (wu: 2.5
        /// cm, half the Width row's step) before fewer pixels make it exactly (0.20.183).</summary>
        public const double StepSlack = 1.0;
        public int Pixels = 200000;
        public float Pitch = 0.5f;
        public float Cover = 2f;
        public Placement Place = Placement.Above;
        /// <summary>The picture's centre, metres right of the sign's centre (negative: left).</summary>
        public float RightM;
        /// <summary>The picture's centre, metres above the sign's centre, when Place is Custom.</summary>
        public float UpM;
        /// <summary>The picture's centre, metres towards someone who faced the sign (negative: away; Fit.Out).</summary>
        public float OutM;
        /// <summary>The API's older above=: the bottom edge this many metres above the sign (overrides Place).</summary>
        public float? BottomM;
        /// <summary>Bytes of text per sign, in KB: 400 (the window, always since 0.20.39), 16..400 from the API; a sign
        /// stays under the 512 KiB a player can receive. 0, no cap, is left to the tests.</summary>
        public float MaxKb = (float)PixelText.NetworkKb;
        /// <summary>For a GIF: also play it over the still, as a texture screen (only players with SignLink see it move;
        /// not saved).</summary>
        public bool Animate = true;
        /// <summary>Glowing pixels: the unlit font, so the picture shows its own colours day and night; off: the sign's
        /// own lit material, shaded by the scene like normal sign text (SignGlow).</summary>
        public bool Glow = true;
        /// <summary>A PNG's alpha is kept (Resampler): see-through pixels stay empty, half-transparent ones half
        /// transparent; off: alpha is dropped, as Pillow's convert("RGB") does.</summary>
        public bool Transparent = true;
        /// <summary>The sign's pose when the picture is placed (SignPose), in degrees: tipped (+ face up) and turned
        /// clockwise. The picture turns with the sign around its own centre (Origin).</summary>
        public float TiltDeg, TurnDeg;
        /// <summary>The tilt tips the sign over its bottom edge (true: the API's way, every pose before 0.20.30) or about
        /// its middle (the window's default since: it follows the choice on 3 Place).</summary>
        public bool EdgePivot = true;

        public PictureOptions Clone() => (PictureOptions)MemberwiseClone();

        /// <summary>The picture's centre above the sign's centre, in metres, for a picture this tall: above puts its bottom
        /// edge 1 m over the sign's centre (as the presets and vanilla_hq.py do), below its top edge half a metre under it
        /// (the board's edges are a quarter metre from its centre).</summary>
        public float UpFor(float heightM)
        {
            if (BottomM.HasValue) return BottomM.Value + heightM / 2;
            switch (Place)
            {
                case Placement.Above: return 1f + heightM / 2;
                case Placement.Below: return -0.5f - heightM / 2;
                case Placement.Centred: return 0f;
                default: return UpM;
            }
        }

        /// <summary>How the picture sits on its sign: made centred on it, its centre moved RightM and up (UpFor), the sign
        /// in the chosen pose (Fit).</summary>
        public Fit FitFor(PicturePlan plan) =>
            new Fit { Scale = 1, Right = RightM * 40.0, Up = UpFor(plan.HeightM) * 40.0, Out = OutM * 40.0, Turn = TurnDeg,
                    Tilt = TiltDeg, Edge = EdgePivot };

        /// <summary>The picture's centre (cx) and bottom edge in the sign's own frame, in wu, for this plan: FitFor's offset
        /// of a picture made centred (a turned sign turns it around its own centre).</summary>
        public void Origin(PicturePlan plan, out double cx, out double bottom)
        {
            FitFor(plan).Offset(0, 0, out cx, out double cy);
            bottom = cy - plan.H * plan.Pitch / 2.0;
        }

        /// <summary>The pixel (wu) that makes w pixels about widthM metres wide: the nearest multiple of PitchStep within
        /// its range whose half TMP reads exactly, so the width snaps to steps of about w x 3.125 mm.</summary>
        public static float PitchFor(float widthM, int w)
        {
            double raw = widthM * 40.0 / Math.Max(1, w);
            int kMax = (int)(MaxPitch / PitchStep);
            int k0 = (int)Math.Max(1, Math.Min(kMax, Math.Round(raw / PitchStep, MidpointRounding.AwayFromZero)));
            for (int d = 0; d <= kMax; d++)                                 // the nearest multiple whose half TMP reads exactly
            {
                int best = -1;
                foreach (int k in new[] { k0 - d, k0 + d })
                    if (k >= 1 && k <= kMax && ExactHalf(k * PitchStep) &&
                        (best < 0 || Math.Abs(k * PitchStep - raw) < Math.Abs(best * PitchStep - raw))) best = k;
                if (best > 0) return best * PitchStep;
            }
            return Pitches[0];
        }

        /// <summary>A fine size (Settings › Pictures › Fine Width, 0.20.126): the pixels across, and their pixel (wu), that
        /// make a picture about widthM metres wide with as few pixels more or less than `near` (the Resolution) as can be:
        /// the exact pixel nearest widthM over near pixels (PitchFor), then as many pixels as widthM takes at it. Within
        /// 8..most: more than most takes the next bigger exact pixel instead, fewer than 8 the next smaller (0.20.128), so
        /// the width stays within half a pixel of widthM.</summary>
        public static int FineDetail(float widthM, int near, int most, out float pitch)
        {
            int hi = Math.Max(1, most), lo = Math.Min(8, hi);
            pitch = PitchFor(widthM, Math.Max(lo, Math.Min(hi, near)));
            int want = (int)Math.Round(widthM * 40.0 / pitch, MidpointRounding.AwayFromZero);
            if (want >= lo && want <= hi) return want;
            float edge = ExactPitch(widthM * 40.0 / (want > hi ? hi : lo), want > hi);
            if (edge > 0f)
            {
                int w = (int)Math.Round(widthM * 40.0 / edge, MidpointRounding.AwayFromZero);
                if (w >= lo && w <= hi)
                {
                    pitch = edge;
                    return w;
                }
            }
            int c = Math.Max(lo, Math.Min(hi, want));                       // past the grid: the nearest pixel for that many
            pitch = PitchFor(widthM, c);
            return c;
        }

        /// <summary>The exact pixel (PitchFor's grid) next to raw wu: the smallest at least raw (up), else the largest at
        /// most raw; 0 when the grid has none there.</summary>
        static float ExactPitch(double raw, bool up)
        {
            int kMax = (int)(MaxPitch / PitchStep);
            if (up)
            {
                for (int k = Math.Max(1, (int)Math.Ceiling(raw / PitchStep - 1e-9)); k <= kMax; k++)
                    if (ExactHalf(k * PitchStep)) return k * PitchStep;
            }
            else
                for (int k = Math.Min(kMax, (int)Math.Floor(raw / PitchStep + 1e-9)); k >= 1; k--)
                    if (ExactHalf(k * PitchStep)) return k * PitchStep;
            return 0f;
        }

        /// <summary>The most pixels across whose bytes, estimated from a count (bytes for that many pixels: the data grows
        /// with the pixels), stay within target for a picture of this aspect (height over width, rounded as PlanFor rounds
        /// it); 0 when nothing was counted (0.20.128: Fine Width's cap).</summary>
        public static int MostWithin(long bytes, long pixels, long target, double aspect)
        {
            if (bytes <= 0 || pixels <= 0 || target <= 0 || aspect <= 0) return 0;
            int w = (int)Math.Min(100000, Math.Floor(Math.Sqrt(target * (double)pixels / (bytes * aspect)))) + 3;
            while (w > 1 && bytes * (double)w * Math.Max(1, (int)Math.Round(w * aspect)) / pixels > target) w--;
            return w;
        }

        /// <summary>Fine Width's cap after a count (0.20.134): the most pixels across a size may take so the data stays
        /// within target, never under own (the Resolution); the first count sets it (cap 0: none yet), a later one only
        /// lowers it, and nothing counted keeps it. Made fresh from every count before, the cap and the plan fed each other:
        /// the plan the cap made counted a little lighter or heavier per pixel, the next cap let it one pixel more or took
        /// one away, and it went 109, 110, 109, ... for good, so a press waiting for the last count never went on (the
        /// author: the bug is back, I cannot save over it because the button is grey).
        /// Lowered only, it settles.</summary>
        public static int NextCap(int cap, int own, long bytes, long pixels, long target, double aspect)
        {
            int most = MostWithin(bytes, pixels, target, aspect);
            if (most <= 0) return cap;
            int fresh = Math.Max(own, most);
            return cap > 0 ? Math.Min(cap, fresh) : fresh;
        }

        /// <summary>A pixel whose half is a line height TMP reads exactly (its digit-by-digit float reading of 0.6875,
        /// half of 1.375, is not 0.6875: about a quarter of the 1/8 multiples drift; PyMath.TmpFloat).</summary>
        static bool ExactHalf(double pitch) => PixelText.ExactLineHeight(pitch / 2) == pitch / 2;

        /// <summary>A pixel the Detail model makes (a recipe from another player's sign is checked against it).</summary>
        public static bool ValidPitch(float pitch) =>
            pitch >= PitchStep && pitch <= MaxPitch && Math.Abs(pitch / PitchStep - Math.Round(pitch / PitchStep)) < 1e-6 &&
            ExactHalf(pitch);

        /// <summary>The Detail the window starts with for a srcW x srcH picture: in a multiplayer world about
        /// SharedPixels (a multiple of 4), in single player 320 across as before (2 m at pixel size 2); never more than
        /// the picture has.</summary>
        public static int AutoDetail(int srcW, int srcH, bool shared)
        {
            if (srcW <= 0 || srcH <= 0) return shared ? 120 : 320;
            int d = shared ? Math.Max(8, (int)Math.Floor(Math.Sqrt(SharedPixels * (double)srcW / srcH)) / 4 * 4) : 320;
            return Math.Min(d, MaxDetail(srcW, srcH));
        }

        /// <summary>One step of the window's Max button: a picture w pixels wide came to `bytes`; the data grows with the
        /// pixels, so the next width is w times the square root of target/bytes, 2 % short of it, within 8..most; over the
        /// target by a rounding, a step down. True, and next = w, when it is done: it fits and a wider one would not (or
        /// the picture has no more pixels).</summary>
        public static bool MaxStep(int w, long bytes, long target, int most, out int next)
        {
            long b = Math.Max(1, bytes);
            next = Math.Max(8, Math.Min(most, (int)Math.Floor(w * Math.Sqrt(target * 0.98 / b))));
            if (b <= target && (next <= w || w >= most))
            {
                next = w;
                return true;
            }
            if (b > target && next >= w) next = Math.Max(8, w - Math.Max(1, w / 50));
            return false;
        }

        /// <summary>The most pixels across a srcW x srcH picture can have: its own, as long as the grid (its height
        /// rounded as PlanFor rounds it) stays within MaxPixels.</summary>
        public static int MaxDetail(int srcW, int srcH)
        {
            if (srcW <= 0 || srcH <= 0) return 1;
            double aspect = srcH / (double)srcW;
            int w = Math.Max(1, Math.Min(srcW, (int)Math.Floor(Math.Sqrt(MaxPixels / aspect))));
            while (w > 1 && (long)w * Math.Max(1, (int)Math.Round(w * aspect)) > MaxPixels) w--;
            return w;
        }

        /// <summary>The pixels across a Resolution of `detail` really makes at this size (PlanFor's: with Fine Width the
        /// exact size takes the pixels nearest it), taken again until they make themselves (a cap may move them twice):
        /// what 2 Content's Resolution row snaps to, so it moves only where the density does and a typed number rounds to
        /// one that is made (0.20.133, the author: the slider should skip the dead ranges where the density does not
        /// even change, and round a number typed into the field). Fine Width off: the Resolution itself.</summary>
        public int Achievable(int detail, int srcW, int srcH)
        {
            int d = Math.Max(1, detail);
            if (!(WidthM > 0) || srcW <= 0 || srcH <= 0) return d;
            PictureOptions probe = Clone();
            for (int i = 0; i < 8; i++)
            {
                probe.Detail = d;
                int w = probe.PlanFor(srcW, srcH).W;
                if (w == d) break;
                d = w;
            }
            return d;
        }

        /// <summary>The grid for a source of srcW x srcH pixels.</summary>
        public PicturePlan PlanFor(int srcW, int srcH)
        {
            var p = new PicturePlan { Pitch = Pitch };
            if (Detail > 0 && WidthM > 0 && srcW > 0 && srcH > 0)
            {
                // the Detail model: the pixels first (never more than the picture has, within MaxPixels), then their size
                double aspect = srcH / (double)srcW;
                int most = MaxDetail(srcW, srcH);
                p.Coarsened = Detail > srcW;
                p.Capped = Detail > most && most < srcW;
                p.W = Math.Max(1, Math.Min(Detail, most));
                // the cap limits the few pixels a size adds, never the Resolution itself (0.20.148: a cap counted for 160 px held
                // every Resolution typed or slid after it at 160-171)
                int top = Fine && FineCap > 0 ? Math.Max(1, Math.Min(most, Math.Max(FineCap, Detail))) : most;
                float fine = 0f;
                int fineW = Fine ? FineDetail(WidthM, p.W, top, out fine) : p.W;
                if (Fine && Math.Abs(fineW - p.W) <= Math.Max(2, p.W * FineSpread))   // the exact size with the pixels nearest Detail
                {
                    p.W = fineW;
                    p.Pitch = fine;
                }
                else
                {
                    p.W = Math.Min(p.W, top);                               // too far from Detail: its pixels, the size in steps
                    p.Pitch = PitchFor(WidthM, p.W);
                    // ... unless that step lands far from the size asked: then fewer pixels make it (0.20.183, the author:
                    // with a very high pixel size the width now moves only in very large jumps; 640 across took 2.2 m,
                    // then 3.6 m), never more, so the data only shrinks (300 KB kept)
                    if (Fine && Math.Abs(p.W * p.Pitch - WidthM * 40.0) > StepSlack)
                    {
                        float coarser = ExactPitch(WidthM * 40.0 / p.W, true);
                        int fewer = coarser > 0f ? (int)Math.Round(WidthM * 40.0 / coarser, MidpointRounding.AwayFromZero) : 0;
                        if (fewer >= Math.Min(8, p.W) && fewer <= p.W)
                        {
                            p.W = fewer;
                            p.Pitch = coarser;
                        }
                    }
                }
                p.H = Math.Max(1, (int)Math.Round(p.W * aspect));
                return p;
            }
            if (WidthM <= 0)
                Resampler.Fit(srcW, srcH, Math.Min(Pixels, (long)srcW * srcH), out p.W, out p.H);
            else
            {
                p.W = (int)Math.Round(WidthM * 40 / Pitch);
                if (p.W > srcW)                                             // a coarser pixel keeps the width
                {
                    float coarser = 0f;                                     // the ladder older recipes were made with
                    foreach (float c in Pitches)
                        if (c > Pitch && Math.Round(WidthM * 40 / c) <= srcW) { coarser = c; break; }
                    if (coarser > 0f)
                    {
                        p.Pitch = coarser;
                        p.W = (int)Math.Min(srcW, Math.Round(WidthM * 40 / coarser));
                    }
                    else                                                    // past the ladder (2 wu): any exact pixel
                    {
                        p.W = srcW;
                        p.Pitch = Math.Max(Pitch, PitchFor(WidthM, srcW));
                    }
                    p.Coarsened = true;
                }
                p.W = Math.Max(1, p.W);
                p.H = Math.Max(1, (int)Math.Round(p.W * (double)srcH / srcW));
            }
            if (p.Pixels > MaxPixels)
            {
                double k = Math.Sqrt((double)MaxPixels / p.Pixels);
                p.W = Math.Max(1, (int)(p.W * k));
                p.H = Math.Max(1, (int)(p.H * k));
                p.Capped = true;
            }
            return p;
        }
    }
}
