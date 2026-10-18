using System;
using System.Collections.Generic;
using SignLink.Animation;
using SignLink.Signs;
using UnityEngine;
using PetyusPolisz.Kit;

namespace SignLink.UI
{
    /// <summary>Everything that moves around you, in two kinds (0.20.54): GIFs over pictures (moving on every computer
    /// with the file) first, then glyph animations (saved on signs: every SignLink player sees them; and, when a tool
    /// started some, text animations played from here), each with Pause / Play (here only) and the Moving switch (for
    /// everyone: off, the sign keeps its still picture, as vanilla players see it; on, it moves again). A switched-off
    /// one stays in the list (2026-10-16, the author: not Delete but the Moving switch, since that is what it does, and
    /// it stays in the list so Moving can go on again). The playback switches are on Settings › Playback.</summary>
    internal sealed class PlayingTab : Tab
    {
        public PlayingTab(SignLinkWindow w) : base(w) { }

        public override string Title => "Playing";

        static string Where(Sign s)
        {
            if (!s) return "its sign is no longer loaded";
            string name = Session.ShownName(s);
            return (name.Length > 0 ? name : "a sign") + ", " + SignWords.Distance(s);
        }

        /// <summary>A sign near you that can move but plays no screen or animation here: Moving off, or on but not
        /// playing on this computer (the GIF file is not in your images folder, a piece too heavy to play).</summary>
        sealed class Idle
        {
            public Sign Sign;
            public string Text;
            public bool Moving;
        }

        readonly List<Idle> idleGifs = new List<Idle>(), idleAnims = new List<Idle>();

        /// <summary>Is a GIF of this name in the images folder (then only the screens' room keeps it still)?</summary>
        static bool HasFile(string recipeFile)
        {
            string path = Pictures.PictureImporter.PathFor(recipeFile);     // in its folder ("funny/cat.gif"; review: the folder
            return path != null && System.IO.File.Exists(path);             // was dropped, and the row said "no file")
        }
        float nextLook;

        /// <summary>Once a second (or at once after a switch): the GIF pictures and the saved animations within the playback
        /// distance that play nothing here, so a switched-off one stays in the list.</summary>
        void LookAround(float now)
        {
            if (now < nextLook) return;
            nextLook = now + 1f;
            idleGifs.Clear();
            idleAnims.Clear();
            Player p = Player.m_localPlayer;
            if (!p || !ZNet.instance) return;
            Vector3 me = p.transform.position;
            float far = PluginConfig.PlayDistance.Value;
            foreach (Sign s in SignFinder.Loaded())
            {
                ZDO zdo = SignFinder.Zdo(s);
                if (zdo == null || Vector3.Distance(me, s.transform.position) > far) continue;
                if (StoredAnims.HasFrames(zdo))
                {
                    if (Plugin.Stored.Drives(s)) continue;
                    bool moving = !StoredAnims.Still(zdo);
                    idleAnims.Add(new Idle { Sign = s, Moving = moving,
                                             Text = !moving ? "not moving" : !PluginConfig.PlayAnimations.Value ? "paused here (Play all)" : "not playing here" });
                    continue;
                }
                if (zdo.GetInt(SignBuilder.CopyKey, 0) != 0) continue;     // a picture's recipe is on its first sign
                if (!zdo.GetString(SignMarks.SourceKey, "").StartsWith("pic|", StringComparison.Ordinal)) continue;
                SignRecipe r = SignRecipe.Parse(SignMarks.Source(s));
                if (r.Kind != SignRecipe.Kinds.Picture || !r.File.EndsWith(".gif", StringComparison.OrdinalIgnoreCase) || Plugin.Screens.Plays(s)) continue;
                idleGifs.Add(new Idle
                {
                    Sign = s,
                    Moving = r.Animate,
                    // the row adds the sign's name, the GIF's (Where): once is enough
                    // why it stands, as far as this computer knows (1.0.2, review: "starting" stood for any reason)
                    Text = !r.Animate ? "not moving" : !PluginConfig.PlayGifs.Value ? "paused here (Play all)"
                         : Plugin.Restore.WaitsForRoom(s) ? "not moving here (no room: other GIFs play)"
                         : !HasFile(r.File) ? "not moving here (no file)"
                         : Plugin.Restore.Skipped(s) ? "not moving here" : "not moving here yet (starting)",
                });
            }
        }

