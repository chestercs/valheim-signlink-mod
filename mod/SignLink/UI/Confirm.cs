using SignLink.Pictures;
using SignLink.Signs;
using UnityEngine;

namespace SignLink.UI
{
    /// <summary>The second step before putting something very large on a sign (the red level of the cost line): what it
    /// costs everyone who comes near, then "anyway" or Cancel. It is asked for one choice (a key: the picture and its
    /// options, or the piece); any change to that choice puts the plain button back.</summary>
    internal sealed class Confirm
    {
        string armed;
        bool shown;

        /// <summary>Is the question up for this choice? Decided in the Layout event only: a change to the choice during a
        /// drag or a click (the key changes) swaps the question for the button, and the controls must stay the same until
        /// the next Layout.</summary>
        public bool Armed(string key)
        {
            if (Event.current == null || Event.current.type == EventType.Layout) shown = key != null && armed == key;
            return shown;
        }

        // the question's numbers as they were when it was armed: a slider dragged while it shows cannot change its
        // lines between a Layout event and the next (which would mislay the window)
        string what, lighter;
        double load;
        long save, largest;

        /// <summary>Armed by 3 Place's Apply content changes: the window stays open after the put, as that button's puts
        /// do (0.20.191, review: it closed).</summary>
        public bool KeepsOpen { get; private set; }

        /// <summary>Arms the question for this choice with its numbers (saveBytes &lt; 0: not counted yet).</summary>
        public void Arm(SignLinkWindow w, string key, string what, double loadSeconds, long saveBytes, long largestBytes, string lighter,
                        bool keepOpen = false) =>
            w.Later(() =>
            {
                armed = key;
                KeepsOpen = keepOpen;
                this.what = what;
                load = loadSeconds;
                save = saveBytes;
                largest = largestBytes;
                this.lighter = lighter;
            });

        public void Disarm() => armed = null;

        /// <summary>The question, drawn in place of the put button, from the numbers it was armed with; true once, when
        /// the player confirms. problem: what keeps it from going on now (a limit, a ward), or null: the button is
        /// greyed with it, as the put button would be.</summary>
        public bool Ask(SignLinkWindow w, string yes, string problem)
        {
            bool ok;
            using (Widgets.Vertical(Theme.Section))
            {
                // the way out first (0.20.186, the author: start by telling them "change resolution lower in content page")
                if (lighter != null) GUILayout.Label(lighter, Theme.Notice);
                GUILayout.Label($"{what ?? "This"} is very large. Put it on the sign anyway?", Theme.Warn);
                GUILayout.Label((save >= 0 ? SaveCost.WaitLine(load, save) : $"Every player who arrives here builds it for about {load:0.#} s, besides its download") +
                                ". The objects around it can arrive only after it, so a player who logs in next to it may not see the " +
                                "buildings there until then.", Theme.Label);
                if (save >= 0)
                    GUILayout.Label($"It adds {SaveCost.Label(save)} to the world save.", Theme.Label);
                if (largest > SignText.NetworkLimit)                        // single player only: in multiplayer the limits stop it first
                    GUILayout.Label($"One sign holds {SaveCost.Label(largest)}: more than the 512 KB other players can receive, so this " +
                                    "world cannot go on a server with it.", Theme.Warn);
                else if (largest > PixelText.NetworkKb * 1024)
                    GUILayout.Label($"One sign holds {SaveCost.Label(largest)}: close to the 512 KB other players can receive.", Theme.Label);
                using (Widgets.Horizontal())
                {
                    GUI.enabled = w.HasTarget && problem == null;
                    ok = Widgets.Button(yes, Theme.Button, Widgets.Wide(34));
                    GUI.enabled = true;
                    if (Widgets.Button("Cancel", Theme.Button, Widgets.H(34), Widgets.W(120))) w.Later(Disarm);
                }
            }
            if (problem != null) Widgets.Hint(problem);
            if (ok) w.Later(Disarm);
            return ok;
        }
    }
}
