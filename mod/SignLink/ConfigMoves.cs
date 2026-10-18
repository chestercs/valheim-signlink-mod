using System;
using System.Collections.Generic;
using System.IO;

namespace SignLink
{
    /// <summary>0.19's config sections were named otherwise (1 - API, 2 - Playback, 3 - Viewer, 4 - Menu, 5 - Building),
    /// and 0.20.45 put the sign settings under 3 - Sign options with clearer names. BepInEx keys an entry by its section
    /// and name, so an older file's values are orphans under the new names: the API would start with a new password and
    /// switched off. Plan says where each goes; PluginConfig moves them once, at start. Stale names the entries later versions dropped, so they leave the file too. MoveFile carries the file
    /// itself over the new GUID of 0.20.43. No Unity here (mod/SignLink.Tests).</summary>
    internal static class ConfigMoves
    {
        public sealed class Move
        {
            /// <summary>"Section|Key", old and new, and the value as written in the file.</summary>
            public string From, To, Value;
            /// <summary>A newer home of the same setting came first: this old entry only leaves the file.</summary>
            public bool Superseded;
        }

        /// <summary>Old section, new section, then the keys that moved between them.</summary>
        static readonly string[][] Moves =
        {
            new[] { "1 - API", "9 - Developer API", "Enabled", "Port", "Password", "AllowNetwork" },
            new[] { "2 - Playback", "4 - Playback", "PlayAnimations", "Distance" },
            new[] { "4 - Menu", "1 - Menu", "Toggle", "Scale" },
            new[] { "4 - Playback", "3 - Sign options", "RefreshSeconds" },                     // 0.20.45
        };

        /// <summary>Entries that took a new name ("Section|Key", old then new), a setting's newest old home first: the
        /// settings of 3 - Sign options (0.20.45; the author: rename Signs in the world to Sign options ... and call it
        /// just Prevent ...).</summary>
        static readonly string[][] Renamed =
        {
            new[] { "1 - Menu|ShowGhost", "3 - Sign options|Highlight" },
            new[] { "5 - Pictures|PlayGifs", "5 - Pictures|MovingGifs" },                        // 0.20.160
            new[] { "9 - Developer API|LogSlowSigns", "9 - Diagnostics|LogSlowSigns" },          // 1.0.3
        };

        /// <summary>Entries no version keeps any more, under every section they ever had ("Section|Key"): HoverSprites
        /// (0.20.4: emoji sprites always leave sign hover texts, since the game leaves an animated one stuck by the
        /// crosshair; the switch only let the bug back in), FastRefresh (0.20.17: the re-read pace is RefreshSeconds,
        /// 2 s by default, which is the game's own pace; the switch was on by default for a networked animation mode
        /// nobody uses from the window), MultiplayerSafe (0.20.39: every sign of a picture stays under 400 KB, always;
        /// the author: remove Multiplayer-safe? yes), AllowOnServers (0.20.44: SignLink makes and removes signs in
        /// multiplayer too; the multiplayer limits keep the content small, a ward still protects, and the switch was the
        /// player's own, never a protection for the server), WindowSize (0.20.46: Width and Height apart; OldSize keeps
        /// its size), PreventEditor and its older names PictureSigns (1.0.2: the short hover line on a picture sign and E
        /// opening SignLink are always on; off, the game read a picture's whole text by the crosshair with a stutter, and a
        /// file that said off kept it off once the switch left the window), the developer API's entries (1.0.3: a released
        /// SignLink has no web server, the Thunderstore moderators asked; a developer build binds them, so there they are no
        /// orphans and stay, and its password with them).</summary>
        static readonly string[] Dropped =
        {
            "3 - Viewer|HoverSprites", "3 - Signs in the world|HoverSprites", "Viewer|HoverSprites",
            "3 - Viewer|FastRefresh", "4 - Playback|FastRefresh",
            "5 - Pictures|MultiplayerSafe",
            "5 - Building|AllowOnServers", "2 - Building|AllowOnServers",
            "1 - Menu|WindowSize", "4 - Menu|WindowSize",
            "3 - Sign options|ShowAll",                                     // starts off every game (0.20.123)
            "5 - Pictures|FineWidthSteps",                                  // FineWidth, on by default (0.20.124)
            "3 - Sign options|PreventEditor", "3 - Signs in the world|PictureSigns", "3 - Viewer|PictureSigns",   // always on (1.0.2)
            "9 - Developer API|Enabled", "9 - Developer API|Port", "9 - Developer API|AllowNetwork", "9 - Developer API|Password",
            "1 - API|Enabled", "1 - API|Port", "1 - API|AllowNetwork", "1 - API|Password",          // a release: no API (1.0.3)
        };