        /// <summary>The GIF on this sign moves, or no more, for everyone: its recipe says so (every SignLink game with the
        /// file plays it when the sign loads, or does not); off, its screen goes and the sign keeps its still picture,
        /// the one vanilla players see.</summary>
        static string SetGifMoving(Sign any, bool on)
        {
            Sign root = SignFinder.Root(any);
            if (!root || SignFinder.Zdo(root) == null) throw new InvalidOperationException("the sign is gone (unloaded or removed)");
            SignText.RequireEdit(root);
            SignRecipe r = SignRecipe.Parse(SignMarks.Source(root));
            if (r.Kind != SignRecipe.Kinds.Picture) return "that sign holds no GIF picture any more.";
            if (r.Animate != on)
            {
                r.Animate = on;
                SignMarks.SetSource(root, r.ToString());
            }
            if (on)
            {
                Plugin.Restore.Soon();                                      // its screen comes back (ScreenRestore)
                return "the GIF moves again, for every SignLink player who has its file.";
            }
            Plugin.Restore.Pause(root, false);                               // nothing to hold any more
            Plugin.Screens.Stop(root, keep: true);                           // parked: on again plays at once (1.0.2, review:
                                                                            // dropped, it waited its minute to be read again)
            return "the GIF moves no more, for anyone: its sign keeps the still picture, as players without SignLink see it.";
        }

        /// <summary>The Moving switch at a row's end, as on 2 Content: the box and its word both flip it; greyed for a
        /// sign a ward you have no access to protects.</summary>
        static bool MovingSwitch(bool on, bool can)
        {
            bool v = on;
            GUI.enabled = can;
            using (Widgets.Horizontal(Widgets.W(156)))
            {
                if (GUILayout.Toggle(on, GUIContent.none, Theme.Toggle, Widgets.W(30)) != on) v = !on;
                if (GUILayout.Button("Moving", Theme.CheckText)) v = !on;
            }
            GUI.enabled = true;
            return v;
        }

        /// <summary>A row: what it is and where, Pause / Play while it plays here (a gap otherwise), the Moving switch.
        /// Returns the switch as the player left it.</summary>
        bool Row(string text, Sign sign, bool plays, bool paused, bool moving, Func<bool, string> pause)
        {
            bool mine = sign && SignText.CanEdit(sign);
            bool want;
            using (Widgets.Horizontal(Theme.Section))
            {
                GUILayout.Label($"{text} · {Where(sign)}" + (sign && !mine ? " · warded" : ""),
                                Theme.Label, Widgets.W(Widgets.ContentWidth - 40 - 160 - 104));
                if (!plays) Widgets.Space(104);
                else if (Widgets.Button(paused ? "Play" : "Pause", paused ? Theme.ButtonGo : Theme.Button, Widgets.W(100)))
                    Window.Act(() => pause(!paused));
                want = MovingSwitch(moving, mine);
            }
            return want;
        }

        /// <summary>A Moving switch flipped: done as a window action (its result in the status line), and the rows looked at
        /// again at once, so the row keeps its place with the switch as set.</summary>
        void Switched(Func<string> act)
        {
            Window.Act(() =>
            {
                string said = act();
                nextLook = 0f;
                return said;
            });
        }

