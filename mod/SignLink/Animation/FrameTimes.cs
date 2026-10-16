namespace SignLink.Animation
{
    /// <summary>Frame timing for screens that play at their own frame times (a GIF's delays): the frame to show at an
    /// elapsed time, from the frames' cumulative end times. No Unity here (mod/SignLink.Tests).</summary>
    internal static class FrameTimes
    {
        /// <summary>ends[i] is the time (ms) at which frame i ends; the frame at `elapsed` is the first whose end the
        /// elapsed time has not reached, the last one for a time past the end (callers wrap with elapsed % total).</summary>
        public static int FrameAt(long[] ends, long elapsed)
        {
            int lo = 0, hi = ends.Length - 1;
            while (lo < hi)
            {
                int mid = (lo + hi) >> 1;
                if (elapsed < ends[mid]) hi = mid;
                else lo = mid + 1;
            }
            return lo;
        }

        /// <summary>The cumulative ends of frames with these delays (a delay of 10 ms or less counts as 100 ms, as
        /// browsers time it).</summary>
        public static long[] Ends(int[] delaysMs)
        {
            var ends = new long[delaysMs.Length];
            long t = 0;
            for (int i = 0; i < delaysMs.Length; i++)
            {
                t += delaysMs[i] <= 10 ? 100 : delaysMs[i];
                ends[i] = t;
            }
            return ends;
        }
    }
}
