using System.Collections.Generic;
using SignLink.Signs;
using UnityEngine;

namespace SignLink.UI
{
    /// <summary>Every sign around wears a faint grey board outline while the window is open and no sign is selected, or
    /// while Pick in view waits for a click (0.20.172, the author: with no sign selected, or while Pick in view is
    /// pressed, every sign should get a faint grey border): what a click could select shows before the click.
    /// The nearest MostBoards signs within Reach, a stack's copies left out (they stand on its first sign's board); looked
    /// for twice a second, each outline built again only when its sign moved (Ghost.ShowBoard).</summary>
    internal sealed class AllBoards
    {
        const float Reach = 30f;
        const int MostBoards = 64;
        static readonly Color Grey = new Color(0.55f, 0.57f, 0.6f, 0.28f);   // fainter than the hover's cool white

        readonly Dictionary<Sign, Ghost> boards = new Dictionary<Sign, Ghost>();
        readonly List<KeyValuePair<float, Sign>> near = new List<KeyValuePair<float, Sign>>();
        readonly HashSet<Sign> shown = new HashSet<Sign>();
        readonly List<Sign> gone = new List<Sign>();
        float nextLook;

        public void Step(float now, bool on)
        {
            if (!on || !Player.m_localPlayer)
            {
                if (boards.Count > 0) Clear();
                return;
            }
            if (now < nextLook) return;
            nextLook = now + 0.5f;
            Vector3 me = Player.m_localPlayer.transform.position;
            near.Clear();
            foreach (Sign s in SignFinder.Loaded())
            {
                ZDO zdo = s ? SignFinder.Zdo(s) : null;
                if (zdo == null || zdo.GetInt(SignBuilder.CopyKey, 0) != 0 || !s.m_textWidget) continue;
                float d = Vector3.Distance(me, s.transform.position);
                if (d <= Reach) near.Add(new KeyValuePair<float, Sign>(d, s));
            }
            near.Sort((a, b) => a.Key.CompareTo(b.Key));
            shown.Clear();
            for (int i = 0; i < near.Count && i < MostBoards; i++)
            {
                Sign s = near[i].Value;
                if (!boards.TryGetValue(s, out Ghost g)) boards[s] = g = new Ghost(Color.clear, Color.clear, Grey);
                g.ShowBoard(s);
                shown.Add(s);
            }
            gone.Clear();
            foreach (KeyValuePair<Sign, Ghost> b in boards)
                if (!shown.Contains(b.Key)) gone.Add(b.Key);
            foreach (Sign s in gone)
            {
                boards[s].Destroy();
                boards.Remove(s);
            }
        }

        public void Clear()
        {
            foreach (Ghost g in boards.Values) g.Destroy();
            boards.Clear();
            nextLook = 0f;
        }
    }
}
