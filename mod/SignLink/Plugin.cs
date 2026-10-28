// SignLink: write, animate and screen sign art in Valheim. Client-side BepInEx plugin, vanilla compatible.
// Layout: Plugin (wiring) / PluginConfig / Signs (sign helpers, stacks, building) / Pictures (PNG/JPG to sign text) /
// Animation (texture screens, saved animations) / Patches / Presets / UI (the window).
using System;
using System.Collections.Generic;
using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using SignLink.Animation;
using SignLink.Signs;
using UnityEngine;

namespace SignLink
{
    [BepInPlugin(Guid, Name, Version)]
    [BepInProcess("valheim.exe")]
    public class Plugin : BaseUnityPlugin
    {
        public const string Guid = "PetyusPolisz.SignLink";              // the Thunderstore team's name (bakterhaz.signlink until 0.20.43)
        public const string Name = "SignLink";
        public const string Version = "1.2.0";

        internal static ManualLogSource Log;
        internal static ScreenPlayer Screens;
        internal static ScreenRestore Restore;
        internal static StoredAnims Stored;
        internal static UI.ContentPreview Preview;

        Harmony harmony;

        void Awake()
        {
            Log = Logger;
            MainThread.Report = e => Log.LogError(e);
            Languages();                                                    // before Bind: the Language setting lists them
            PluginConfig.Bind(Config);
            harmony = new Harmony(Guid);
            Patch();
            Screens = new ScreenPlayer();
            Restore = new ScreenRestore();
            Stored = new StoredAnims();
            Preview = new UI.ContentPreview();
            // the steps that touch the game or the disk, each on its own: one that throws (an odd data folder, a game update)
            // leaves the window and the rest working (1.1.0, review: a throw here left no window, the patches on)
            TryStart("the loaded signs", SignFinder.Seed);                  // loaded again in a running game: its signs (0.20.135)
            TryStart("the images folder", Pictures.PictureFolder.Find);   // the old one moves over once
            TryStart("the stock pictures", () => Pictures.StockPictures.Place(Pictures.PictureImporter.Folder));   // the paintings and
                                                                            // the horse GIF on every computer with the mod (review)
            gameObject.AddComponent<UI.SignLinkWindow>();
            PluginConfig.GifMemory.SettingChanged += (_, __) => Restore.RoomFreed();   // raised: the GIFs waiting for room start
                                                                                       // (lowered: ScreenPlayer.Trim at its step)
            PluginConfig.GifFullQuality.SettingChanged += (_, __) =>                 // the frames were made at the other size:
            {                                                                        // the GIFs near come back from their files
                Screens.StopAll();                                                   // at the new one (ScreenRestore, nearest first)
                Restore.Rescan();
            };
            Log.LogInfo($"{Name} {Version} loaded");
            Commands.Register();                                            // last, and guarded: a game update to the console
        }                                                                   // must not stop the rest of the plugin

        /// <summary>A start step that may fail on its own: logged, the plugin goes on.</summary>
        static void TryStart(string what, Action step)
        {
            try { step(); }
            catch (Exception e) { Log.LogError($"{what}: {e}"); }
        }

        static readonly HashSet<Type> patched = new HashSet<Type>();

        /// <summary>Did this patch class apply? (A feature that needs it falls back or stays off without it.)</summary>
        public static bool Patched(Type t) => patched.Contains(t);