        /// <summary>One button for every animation of a kind on this computer: Pause all, then Play all (a config switch, so it
        /// lasts over restarts); what pausing does, beside it.</summary>
        void PauseAll(BepInEx.Configuration.ConfigEntry<bool> play, string what, string paused)
        {
            using (Widgets.Horizontal(Theme.Section))
            {
                if (Widgets.Button(play.Value ? $"Pause all {what}" : $"Play all {what}", play.Value ? Theme.Button : Theme.ButtonGo, Widgets.W(260)))
                    Window.Later(() => play.Value = !play.Value);
                GUILayout.Label(play.Value ? "here only" : $"paused: {paused}", Theme.SideNote);
            }
        }

        public override void Body()
        {
            float now = Time.unscaledTime;
            if (Event.current.type == EventType.Layout)                     // the rows change on a Layout only
            {
                LookAround(now);
                idleGifs.RemoveAll(g => g.Sign && Plugin.Screens.Plays(g.Sign));   // started since the last look: one row
                idleAnims.RemoveAll(g => g.Sign && Plugin.Stored.Drives(g.Sign));
            }
            // two kinds, apart (0.20.54, the author: just 2 separate categories ... GIF and glyph animation, GIF on
            // top); each with its own Pause all (0.20.163, the author: put it in playback after all, as 2 buttons, one
            // for glyphs and one for GIFs: from Settings › Playback)
            Widgets.Header("GIFs");
            PauseAll(PluginConfig.PlayGifs, "GIFs", "frozen");
            AreaMeter.Gifs(0);                                              // what the moving GIFs hold here (1.0.2)
            List<ScreenPlayer.Row> screens = Plugin.Screens.Describe(now);
            if (screens.Count == 0 && idleGifs.Count == 0) Widgets.EmptyList("None nearby");
            foreach (ScreenPlayer.Row s in screens)
            {
                Sign sign = s.Sign;
                if (!Row(s.Text, sign, true, s.Paused, true, pause =>
                    {
                        Plugin.Restore.Pause(sign, pause);                  // holds while the stack unloads and comes back
                        return Plugin.Screens.Pause(sign, pause) > 0
                            ? (pause ? "paused the GIF: its still picture shows; Play moves it again." : "the GIF moves again.")
                            : "that GIF is gone.";
                    }))
                    Switched(() => SetGifMoving(sign, false));
            }
            foreach (Idle g in idleGifs)
            {
                Sign sign = g.Sign;
                bool on = !g.Moving;
                if (Row(g.Text, sign, false, false, g.Moving, null) == on) Switched(() => SetGifMoving(sign, on));
            }

            Widgets.Space(6);
            Widgets.Rule();
            Widgets.Header("Glyph animations");
            PauseAll(PluginConfig.PlayAnimations, "glyph animations", "first frame");
            List<KeyValuePair<string, Sign>> stored = Plugin.Stored.Describe();
            List<KeyValuePair<string, Sign>> tools = Plugin.Text.Describe(now);
            if (stored.Count == 0 && tools.Count == 0 && idleAnims.Count == 0)
                Widgets.EmptyList(!PluginConfig.PlayAnimations.Value ? "Paused" : "None nearby");
            foreach (KeyValuePair<string, Sign> s in stored)
            {
                Sign sign = s.Value;
                if (!Row(s.Key, sign, true, Plugin.Stored.Paused(sign), true, pause =>
                    {
                        Plugin.Stored.Pause(sign, pause);
                        return pause ? "paused: the sign shows its first frame here; Play moves it again." : "the animation moves again.";
                    }))
                    Switched(() => Plugin.Stored.SetMoving(sign, false));
            }
            foreach (Idle a in idleAnims)
            {
                Sign sign = a.Sign;
                bool on = !a.Moving;
                if (Row(a.Text, sign, false, false, a.Moving, null) == on) Switched(() => Plugin.Stored.SetMoving(sign, on));
            }
            foreach (KeyValuePair<string, Sign> t in tools)                 // a program's, through the API: they stop when you leave
                if (Widgets.Row($"{t.Key} · {Where(t.Value)} · started by a tool", "Stop") == 0)
                    Window.Act(() => Plugin.Text.Stop(t.Value) > 0 ? "stopped that animation." : "it had already stopped.");
        }
    }
}
