using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using BepInEx.Configuration;
using UnityEngine;
using PetyusPolisz.Kit;

namespace SignLink
{
    /// <summary>Every config entry in one place (BepInEx/config/PetyusPolisz.SignLink.cfg), in the order a player reads
    /// them: the menu first, the diagnostics last (the file and ConfigurationManager sort sections by name).</summary>
    internal static class PluginConfig
    {
        public static ConfigEntry<KeyboardShortcut> MenuKey;
        public static ConfigEntry<float> MenuScale;
        public static ConfigEntry<float> MenuWidth, MenuHeight;
        public static ConfigEntry<float> MenuOpacity;
        public static ConfigEntry<string> MenuTheme, MenuLanguage;
        public static ConfigEntry<bool> Highlight, HighlightContent;
        public static ConfigEntry<float> WindowX, WindowY;
        public static ConfigEntry<bool> PngTransparency, MovingGifs;
        public static ConfigEntry<bool> PlayAnimations, PlayGifs;
        public static ConfigEntry<float> PlayDistance;
        public static ConfigEntry<int> GifMemory;
        public static ConfigEntry<bool> PictureFineWidth;
        public static ConfigEntry<bool> LogSlowSigns;

        static ConfigFile file;

        /// <summary>Every setting back to its default (Settings › Reset to defaults, 0.20.121, the author: Settings
        /// needs a Reset to defaults button too, with a confirmation as well), the hotkey too. The file is written once.
        /// Returns how many changed.</summary>
        public static int ResetToDefaults()
        {
            if (file == null) return 0;
            int n = 0;
            bool save = file.SaveOnConfigSet;
            file.SaveOnConfigSet = false;
            try
            {
                foreach (KeyValuePair<ConfigDefinition, ConfigEntryBase> e in file)
                {
                    if (Equals(e.Value.BoxedValue, e.Value.DefaultValue)) continue;
                    e.Value.BoxedValue = e.Value.DefaultValue;
                    n++;
                }
            }
            finally
            {
                file.SaveOnConfigSet = save;
            }
            file.Save();
            return n;
        }

