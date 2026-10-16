using System;

namespace SignLink.UI
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
