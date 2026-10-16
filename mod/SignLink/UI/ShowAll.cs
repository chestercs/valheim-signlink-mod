using System.Collections.Generic;
using SignLink.Signs;
using UnityEngine;

namespace SignLink.UI
{
    /// <summary>Show all SignLink signs (1 Sign, 0.20.87, the author: a Show all SignLink button ... would show every
    /// SignLink sign with the bordered, layered selection look, even with the window closed, until I toggle it off
    /// again): every SignLink stack loaded around the player wears the selection's frame (Ghost.ShowStack: the
    /// content's veil, the board's outline), the window open or shut, looked for once a second. While it is on, no sign
    /// is selected or picked (the author: the selected SignLink sign loses its selection, and picking is not possible
    /// either).</summary>
    internal sealed class ShowAll
    {
        readonly Dictionary<Sign, Ghost> frames = new Dictionary<Sign, Ghost>();
        float nextLook;

        /// <summary>Show all is on: kept for this game only, off when it starts and when the player leaves the world
        /// (0.20.123, the author: after a game restart, a logout and so on, forget that Show all was enabled; it always
        /// starts off; a setting in the config file before).</summary>
        public static bool On { get; private set; }

        /// <summary>The most stacks Show all frames at once (the nearest).</summary>
        const int MostFrames = 64;

        public static void Set(bool on) => On = on;

        /// <summary>Every frame (the window's Update): once a second, a frame for each SignLink stack there is, and none
        /// for those gone (unloaded, removed, made again).</summary>
        public void Step(float now)
        {
            if (On && !Game.instance) On = false;                          // left the world: it starts off next time
            if (!On || !Player.m_localPlayer)
            {
                if (frames.Count > 0) Clear();
                return;
            }
            if (now < nextLook) return;
            nextLook = now + 1f;
            var signs = new List<Sign>();
            foreach (Sign s in SignFinder.Loaded())
                if (s && SignFinder.Zdo(s) != null) signs.Add(s);
            var shown = new HashSet<Sign>();
            // the SignLink stacks within the area the limits count, the nearest MostFrames (0.20.130, review: thousands of
            // frames in a big base, each drawn through walls); only the stacks of SignLink's marked signs within reach are
            // looked for, their copies among all the signs (0.20.132, review: every loaded sign was stacked once a second):
            // each holds its marked seed, so each is SignLink's
            Vector3 me = Player.m_localPlayer.transform.position;
            float reach = Pictures.SaveCost.AreaRadiusM + 1f;                // a stack's first sign lies within centimetres of its seed
            var near = new List<KeyValuePair<float, List<Sign>>>();
            foreach (List<Sign> stack in SignFinder.Stacks(signs, s => (s.transform.position - me).sqrMagnitude <= reach * reach && SignMarks.IsSignLinks(s)))
            {
                float d = Vector3.Distance(me, stack[0].transform.position);
                if (d <= Pictures.SaveCost.AreaRadiusM) near.Add(new KeyValuePair<float, List<Sign>>(d, stack));
            }
            near.Sort((a, b) => a.Key.CompareTo(b.Key));
            for (int k = 0; k < near.Count && k < MostFrames; k++)
            {
                List<Sign> stack = near[k].Value;
                Sign first = stack[0], host = stack[stack.Count - 1];        // the frontmost copy's canvas draws last
                if (!frames.TryGetValue(first, out Ghost g)) frames[first] = g = new Ghost();
                g.ShowStack(first, host, stack);
                shown.Add(first);
            }
            var gone = new List<Sign>();
            foreach (KeyValuePair<Sign, Ghost> f in frames)
                if (!shown.Contains(f.Key)) gone.Add(f.Key);
            foreach (Sign s in gone)
            {
                frames[s].Destroy();
                frames.Remove(s);
            }
        }

        public void Clear()
        {
            foreach (Ghost g in frames.Values) g.Destroy();
            frames.Clear();
            nextLook = 0f;
        }
    }
}
