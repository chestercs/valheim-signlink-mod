using SignLink.Pictures;
using SignLink.Signs;
using UnityEngine;
using PetyusPolisz.Kit;

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
        long save, largest;

        /// <summary>Armed by 3 Place's Apply content changes: the window stays open after the put, as that button's puts
        /// do (0.20.191, review: it closed).</summary>
        public bool KeepsOpen { get; private set; }

        /// <summary>Arms the question for this choice with its numbers (saveBytes &lt; 0: not counted yet).</summary>
        public void Arm(SignLinkWindow w, string key, string what, long saveBytes, long largestBytes, string lighter,
                        bool keepOpen = false) =>
            w.Later(() =>
            {
                armed = key;
                KeepsOpen = keepOpen;
                this.what = what;
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
                GUILayout.Label(what == null ? Lang.T("This is very large. Put it on anyway?") : Lang.F("{0} is very large. Put it on anyway?", what), Theme.Warn);
                // the download and the size, the Summary's words (1.1.0, the author: no build seconds, a number of this
                // computer's; World save is Content size); armed only once counted (SaveCost.Heavy)
                if (save >= 0)
                {
                    GUILayout.Label(SaveCost.WaitLine(save), Theme.Label);
                    GUILayout.Label(Lang.F("Content size: {0}", SaveCost.Label(save)), Theme.Label);
                }
                if (largest > SignText.NetworkLimit)                        // single player only: in multiplayer the limits stop it first
                    GUILayout.Label(Lang.F("One sign: {0}, over 512 KB: no server", SaveCost.Label(largest)), Theme.Warn);
                else if (largest > PixelText.NetworkKb * 1024)
                    GUILayout.Label(Lang.F("One sign: {0}, near 512 KB", SaveCost.Label(largest)), Theme.Label);
                using (Widgets.Horizontal())
                {
                    GUI.enabled = w.HasTarget && problem == null;
                    ok = Widgets.Button(yes, Theme.Button, Widgets.Wide(34));
                    GUI.enabled = true;
                    if (Widgets.Button(Lang.T("Cancel"), Theme.Button, Widgets.H(34), Widgets.W(120))) w.Later(Disarm);
                }
            }
            if (problem != null) Widgets.Hint(problem);
            if (ok) w.Later(Disarm);
            return ok;
        }
    }
}