        /// <summary>The window's languages (1.1.0): a table per language in the DLL (Lang/&lt;Language&gt;.txt, the
        /// game's own name for it: "Hungarian"), read when first used; the words follow the Language setting, Game default
        /// the game's language (Localization), English where no table is. No file under BepInEx/config.</summary>
        static void Languages()
        {
            System.Reflection.Assembly own = typeof(Plugin).Assembly;
            const string Prefix = "SignLink.Lang.", Suffix = ".txt";
            foreach (string res in own.GetManifestResourceNames())
            {
                if (!res.StartsWith(Prefix, StringComparison.Ordinal) || !res.EndsWith(Suffix, StringComparison.Ordinal)) continue;
                string name = res;
                PetyusPolisz.Kit.Lang.Add(res.Substring(Prefix.Length, res.Length - Prefix.Length - Suffix.Length), () =>
                {
                    using (var r = new System.IO.StreamReader(own.GetManifestResourceStream(name))) return r.ReadToEnd();
                });
            }
            PetyusPolisz.Kit.Lang.Order = new[] { "Hungarian", "Chinese", "Russian" };   // the setting's order (1.1.0, the author)
            PetyusPolisz.Kit.Lang.Setting = () => PluginConfig.MenuLanguage?.Value;
            PetyusPolisz.Kit.Lang.Game = () => gameLanguage;                // read on the main thread (Update): workers word texts too
            ReadGameLanguage();
        }

        /// <summary>Every patch class on its own: one that a game update breaks is skipped with a warning, the rest of
        /// the mod keeps working.</summary>
        void Patch()
        {
            foreach (Type t in AccessTools.GetTypesFromAssembly(typeof(Plugin).Assembly))
            {
                if (t.GetCustomAttributes(typeof(HarmonyPatch), false).Length == 0) continue;
                try
                {
                    harmony.CreateClassProcessor(t).Patch();
                    patched.Add(t);
                }
                catch (Exception e) { Log.LogWarning($"{t.Name} not applied, that feature is off: {e.Message}"); }
            }
        }

        Action<float>[] steps;
        static readonly string[] StepNames = { "screens", "screens restored", "saved animations", "sign moves" };
        readonly int[] failures = new int[StepNames.Length];
        readonly bool[] off = new bool[StepNames.Length];

        static string gameLanguage;
        static float gameLanguageAt;

        /// <summary>The game's language as Lang reads it (Localization is the main thread's): the same string while it does
        /// not change, so Lang's reference test holds.</summary>
        static void ReadGameLanguage()
        {
            gameLanguageAt = Time.unscaledTime + 1f;
            string now = Localization.instance?.GetSelectedLanguage();
            if (now != gameLanguage) gameLanguage = now;
        }

        void Update()
        {
            MainThread.Drain();
            float now = Time.unscaledTime;
            if (now >= gameLanguageAt) ReadGameLanguage();                  // the game's own setting, once a second
            if (steps == null) steps = new Action<float>[] { Screens.Step, Restore.Step, Stored.Step, SignMove.Step };
            for (int i = 0; i < steps.Length; i++) Safe(i, now);
        }

        /// <summary>Runs one subsystem's step. An exception is logged; after three in a row the step is off until the next
        /// start, with one error line, so the others keep running and the log does not fill with the same trace.</summary>
        void Safe(int which, float now)
        {
            if (off[which]) return;
            try
            {
                steps[which](now);
                failures[which] = 0;
            }
            catch (Exception e)
            {
                if (++failures[which] >= 3)
                {
                    off[which] = true;
                    Log.LogError($"{StepNames[which]}: failed three times in a row, off until the next start: {e}");
                }
                else Log.LogWarning($"{StepNames[which]}: {e.Message}");
            }
        }

        /// <summary>Unloaded (the game closes, or ScriptEngine reloads it in a running game: the new copy loads from
        /// BepInEx/scripts a frame later): everything it changed in the game goes back, so the new copy starts clean
        /// (0.20.135, the author: now let us build the OnDestroy solution mentioned). The window cleans up its own part (SignLinkWindow.OnDestroy).</summary>
        void OnDestroy()
        {
            try
            {
                Stored?.StopAll();                                          // the stored animations: the signs' texts back (the new copy starts them again)
            }
            catch (Exception e) { Log.LogDebug($"unload: {e.Message}"); }
            Screens?.StopAll();
            ScreenPlayer.DropLitMaterial();
            harmony?.UnpatchSelf();
            Log.LogInfo($"{Name} {Version} unloaded");
        }
    }
}
