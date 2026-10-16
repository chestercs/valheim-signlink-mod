using System;
using System.Collections.Generic;
using SignLink.Animation;
using SignLink.Signs;
using UnityEngine;

namespace SignLink.UI
{
    /// <summary>Everything that moves around you, in two kinds (0.20.54): GIFs playing over pictures (on every computer
    /// with the file) first, then glyph animations (saved on signs: every SignLink player sees them; and, when a tool
    /// started some, text animations played from here), each with Pause / Play (here only) and Delete (the movement goes
    /// for everyone, the sign keeps its still picture, as vanilla players see it). The playback switches are on
    /// Settings › Playback.</summary>
    internal sealed class PlayingTab : Tab
    {
        public PlayingTab(SignLinkWindow w) : base(w) { }

        public override string Title => "Playing";

        static string Where(Sign s)
        {
            if (!s) return "its sign is no longer loaded";
            string name = Session.ShownName(s);
            return (name.Length > 0 ? name : "a sign") + ", " + Widgets.Distance(s);
        }

        /// <summary>The GIF on this sign moves no more, for anyone: its recipe says still (no SignLink game plays it again
        /// when the sign loads) and its screen goes; the sign keeps its still picture, the one vanilla players see.</summary>
        static string StopMoving(Sign any)
        {
            Sign root = SignFinder.Root(any);
            if (!root || SignFinder.Zdo(root) == null) throw new InvalidOperationException("the sign is gone (unloaded or removed)");
            SignText.RequireEdit(root);
            SignRecipe r = SignRecipe.Parse(SignMarks.Source(root));
            if (r.Kind == SignRecipe.Kinds.Picture && r.Animate)
            {
                r.Animate = false;
                SignMarks.SetSource(root, r.ToString());
            }
            Plugin.Restore.Pause(root, false);                               // nothing to hold any more
            Plugin.Screens.Stop(root, keep: false);
            return "the GIF moves no more, for anyone: its sign keeps the still picture, as players without SignLink see it.";
        }

        /// <summary>One button for every animation of a kind on this computer: Pause all, then Play all (a config switch, so it
        /// lasts over restarts); what pausing does, beside it.</summary>
        void PauseAll(BepInEx.Configuration.ConfigEntry<bool> play, string what, string paused)
        {
            using (Widgets.Horizontal(Theme.Section))
            {
                if (Widgets.Button(play.Value ? $"Pause all {what}" : $"Play all {what}", play.Value ? Theme.Button : Theme.ButtonGo, Widgets.W(260)))
                    Window.Later(() => play.Value = !play.Value);
                GUILayout.Label(play.Value ? "here only, until you play them again" : $"paused here: {paused}", Theme.SideNote);
            }
        }

        public override void Body()
        {
            float now = Time.unscaledTime;
            // two kinds, apart (0.20.54, the author: just 2 separate categories ... GIF and glyph animation, GIF on
            // top); each with its own Pause all (0.20.163, the author: put it in playback after all, as 2 buttons, one
            // for glyphs and one for GIFs: from Settings › Playback)
            Widgets.Header("GIFs");
            PauseAll(PluginConfig.PlayGifs, "GIFs", "frozen where they are");
            Widgets.Hint("They move over their pictures wherever the GIF file is in the images folder: here, and for any SignLink " +
                         "player who has it. Pause shows the still picture here only; Delete stops it moving for everyone: the sign keeps its still picture.");
            List<ScreenPlayer.Row> screens = Plugin.Screens.Describe(now);
            if (screens.Count == 0) Widgets.EmptyList("No moving GIFs nearby. Each one shows here on a row of its own, with Pause and Delete.");
            foreach (ScreenPlayer.Row s in screens)
                using (Widgets.Horizontal(Theme.Section))
                {
                    bool mine = s.Sign && SignText.CanEdit(s.Sign);
                    GUILayout.Label($"{s.Text} · {Where(s.Sign)}" + (s.Sign && !mine ? " · a ward you have no access to protects it" : ""),
                                    Theme.Label, Widgets.W(Widgets.ContentWidth - 40 - 160 - 104));
                    if (Widgets.Button(s.Paused ? "Play" : "Pause", s.Paused ? Theme.ButtonGo : Theme.Button, Widgets.W(100)))
                        Window.Act(() =>
                        {
                            bool pause = !s.Paused;
                            Plugin.Restore.Pause(s.Sign, pause);            // holds while the stack unloads and comes back
                            return Plugin.Screens.Pause(s.Sign, pause) > 0
                                ? (pause ? "paused the GIF: its still picture shows; Play moves it again." : "the GIF moves again.")
                                : "that GIF is gone.";
                        });
                    GUI.enabled = mine;
                    if (Armed.Button(Window, "play.gifdel|" + (SignFinder.Id(s.Sign) ?? s.Text), "Delete", "Sure? Click again", Theme.Button, Widgets.W(156)))
                        Window.Act(() => StopMoving(s.Sign));
                    GUI.enabled = true;
                }

            Widgets.Space(6);
            Widgets.Rule();
            Widgets.Header("Glyph animations");
            PauseAll(PluginConfig.PlayAnimations, "glyph animations", "their first frame shows");
            Widgets.Hint("Saved in the world with their sign: SignLink players see them move, players without SignLink the first frame. " +
                         "Pause shows the first frame here only; Delete takes the movement off the sign for everyone.");
            List<KeyValuePair<string, Sign>> stored = Plugin.Stored.Describe();
            List<KeyValuePair<string, Sign>> tools = Plugin.Text.Describe(now);
            if (stored.Count == 0 && tools.Count == 0)
                Widgets.EmptyList(!PluginConfig.PlayAnimations.Value ? "Glyph animations are paused here (Play all, above)."
                                  : "No glyph animations nearby. Each one shows here on a row of its own, with Pause and Delete.");
            foreach (KeyValuePair<string, Sign> s in stored)
                using (Widgets.Horizontal(Theme.Section))
                {
                    bool mine = s.Value && SignText.CanEdit(s.Value);
                    bool still = Plugin.Stored.Paused(s.Value);
                    GUILayout.Label($"{s.Key} · {Where(s.Value)}" + (s.Value && !mine ? " · a ward you have no access to protects it" : ""),
                                    Theme.Label, Widgets.W(Widgets.ContentWidth - 40 - 160 - 104));
                    if (Widgets.Button(still ? "Play" : "Pause", still ? Theme.ButtonGo : Theme.Button, Widgets.W(100)))
                        Window.Act(() =>
                        {
                            Plugin.Stored.Pause(s.Value, !still);
                            return still ? "the animation moves again." : "paused: the sign shows its first frame here; Play moves it again.";
                        });
                    GUI.enabled = mine;                                     // a ward you have no access to: not yours to delete
                    if (Armed.Button(Window, "play.remove|" + (SignFinder.Id(s.Value) ?? s.Key), "Delete", "Sure? Click again", Theme.Button, Widgets.W(156)))
                        Window.Act(() => Plugin.Stored.Remove(s.Value)
                            ? "deleted the animation: the sign keeps its first frame as a still picture, for everyone."
                            : "that sign has no saved animation any more.");
                    GUI.enabled = true;
                }
            foreach (KeyValuePair<string, Sign> t in tools)                 // a program's, through the API: they stop when you leave
                if (Widgets.Row($"{t.Key} · {Where(t.Value)} · started by a tool", "Stop") == 0)
                    Window.Act(() => Plugin.Text.Stop(t.Value) > 0 ? "stopped that animation." : "it had already stopped.");
        }
    }
}
