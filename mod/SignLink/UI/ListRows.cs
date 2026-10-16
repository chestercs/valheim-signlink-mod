using System;

namespace SignLink.UI
{
    /// <summary>A long list in a scroll view drawn only where it shows (0.20.132, review: the Picture and Art lists laid out
    /// every line on every event, and every line marked its thumbnail used, so the thumbnail cache never shrank while a
    /// list was open). A full draw measures where every line lies (its top and bottom in the scroll's content, Saw at a
    /// Repaint); from then on each Layout event picks the lines in view, Spare more past each edge, and the room the others
    /// take, which the list leaves as two spaces (Above, Below). Whatever can move the lines (their number, the list, the
    /// width, the scale) brings one full draw again. The space above is checked against where its first line lands and
    /// corrected (a space's margins are not a line's). No Unity here (mod/SignLink.Tests).</summary>
    internal sealed class ListRows
    {
        /// <summary>Lines drawn past each edge of the view (a scroll between two Layout events shows no gap).</summary>
        public const int Spare = 2;

        float[] tops = new float[0], bottoms = new float[0];
        int measured = -1, count = -1;
        object list;
        float width, scale, fix;
        bool full;

        /// <summary>The lines to draw this frame, first to last (none when Last &lt; First).</summary>
        public int First { get; private set; }
        public int Last { get; private set; } = -1;
        /// <summary>The room of the lines before First and after Last.</summary>
        public float Above { get; private set; }
        public float Below { get; private set; }

        /// <summary>At a Layout event (and only there: the other events of the frame draw the same lines): what to draw for
        /// this scroll position and view height. list, width and scale: what lays the lines out (a change measures again).</summary>
        public void Plan(int lines, float scrollY, float viewH, object of, float contentWidth, float uiScale)
        {
            if (lines != count || !ReferenceEquals(of, list) || contentWidth != width || uiScale != scale)
            {
                count = lines;
                list = of;
                width = contentWidth;
                scale = uiScale;
                measured = -1;
                fix = 0f;
            }
            full = measured != count;
            if (full)
            {
                if (tops.Length < count)
                {
                    tops = new float[count];
                    bottoms = new float[count];
                }
                First = 0;
                Last = count - 1;
                Above = Below = 0f;
                return;
            }
            int first = 0;                                                  // the first line reaching into the view
            while (first < count - 1 && bottoms[first] < scrollY) first++;
            int last = first;                                               // the last one starting in it
            while (last + 1 < count && tops[last + 1] <= scrollY + viewH) last++;
            First = Math.Max(0, first - Spare);
            Last = Math.Min(count - 1, last + Spare);
            Above = First > 0 ? Math.Max(0f, tops[First] - tops[0] + fix) : 0f;
            Below = Last < count - 1 ? bottoms[count - 1] - bottoms[Last] : 0f;
        }

        /// <summary>Where line i lies in the scroll's content once every line was measured (to scroll it into view).</summary>
        public bool Span(int i, out float top, out float bottom)
        {
            top = bottom = 0f;
            if (i < 0 || i >= count || measured != count) return false;
            top = tops[i];
            bottom = bottoms[i];
            return true;
        }

        /// <summary>Scrolls so that line i shows whole in a view viewH high (nothing when it shows already).</summary>
        public void Reveal(int i, ref float scrollY, float viewH)
        {
            if (!Span(i, out float top, out float bottom)) return;
            if (top < scrollY) scrollY = top;
            else if (bottom > scrollY + viewH) scrollY = bottom - viewH;
        }

        /// <summary>At a Repaint: where drawn line i lies (its rect's top and bottom, in the scroll's content).</summary>
        public void Saw(int i, float top, float bottom)
        {
            if (i < 0 || i >= count) return;
            if (full)
            {
                tops[i] = top;
                bottoms[i] = bottom;
                if (i == count - 1) measured = count;                       // all of them: culled from the next Layout on
                return;
            }
            if (i == First && First > 0)                                    // where the full draw had it: the space's error
                fix = Math.Max(-64f, Math.Min(64f, fix - (top - tops[First])));
        }
    }
}
