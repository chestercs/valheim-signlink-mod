using System;
using SignLink.Pictures;
using UnityEngine;
using PetyusPolisz.Kit;

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

        /// <summary>Draws the group: its title and the row (no hint under it since 1.0.2, the author: no long texts), the last group of a source (0.20.69, the author: in
        /// 2 Content the light goes to the very bottom everywhere after all; not into the footer, just the last of the
        /// groups ... with a group of its own, as the resolution has). A click reaches the choice at a later Layout (the button
        /// shown chosen changes). A light the row does not offer shows as its first choice.</summary>
        public static void Draw(Session s, SignLinkWindow w, GlowMode[] lights, string[] names)
        {
            int at = Math.Max(0, Array.IndexOf(lights, s.Chosen.Light));
            Widgets.Header("Light");
            using (Widgets.Vertical(Theme.Section))
            {
                {
                    int pick = Widgets.Choice("Light", at, names, names.Length * 140f);   // small (0.20.85)
                    if (pick != at)
                    {
                        GlowMode m = lights[pick];
                        w.Later(() => w.Session.Chosen.Light = m);
                    }
                }
            }
        }

    }
}