        public static void Bind(ConfigFile cfg)
        {
            file = cfg;
            MoveOldFile(cfg);
            MenuKey = cfg.Bind("1 - Menu", "Toggle", new KeyboardShortcut(KeyCode.PageUp),
                Desc("Opens and closes the SignLink window.", 5));
            MenuKey.SettingChanged += (_, __) =>
            {
                keyParts = null;
                keyText = null;
            };
            MenuScale = cfg.Bind("1 - Menu", "Scale", 1f,
                Desc("Text size in the window (1 = normal for your screen height). The window keeps its size.", 4,
                     new AcceptableValueRange<float>(0.6f, 1.5f)));   // the Text size buttons (SignLinkWindow.Scales, 0.20.120)
            MenuWidth = cfg.Bind("1 - Menu", "Width", 0f,
                Desc("The window's width as a share of the screen's, up to 1 (the whole screen); 0 is automatic: as wide as " +
                     "80 % of the screen's height, on any screen shape. It never gets narrower than the rows need at the text " +
                     "size. The window's edges and corners resize it too.", 3,
                     new AcceptableValueRange<float>(0f, WindowSizing.WidthMost)));
            MenuHeight = cfg.Bind("1 - Menu", "Height", 0.86f,
                Desc("The window's height as a share of the screen's: 1 reaches from the top of the screen to the bottom.", 3,
                     new AcceptableValueRange<float>(WindowSizing.HeightLeast, WindowSizing.HeightMost)));
            MenuOpacity = cfg.Bind("1 - Menu", "Opacity", 0.85f,
                Desc("How solid the window is: 1 is opaque, 0.5 shows the world through it. The text stays readable.", 2,
                     new AcceptableValueRange<float>(0.5f, 1f)));
            MenuTheme = cfg.Bind("1 - Menu", "Theme", Theme.ClassicName,         // the default (1.1.0, the author; Nordic before)
                Desc("The window's look: Classic (dark wood and gold) or Nordic (slate and amber).", 2,
                     new AcceptableValueList<string>(Theme.Names)));
            // the window's language (1.1.0, the author: Game default takes the game's language, English where SignLink has
            // no table for it; the default): Game default first, so a value not in the list becomes it
            MenuLanguage = cfg.Bind("1 - Menu", "Language", Lang.GameDefault,
                Desc("The window's language: Game default follows the game's language setting (English where SignLink has no " +
                     "translation for it), or a language chosen here.", 2,
                     new AcceptableValueList<string>(Lang.Choices)));
            WindowX = cfg.Bind("1 - Menu", "WindowX", -1f,
                Desc("Where the window was dragged: its left edge in screen pixels (-1: the default place).", 0));
            WindowY = cfg.Bind("1 - Menu", "WindowY", -1f,
                Desc("Where the window was dragged: its top edge in screen pixels (-1: the default place).", 0));
            Highlight = cfg.Bind("3 - Sign options", "Highlight", true,
                Desc("While the window is open, signs' boards are outlined in the world: the selected one, Show all's, the one " +
                     "under the cursor (where content goes is HighlightContent's).", 4));
            HighlightContent = cfg.Bind("3 - Sign options", "HighlightContent", true,
                Desc("While the window is open, where a sign's content goes is framed in the world, green over what " +
                     "will show, red where a wall or a floor stands in front of it, grey stripes seen from behind (the board's " +
                     "outline is Highlight's).", 3));
            PlayAnimations = cfg.Bind("4 - Playback", "PlayAnimations", true,
                Desc("Play the glyph animations saved on signs (moving art pieces). Off: they show " +
                     "their first frame, as players without SignLink see them.", 4));
            // the author (0.20.160): two switches, one for the glyphs and one for the GIFs
            PlayGifs = cfg.Bind("4 - Playback", "PlayGifs", true,
                Desc("Play moving GIFs over their pictures. Off freezes them on their current frame.", 3));
            PlayDistance = cfg.Bind("4 - Playback", "Distance", 64f,
                Desc("Glyph animations and GIFs farther than this many metres pause.", 2, new AcceptableValueRange<float>(8f, 200f)));
            // the author (1.0.2): 512 and 1024 too; lowered, the farthest screens park until the rest fit (ScreenPlayer.Trim)
            GifMemory = cfg.Bind("4 - Playback", "GifMemoryMB", 256,
                Desc("The memory the moving GIFs' frames may take on this computer, in MB: 256, 512 or 1024. More GIFs move at " +
                     "once with more; 1024 wants a PC with 16 GB of RAM, as the game itself takes several GB. Lowered, the " +
                     "farthest GIFs stop until the rest fit.", 1, new AcceptableValueList<int>(256, 512, 1024)));
            PngTransparency = cfg.Bind("5 - Pictures", "PngTransparency", true,
                Desc("A PNG's see-through parts stay empty on the sign, half-transparent ones half transparent.", 2));
            // a choice of each picture since 0.20.160 (the Moving switch on 2 Content, as an art piece's): this is where it
            // starts for a GIF picked from the list, the last choice
            MovingGifs = cfg.Bind("5 - Pictures", "MovingGifs", true,
                Desc("A GIF picked on 2 Content starts with Moving on: it plays over its still whenever its sign is near, on " +
                     "every computer that has the GIF file in its SignLink images folder (its frames are not in the world). " +
                     "The Moving switch keeps your last choice here.", 1));
            // the author: can it be set in the settings? (0.20.122): a fine size costs data, so it is a choice; on by
            // default under a new name (0.20.124, the author: fine tune enabled by default; every file held the
            // first one's default off)
            PictureFineWidth = cfg.Bind("5 - Pictures", "FineWidth", true,
                Desc("A picture's Width and Height move finely (dragged, typed, Shift + mouse wheel about a pixel a notch). " +
                     "Your Resolution stays: for the exact size the picture takes the pixels nearest it, a few more or less, " +
                     "on a server never more than the multiplayer limit allows. Off: the next size it takes for the same data (20-150 cm a " +
                     "step on most pictures: the game reads only some pixel sizes exactly).", 1));
            LogSlowSigns = cfg.Bind("9 - Diagnostics", "LogSlowSigns", false,
                Desc("Log sign hover texts and text refreshes that take over 30 ms (to find stalls); off unless asked for.", 1));
            Migrate(cfg);
        }

