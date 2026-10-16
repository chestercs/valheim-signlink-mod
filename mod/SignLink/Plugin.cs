// SignLink: write, animate and screen sign art in Valheim. Client-side BepInEx plugin, vanilla compatible.
// Layout: Plugin (wiring) / PluginConfig / Api (local HTTP) / Signs (sign helpers, stacks, building) / Pictures
// (PNG/JPG to sign text) / Animation (text frames, texture screens) / Patches / Presets / UI (the window).
// Endpoints are listed in Api/Routes.cs, background in mod/README.md.
using System;
using System.Collections.Generic;
using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using SignLink.Animation;
using SignLink.Api;
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
        public const string Version = "1.0.1";

        internal static ManualLogSource Log;
        internal static TextAnimator Text;
        internal static ScreenPlayer Screens;
        internal static ScreenRestore Restore;
        internal static StoredAnims Stored;
        internal static UI.ContentPreview Preview;

        Harmony harmony;
        LocalHttpServer server;
        FastRefresh refresh;

        void Awake()
        {
            Log = Logger;
            MainThread.Report = e => Log.LogError(e);
            PluginConfig.Bind(Config);
            harmony = new Harmony(Guid);
            Patch();
            SignFinder.Seed();                                              // loaded again in a running game: its signs (0.20.135)
            Text = new TextAnimator();
            Screens = new ScreenPlayer();
            Restore = new ScreenRestore();
            Stored = new StoredAnims();
            Preview = new UI.ContentPreview();
            refresh = new FastRefresh(Text);
            gameObject.AddComponent<UI.SignLinkWindow>();
            instance = this;
            ApplyApi();
            PluginConfig.ApiEnabled.SettingChanged += (_, __) => ApplyApi();
            PluginConfig.ApiNetwork.SettingChanged += (_, __) => { StopApi(); ApplyApi(); };
            PluginConfig.ApiPort.SettingChanged += (_, __) => { StopApi(); ApplyApi(); };
            PluginConfig.ApiPassword.SettingChanged += (_, __) =>
            {
                string why = PluginConfig.PasswordProblem();                 // a bad password typed into the config: the API stops
                if (why != null && server != null)
                {
                    StopApi();
                    ApiProblem = $"Stopped: the new password {why} (New makes a good one).";
                }
                else if (server == null) ApplyApi();
            };
            Log.LogInfo($"{Name} {Version} loaded" + (server == null ? "" : $", developer API on port {PluginConfig.ApiPort.Value} " +
                        (PluginConfig.ApiNetwork.Value ? "(network: every interface)" : "(this computer only)")));
            Commands.Register();                                            // last, and guarded: a game update to the console
        }                                                                   // must not stop the rest of the plugin

        static Plugin instance;

        /// <summary>The developer API follows its switch at once (window or config), no restart needed.</summary>
        void ApplyApi()
        {
            if (PluginConfig.ApiEnabled.Value && server == null)
            {
                if (string.IsNullOrEmpty(PluginConfig.ApiPassword.Value))
                {
                    PluginConfig.ApiPassword.Value = PluginConfig.NewPassword();   // made on first use, not on every install
                    if (server != null) return;                                     // its SettingChanged started the API already
                }
                string why = PluginConfig.PasswordProblem();
                if (why != null)
                {
                    ApiProblem = $"Not started: the password {why} (New makes a good one).";
                    Log.LogError($"developer API not started: its password {why}");
                    return;
                }
                server = new LocalHttpServer(PluginConfig.ApiPort.Value, PluginConfig.ApiNetwork.Value, new Routes(Text, Screens).Handle);
                server.Start();
                ApiProblem = server.Running ? null
                    : $"Could not listen on port {PluginConfig.ApiPort.Value} (another program uses it? see the BepInEx log).";
            }
            else if (!PluginConfig.ApiEnabled.Value) StopApi();
        }

        /// <summary>Why the developer API is on but not listening (the Settings tab says it), or null.</summary>
        internal static string ApiProblem { get; private set; }

        void StopApi()
        {
            server?.Stop();
            server = null;
            ApiProblem = null;
        }

        /// <summary>Is the developer API listening (for the Settings tab)?</summary>
        internal static bool ApiRunning => instance && instance.server != null && instance.server.Running;

        static readonly HashSet<Type> patched = new HashSet<Type>();

        /// <summary>Did this patch class apply? (A feature that needs it falls back or stays off without it.)</summary>
        public static bool Patched(Type t) => patched.Contains(t);

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
        static readonly string[] StepNames = { "text animations", "screens", "screens restored", "saved animations", "fast refresh", "sign moves" };
        readonly int[] failures = new int[6];
        readonly bool[] off = new bool[6];

        static string copiedPassword;
        static float copiedUntil;

        /// <summary>Settings › Copy put the API password on the clipboard: 30 s later it is taken off again if the
        /// clipboard still holds exactly it (0.20.132, review: it stayed there for any program, or a Paste, to find).</summary>
        internal static void PasswordCopied(string password)
        {
            copiedPassword = string.IsNullOrEmpty(password) ? null : password;
            copiedUntil = Time.unscaledTime + 30f;
        }

        /// <summary>The copied password off the clipboard once its time is up (or the game closes), the clipboard read
        /// only then, once.</summary>
        static void ForgetCopiedPassword(float now, bool closing = false)
        {
            if (copiedPassword == null || (!closing && now < copiedUntil)) return;
            string pw = copiedPassword;
            copiedPassword = null;
            try
            {
                if (GUIUtility.systemCopyBuffer == pw) GUIUtility.systemCopyBuffer = "";
            }
            catch (Exception e) { Log.LogDebug($"the clipboard: {e.Message}"); }
        }

        void Update()
        {
            MainThread.Drain();
            float now = Time.unscaledTime;
            ForgetCopiedPassword(now);
            if (steps == null) steps = new Action<float>[] { Text.Step, Screens.Step, Restore.Step, Stored.Step, refresh.Step, SignMove.Step };
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
            ForgetCopiedPassword(0f, closing: true);
            server?.Stop();
            try
            {
                Text?.Stop(null);                                           // the API's animations: the signs' texts back
                Stored?.StopAll();                                          // stored ones too (the new copy starts them again)
            }
            catch (Exception e) { Log.LogDebug($"unload: {e.Message}"); }
            Screens?.StopAll();
            ScreenPlayer.DropLitMaterial();
            harmony?.UnpatchSelf();
            Log.LogInfo($"{Name} {Version} unloaded");
        }
    }
}
