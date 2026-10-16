using System;
using SignLink.Pictures;
using UnityEngine;

namespace SignLink.UI
{
    /// <summary>The Light row of a source on 2 Content: how its content is lit (Session.Chosen.Light), with the choices
    /// that make sense for it (a picture glows or is lit by the scene; a piece may also stay as made), and one line on
    /// what the choice does. The light belongs to the content, so it sits with it: a picture's glow is one of its
    /// settings, a piece's light is in its recipe; a text carries its own tags, so the Text source edits the text
    /// instead (SignGlow.Apply) and draws its own row.</summary>
    internal static class LightRow
    {
        public static readonly GlowMode[] PictureLights = { GlowMode.Glowing, GlowMode.Lit },
                                          PieceLights = { GlowMode.AsMade, GlowMode.Glowing, GlowMode.Lit };
        public static readonly string[] PictureNames = { "Glowing", "Lit by the scene" },
                                        PieceNames = { "As made", "Glowing", "Lit by the scene" };

        /// <summary>Draws the group: its title, the row and its hint, the last group of a source (0.20.69, the author: in
        /// 2 Content the light goes to the very bottom everywhere after all; not into the footer, just the last of the
        /// groups ... with a group of its own, as the resolution has). A click reaches the choice at a later Layout (the button
        /// shown chosen changes). A light the row does not offer shows as its first choice.</summary>
        public static void Draw(Session s, SignLinkWindow w, GlowMode[] lights, string[] names, string asMadeHint)
        {
            int at = Math.Max(0, Array.IndexOf(lights, s.Chosen.Light));
            Widgets.Header("Light");
            using (Widgets.Vertical(Theme.Section))
            {
                using (Widgets.Horizontal())
                {
                    GUILayout.Label("Light", Theme.Label, Widgets.W(Widgets.LabelW));
                    int pick = Widgets.Toolbar(at, names, Theme.Chip, Widgets.W(Mathf.Min(names.Length * 140f, Widgets.RestW)));   // small (0.20.85)
                    GUILayout.FlexibleSpace();
                    if (pick != at)
                    {
                        GlowMode m = lights[pick];
                        w.Later(() => w.Session.Chosen.Light = m);
                    }
                }
                Widgets.Hint(Hint(lights[at], asMadeHint));
            }
        }

        /// <summary>What a light choice does, in one line.</summary>
        public static string Hint(GlowMode m, string asMade) =>
            m == GlowMode.Glowing ? "Glowing: its own colours, bright day and night."
            : m == GlowMode.Lit ? "Lit by the scene: like the game's own sign text, true colours by day, dark at night."
            : asMade;
    }
}