        /// <summary>For the orphaned entries ("Section|Key" to the value as written) that have a new home: the move, in
        /// the order of Moves, then Renamed; a second old entry of the same setting is Superseded (the newer one's value
        /// goes). Entries no version knows stay where they are.</summary>
        public static List<Move> Plan(IDictionary<string, string> orphaned)
        {
            var plan = new List<Move>();
            var taken = new HashSet<string>();
            void Add(string from, string to)
            {
                if (orphaned.TryGetValue(from, out string value))
                    plan.Add(new Move { From = from, To = to, Value = value, Superseded = !taken.Add(to) });
            }
            foreach (string[] m in Moves)
                for (int i = 2; i < m.Length; i++) Add(m[0] + "|" + m[i], m[1] + "|" + m[i]);
            foreach (string[] r in Renamed) Add(r[0], r[1]);
            return plan;
        }

        /// <summary>The config file's name until 0.20.43: BepInEx names the file after the plugin's GUID, bakterhaz.signlink
        /// then, PetyusPolisz.SignLink since.</summary>
        public const string OldFile = "bakterhaz.signlink.cfg";

        /// <summary>The old config file in dir takes the name `now`, once: only when it is there and no file has the new
        /// name yet (a new one already made wins and the old one is left alone). True when it moved.</summary>
        public static bool MoveFile(string dir, string now)
        {
            if (string.Equals(now, OldFile, StringComparison.OrdinalIgnoreCase)) return false;
            string from = Path.Combine(dir, OldFile), to = Path.Combine(dir, now);
            if (!File.Exists(from) || File.Exists(to)) return false;
            File.Move(from, to);
            return true;
        }

        /// <summary>The window's size before 0.20.46 was one of four presets (1 - Menu / WindowSize; 4 - Menu in 0.19),
        /// since then Width and Height are shares of the screen apart: the preset's size as such shares on a 16:9 screen
        /// (its width was a share of the screen's height; Large's is the automatic width, 0), the newer entry first. False without one, or with a name no
        /// version had.</summary>
        public static bool OldSize(IDictionary<string, string> orphaned, out float width, out float height)
        {
            width = height = 0f;
            foreach (string k in new[] { "1 - Menu|WindowSize", "4 - Menu|WindowSize" })
            {
                if (!orphaned.TryGetValue(k, out string v)) continue;
                switch ((v ?? "").Trim())
                {
                    case "Small": width = 0.33f; height = 0.70f; return true;
                    case "Medium": width = 0.38f; height = 0.78f; return true;
                    case "Large": width = 0f; height = 0.86f; return true;          // the automatic width
                    case "VeryLarge": width = 0.5f; height = 0.92f; return true;
                    default: return false;
                }
            }
            return false;
        }

        /// <summary>The orphaned entries that were dropped: they leave the file.</summary>
        public static List<string> Stale(IDictionary<string, string> orphaned)
        {
            var stale = new List<string>();
            foreach (string k in Dropped)
                if (orphaned.ContainsKey(k)) stale.Add(k);
            return stale;
        }
    }
}
