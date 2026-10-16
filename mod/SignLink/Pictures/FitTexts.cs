using System;
using System.Collections.Generic;

namespace SignLink.Pictures
{
    /// <summary>The texts a fit writes, worked out without a sign (0.20.129): SignFit.Put and SignFit.Refit write them, and
    /// the live preview (UI.ContentPreview) shows the very same before the button (the author: show the end result
    /// instead of the half-done old picture).</summary>
    internal static class FitTexts
    {
        internal sealed class Result
        {
            /// <summary>The texts as written; Refit: null when they stay as they are.</summary>
            public IList<string> Texts;
            /// <summary>The fit they get: a size TMP cannot take exactly snapped (TextTransform.Snap); texts SignLink cannot
            /// resize or move as made, only posed.</summary>
            public Fit Fit;
            /// <summary>Where their middle sits as made (wu), and whether that is known.</summary>
            public double C0x, C0y;
            public bool Known;
            /// <summary>The texts were sized and moved for Fit from C0 (TextTransform), not only posed: another Rotate moves
            /// them (Fit.Offset), which the live preview follows while a slider is held (0.20.153).</summary>
            public bool Movable;
        }

        /// <summary>Texts made with their middle at c0 in a fit (SignFit.Put): sized and moved (TextTransform); centred:
        /// Right and Up from the sign's middle (0.20.38), and from then on made at the middle.</summary>
        public static Result ForPut(IList<string> texts, double c0x, double c0y, bool known, Fit fit, bool centred)
        {
            if (!TextTransform.Supports(texts, out _))
                return new Result { Texts = texts, Fit = fit.PoseOnly() };  // as made, turned around the sign's centre
            double k = TextTransform.Snap(texts, fit.Scale);
            fit.Scale = k > 0 ? k : 1;
            double dx, dy;
            if (centred && known)
            {
                fit.OffsetCentred(c0x, c0y, out dx, out dy);
                c0x = c0y = 0;
            }
            else fit.Offset(c0x, c0y, out dx, out dy);
            if (Math.Abs(fit.Scale - 1) > 1e-9 || dx != 0 || dy != 0) texts = TextTransform.Apply(texts, fit.Scale, dx, dy);
            return new Result { Texts = texts, Fit = fit, C0x = c0x, C0y = c0y, Known = known, Movable = true };
        }

        /// <summary>What a stack holds, made with its middle at c0 and fitted `from`, given the fit `to` and a light
        /// (AsMade: as it is) (SignFit.Refit): from one fit to the other (Fit.Between), snapped; texts SignLink cannot
        /// resize or move keep their place.</summary>
        public static Result ForRefit(IList<string> texts, Fit from, double c0x, double c0y, bool known, Fit to, GlowMode light)
        {
            double k = 1, dx = 0, dy = 0;
            bool movable = TextTransform.Supports(texts, out _);
            if (!movable)
            {
                to.Scale = from.Scale;
                to.Right = from.Right;
                to.Up = from.Up;
                to.Out = from.Out;
                c0x = c0y = 0;
                known = false;
            }
            else
            {
                Fit.Between(from, to, c0x, c0y, out k, out dx, out dy);
                double snapped = TextTransform.Snap(texts, k);
                if (Math.Abs(snapped - k) > 1e-9)
                {
                    to.Scale = from.Scale * (snapped > 0 ? snapped : 1);    // no exact line height that size: keep it
                    Fit.Between(from, to, c0x, c0y, out k, out dx, out dy);
                }
            }
            bool move = Math.Abs(k - 1) > 1e-9 || Math.Abs(dx) > 0.005 || Math.Abs(dy) > 0.005, relight = light != GlowMode.AsMade;
            IList<string> next = texts;
            if (move) next = TextTransform.Apply(next, k, dx, dy);
            if (relight) next = SignGlow.Apply(next, light);
            return new Result { Texts = move || relight ? next : null, Fit = to, C0x = c0x, C0y = c0y, Known = known, Movable = movable };
        }
    }
}
