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
            return p && s ? $"{Vector3.Distance(p.transform.position, s.transform.position):0} m away" : "";
        }
    }
}
