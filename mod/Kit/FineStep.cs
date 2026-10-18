using System;

namespace PetyusPolisz.Kit
{
    /// <summary>Shift + mouse wheel over a slider's row (Widgets.Number, 0.20.93, the author: while the mouse hovers a
    /// slider in the window, Shift + wheel should turn it finely; the plain wheel stays as it was): a notch moves the
    /// value one fine step, a fifth of the slider's snap but no finer than the row shows; a value off that grid (typed)
    /// goes to the next grid point first. No Unity here (mod/SignLink.Tests).</summary>
    internal static class FineStep
    {
        /// <summary>The fine step of a slider snapping to `step` whose value shows in `format` ("0", "0.#", "0.##").</summary>
        public static float Of(float step, string format)
        {
            int dot = format.IndexOf('.');
            int places = 0;
            if (dot >= 0)
                for (int i = dot + 1; i < format.Length && (format[i] == '0' || format[i] == '#'); i++) places++;
            double shown = Math.Pow(10, -places);
            return (float)Math.Min(step, Math.Max(step / 5.0, shown));
        }

        /// <summary>A notch on a row whose value snaps (snap: the size a value really takes, 0 for none: a picture's width
        /// to pixels TMP adds up exactly, a piece's size to exact line heights; 0.20.98, the author: Shift + wheel does not
        /// work on Content size width and height): fine steps from the size it shows until that size moves the wheel's way,
        /// then that size; the size it shows when none does before the end. Without snap, a fine step (Nudge).</summary>
        public static float Notch(float value, float wheel, float step, string format, float lo, float hi, Func<float, float> snap)
        {
            if (snap == null || wheel == 0 || float.IsNaN(wheel)) return Nudge(value, wheel, step, format, lo, hi);
            float now = snap(value);
            if (!(now > 0)) now = value;
            float v = now;
            for (int i = 0; i < 5000; i++)
            {
                float next = Nudge(v, wheel, step, format, lo, hi);
                if (next == v) break;                                       // at an end
                v = next;
                float got = snap(v);
                if (got > 0 && (wheel < 0 ? got > now + 1e-5f : got < now - 1e-5f)) return got;
            }
            return now;
        }

        /// <summary>Where a size row that snaps lands after a change (Widgets.SizeRows; snap: the size a value really takes).
        /// A slider drag or press (dragged) takes the size nearest the mouse, so the row follows the mouse both ways
        /// (2026-10-16, a player's test: Width and Height of an art piece still jumped while dragged, because the rule
        /// below ran on every mouse move: the row raced a size ahead of the mouse, and the next move, now behind it, sent
        /// it back). Anything else (a typed size) takes the size it snaps to, or the next one the change's way when that
        /// lands where the row is or against the change (0.20.149: a typed size did not move).</summary>
        public static float Settle(float was, float got, Func<float, float> snap, bool dragged, float step, float lo, float hi)
        {
            if (snap == null || Math.Abs(got - was) < 1e-4f) return got;
            float here = snap(was), to = snap(got);
            if (!(here > 0) || !(to > 0)) return got;                       // no exact sizes (several line heights): as asked
            int dir = Math.Sign(got - was);
            if (!dragged && Math.Sign(to - here) != dir) to = Notch(here, -dir, step, "0.#", lo, hi, snap);
            return Math.Max(lo, Math.Min(hi, to));
        }

        /// <summary>The value a notch moves it to (wheel: the event's delta, below zero the wheel up), within [lo, hi].</summary>
        public static float Nudge(float value, float wheel, float step, string format, float lo, float hi)
        {
            if (wheel == 0 || float.IsNaN(wheel)) return value;
            double fine = Of(step, format);
            double q = value / fine;
            double n = wheel < 0 ? Math.Floor(q + 1e-3) + 1 : Math.Ceiling(q - 1e-3) - 1;
            double v = Math.Round(n * fine, 6);
            return (float)Math.Max(lo, Math.Min(hi, v));
        }
    }
}
