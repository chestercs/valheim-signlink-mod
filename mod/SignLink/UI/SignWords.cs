using PetyusPolisz.Kit;
using UnityEngine;

namespace SignLink.UI
{
    /// <summary>Words about a sign for the window's rows (out of the kit: they know the game's signs).</summary>
    internal static class SignWords
    {
        /// <summary>"12 m away" from the local player, or "".</summary>
        public static string Distance(Sign s)
        {
            Player p = Player.m_localPlayer;
            return p && s ? Lang.F("{0:0} m away", Vector3.Distance(p.transform.position, s.transform.position)) : "";
        }
    }

    /// <summary>A row's choice names (Widgets.Choice, a strip of toggles) in the language in use: the English kept as
    /// given, the shown ones made again only when the language changes (not at every GUI event).</summary>
    internal sealed class ChoiceNames
    {
        readonly string[] english;
        string[] shown;
        string shownFor;

        string context;

        public ChoiceNames(params string[] english) => this.english = english;

        /// <summary>Names that mean something else elsewhere: looked up as "context|name" (Lang.C).</summary>
        public static ChoiceNames In(string context, string[] english) => new ChoiceNames(english) { context = context };

        /// <summary>How many choices.</summary>
        public int Length => english.Length;

        /// <summary>The names as the player reads them.</summary>
        public string[] Shown
        {
            get
            {
                string lang = Lang.Current;
                if (shown == null || shownFor != lang)
                {
                    shown = new string[english.Length];
                    for (int i = 0; i < english.Length; i++) shown[i] = context != null ? Lang.C(context, english[i]) : Lang.T(english[i]);
                    shownFor = lang;
                }
                return shown;
            }
        }
    }
}