        /// <summary>Until 0.20.43 the GUID was bakterhaz.signlink, and BepInEx names the config file after the GUID: the old
        /// file takes the new name once, before anything is bound, so the settings carry over.</summary>
        static void MoveOldFile(ConfigFile cfg)
        {
            try
            {
                string now = Path.GetFileName(cfg.ConfigFilePath);
                if (!ConfigMoves.MoveFile(Path.GetDirectoryName(cfg.ConfigFilePath), now)) return;
                cfg.Reload();
                Plugin.Log.LogInfo($"config: {ConfigMoves.OldFile} is {now} now (the plugin's id changed), its settings kept");
            }
            catch (Exception e)
            {
                Plugin.Log.LogWarning($"config: {ConfigMoves.OldFile} could not take the new name, the settings start anew: {e.Message}");
            }
        }

        /// <summary>An older config's values (orphans under the new names: ConfigMoves) go to their new entries once,
        /// where the new entry is still at its default (a choice already made there stays), and the old entries
        /// leave the file. Entries a later
        /// version dropped (ConfigMoves.Stale) leave the file too. BepInEx keeps the orphans in a private dictionary;
        /// without it nothing moves and the file is as it was.</summary>
        static void Migrate(ConfigFile cfg)
        {
            try
            {
                var orphans = typeof(ConfigFile).GetProperty("OrphanedEntries", BindingFlags.NonPublic | BindingFlags.Instance)?.GetValue(cfg)
                              as Dictionary<ConfigDefinition, string>;
                if (orphans == null || orphans.Count == 0) return;
                var old = new Dictionary<string, string>();
                foreach (KeyValuePair<ConfigDefinition, string> e in orphans) old[e.Key.Section + "|" + e.Key.Key] = e.Value;
                int moved = 0, dropped = 0;
                foreach (ConfigMoves.Move m in ConfigMoves.Plan(old))
                {
                    string[] to = m.To.Split('|'), from = m.From.Split('|');
                    var def = new ConfigDefinition(to[0], to[1]);
                    if (cfg.ContainsKey(def))
                    {
                        ConfigEntryBase entry = cfg[def];
                        if (!m.Superseded && Equals(entry.BoxedValue, entry.DefaultValue))
                        {
                            try
                            {
                                entry.SetSerializedValue(m.Value);
                                moved++;
                            }
                            catch (Exception e) { Plugin.Log.LogWarning($"config: {m.To} keeps its default, the old value cannot be read: {e.Message}"); }
                        }
                    }
                    if (orphans.Remove(new ConfigDefinition(from[0], from[1]))) dropped++;
                }
                if (ConfigMoves.OldSize(old, out float oldW, out float oldH) &&
                    Equals(MenuWidth.BoxedValue, MenuWidth.DefaultValue) && Equals(MenuHeight.BoxedValue, MenuHeight.DefaultValue))
                {
                    MenuWidth.Value = oldW;                                     // the old Window size, as Width and Height
                    MenuHeight.Value = oldH;
                    moved++;
                }
                foreach (string stale in ConfigMoves.Stale(old))
                {
                    string[] k = stale.Split('|');
                    if (orphans.Remove(new ConfigDefinition(k[0], k[1]))) dropped++;
                }
                if (dropped > 0) cfg.Save();
                if (moved > 0 || dropped > 0)
                    Plugin.Log.LogInfo($"config: {moved} setting(s) moved from an older config to their sections, {dropped} old entries dropped");
            }
            catch (Exception e)
            {
                Plugin.Log.LogWarning($"config: the older settings could not be moved: {e.Message}");
            }
        }

        /// <summary>The window's hotkey as people write it, the modifiers first ("Shift + Page Up"; UI.HotkeyText,
        /// 0.20.82), one key a part; none for no key.</summary>
        public static string[] MenuKeyParts()
        {
            if (keyParts != null) return keyParts;
            KeyboardShortcut k = MenuKey.Value;
            var mods = new List<string>();
            foreach (KeyCode m in k.Modifiers) mods.Add(m.ToString());
            return keyParts = HotkeyText.Parts(k.MainKey.ToString(), mods);
        }

        /// <summary>The same in one line ("" for no key).</summary>
        public static string MenuKeyText() => keyText ?? (keyText = string.Join(" + ", MenuKeyParts()));

        // made once per hotkey (the window's footer draws them every event; 0.20.91, review)
        static string[] keyParts;
        static string keyText;

        static ConfigDescription Desc(string text, int order, AcceptableValueBase range = null) =>
            new ConfigDescription(text, range, new ConfigurationManagerAttributes { Order = order });

        /// <summary>Read by BepInEx.ConfigurationManager through reflection; a private copy is the usual way.</summary>
        sealed class ConfigurationManagerAttributes
        {
            public int? Order;
        }
    }
}
