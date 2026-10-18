using System;
using System.Collections.Generic;
using BepInEx.Configuration;
using SignLink.Pictures;
using SignLink.Signs;
using UnityEngine;
using PetyusPolisz.Kit;

namespace SignLink.UI
{
    /// <summary>Window size, text size, opacity and the key that opens the window, how pictures are made (PNG
    /// transparency, moving GIFs), the sign options (highlight, the game's editor kept off pictures, the re-read pace),
    /// and the developer API (off by default; switches at once).</summary>
    internal sealed class SettingsTab : Tab
    {
        static readonly string[] GifMemoryNames = { "256 MB", "512 MB", "1024 MB" };
        static readonly int[] GifMemoryMB = { 256, 512, 1024 };

        bool showPassword;
        // the Re-read row (Sign options): the field's value and whether it waits to be written (see Playing's Pause beyond)
        float refresh = float.NaN;
        bool refreshDirty;

        /// <summary>The Pause beyond row (moved from Playing, 0.20.54): the config is written once a new value is set and
        /// nothing is held or focused (typing "150" does not save 8 and 15 on the way); otherwise the field reads the
        /// config (a value set in ConfigurationManager stays).</summary>
        float distance = float.NaN;
        bool distanceDirty;

        /// <summary>The Opacity slider's value while it is dragged or typed: the window shows it at once (Theme.Ensure),
        /// the config takes it once the slider is let go (a save per step would write the file all through the drag).</summary>
        public static float? PreviewOpacity { get; private set; }

        /// <summary>Width and Height while an edge or a corner of the window is dragged: the window takes them at once, the
        /// config once let go (KeepSize).</summary>
        public static float? PreviewWidth { get; private set; }
        public static float? PreviewHeight { get; private set; }

        public static void Resize(float width, float height)
        {
            PreviewWidth = width <= 0f ? 0f : WindowSizing.Clamp(width, WindowSizing.WidthLeast, WindowSizing.WidthMost);   // 0: automatic
            PreviewHeight = WindowSizing.Clamp(height, WindowSizing.HeightLeast, WindowSizing.HeightMost);
        }

        public static void KeepSize()
        {
            if (PreviewWidth.HasValue) PluginConfig.MenuWidth.Value = PreviewWidth.Value <= 0f ? 0f
                                                                     : WindowSizing.Clamp(PreviewWidth.Value, WindowSizing.WidthLeast, WindowSizing.WidthMost);
            if (PreviewHeight.HasValue) PluginConfig.MenuHeight.Value = WindowSizing.Clamp(PreviewHeight.Value, WindowSizing.HeightLeast, WindowSizing.HeightMost);
            PreviewWidth = PreviewHeight = null;
        }

        /// <summary>Change key was pressed: the next key pressed (with Ctrl, Shift or Alt held) opens and closes the
        /// window from then on; Esc keeps the old one. The window's own key does not toggle it meanwhile.</summary>
        public static bool CapturingKey { get; private set; }

        /// <summary>About's "E on a sign" row, as the switch was at Layout (Widgets.Check flips it mid-frame).</summary>

        public static void StopCapture() => CapturingKey = false;

        static readonly KeyCode[] ModifierKeys =
        {
            KeyCode.LeftControl, KeyCode.RightControl, KeyCode.LeftShift, KeyCode.RightShift, KeyCode.LeftAlt, KeyCode.RightAlt,
            KeyCode.LeftCommand, KeyCode.RightCommand, KeyCode.LeftWindows, KeyCode.RightWindows,
        };

        /// <summary>The key pressed while capturing (an IMGUI key event in the window), as the new shortcut.</summary>
        void Capture()
        {
            Event e = Event.current;
            if (e.type != EventType.KeyDown || e.keyCode == KeyCode.None || Array.IndexOf(ModifierKeys, e.keyCode) >= 0) return;
            if (e.keyCode == KeyCode.Escape)
            {
                Window.Later(StopCapture);
                e.Use();
                return;
            }
            var mods = new List<KeyCode>();
            if (e.control) mods.Add(KeyCode.LeftControl);
            if (e.shift) mods.Add(KeyCode.LeftShift);
            if (e.alt) mods.Add(KeyCode.LeftAlt);
            KeyCode main = e.keyCode;
            Window.Later(() =>
            {
                PluginConfig.MenuKey.Value = new KeyboardShortcut(main, mods.ToArray());
                CapturingKey = false;
            });
            e.Use();
        }

        static string[] scaleNames;

        /// <summary>The text sizes' buttons ("0.8x" ...), made once (the scales are fixed).</summary>
        static string[] ScaleNames(float[] scales) => scaleNames ?? (scaleNames = Array.ConvertAll(scales, x => $"{x:0.##}x"));

        static int Nearest(float[] values, float v)
        {
            int best = 0;
            for (int i = 1; i < values.Length; i++)
                if (Math.Abs(values[i] - v) < Math.Abs(values[best] - v)) best = i;
            return best;
        }

        public SettingsTab(SignLinkWindow w) : base(w) { }

        public override string Title => "Settings";

        static string lanAddress;

        /// <summary>This computer's address on the local network, for the API line (0.20.151, review: it read a literal
        /// "&lt;this computer's address&gt;"); found once.</summary>
        static string LanAddress()
        {
            if (lanAddress != null) return lanAddress;
            lanAddress = "this computer's network address";
            try
            {
                foreach (System.Net.IPAddress a in System.Net.Dns.GetHostAddresses(System.Net.Dns.GetHostName()))
                    if (a.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork && !System.Net.IPAddress.IsLoopback(a))
                    {
                        lanAddress = a.ToString();
                        break;
                    }
            }
            catch (Exception) { }
            return lanAddress;
        }

        public override void OnOpen() => showPassword = false;

        /// <summary>A slider let go by closing the window still counts.</summary>
        public override void OnClose()
        {
            if (PreviewOpacity.HasValue) PluginConfig.MenuOpacity.Value = Mathf.Clamp(PreviewOpacity.Value, 0.5f, 1f);
            PreviewOpacity = null;
            KeepSize();
            // a value typed and left by closing the window (Esc) counts too, and none waits to be written later (0.20.130)
            if (refreshDirty && !float.IsNaN(refresh) && Math.Abs(refresh - PluginConfig.RefreshSeconds.Value) > 0.001f)
                PluginConfig.RefreshSeconds.Value = refresh;
            if (distanceDirty && !float.IsNaN(distance) && Math.Abs(distance - PluginConfig.PlayDistance.Value) > 0.001f)
                PluginConfig.PlayDistance.Value = distance;
            refreshDirty = distanceDirty = false;
            refresh = distance = float.NaN;
            StopCapture();
        }

        public override void Body()
        {
            Widgets.Header("Window", Widgets.WheelKey);
            using (Widgets.Vertical(Theme.Section))
            {
                // the window's size: by its edges and corners (0.20.50); the rows that set it in percent went (0.20.53, the
                // author: drop the sliders ... we can resize it by hand now); Default size stays
                float setW = PreviewWidth ?? PluginConfig.MenuWidth.Value, setH = PreviewHeight ?? PluginConfig.MenuHeight.Value;
                Vector2 now = Window.Size;
                bool wasOn = GUI.enabled;
                // a narrow window puts a row's buttons under it, so nothing runs out of the window (0.20.74, the author:
                // in a narrow window things do not wrap and run off the screen ... fix the responsiveness)
                bool narrow = Widgets.Narrow;
                bool custom = setW > 0f || Mathf.Abs(setH - (float)PluginConfig.MenuHeight.DefaultValue) > 0.001f;
                void DefaultSize()
                {
                    GUI.enabled = wasOn && custom;
                    if (Widgets.Button("Default size", Theme.Button, Widgets.W(180)))
                        Window.Later(() =>
                        {
                            PreviewWidth = PreviewHeight = null;
                            PluginConfig.MenuWidth.Value = 0f;              // automatic: fits any screen shape
                            PluginConfig.MenuHeight.Value = (float)PluginConfig.MenuHeight.DefaultValue;
                        });
                    GUI.enabled = wasOn;
                }
                using (Widgets.Horizontal())
                {
                    GUILayout.Label("Window size", Theme.Label, Widgets.W(Widgets.LabelW));
                    GUILayout.Label($"{now.x:0} x {now.y:0} px", Theme.Label);
                    if (!narrow) DefaultSize();
                }
                if (narrow) DefaultSize();                                  // under the row, from its start
                float[] scales = SignLinkWindow.Scales;
                int si = Nearest(scales, PluginConfig.MenuScale.Value);
                int pick;
                if (narrow)                                                 // the sizes under their label, the row's whole width
                {
                    GUILayout.Label("Text size", Theme.Label);
                    pick = Widgets.Toolbar(si, ScaleNames(scales), Theme.Tab, Widgets.W(Widgets.ContentWidth - 30));
                }
                else
                    pick = Widgets.Choice("Text size", si, ScaleNames(scales), 0f, Theme.Tab);
                if (pick != si) Window.Later(() => PluginConfig.MenuScale.Value = scales[pick]);
                float shownOpacity = PreviewOpacity ?? PluginConfig.MenuOpacity.Value;
                float opacity = Widgets.Number("set.opacity", "Opacity", Mathf.Round(shownOpacity * 100f), 50f, 100f, 5f, "%", "0") / 100f;
                // the slider and the field report a change in their own event (a drag, a key), never in Layout: take it
                // there, so the next event shows it (Theme.Ensure); the config takes it once nothing is held or focused
                if (Mathf.Abs(opacity - shownOpacity) > 0.0005f) PreviewOpacity = opacity;
                if (Event.current.type == EventType.Layout && PreviewOpacity.HasValue &&
                    GUIUtility.hotControl == 0 && GUIUtility.keyboardControl == 0 && Widgets.StillFor(0.5f))
                {
                    PluginConfig.MenuOpacity.Value = Mathf.Clamp(PreviewOpacity.Value, 0.5f, 1f);   // let go: kept
                    PreviewOpacity = null;
                }
                bool noKey = PluginConfig.MenuKey.Value.MainKey == KeyCode.None;
                void KeyButtons()
                {
                    if (Widgets.Button(CapturingKey ? "Cancel" : "Change key", Theme.Button, Widgets.W(140)))
                        Window.Later(() =>
                        {
                            CapturingKey = !CapturingKey;
                            Widgets.DropFocus();                            // a focused field would eat the key
                        });
                    GUI.enabled = !noKey && !CapturingKey;
                    if (Widgets.Button("No key", Theme.Button, Widgets.W(100)))
                        Window.Later(() => PluginConfig.MenuKey.Value = KeyboardShortcut.Empty);
                    GUI.enabled = true;
                }
                using (Widgets.Horizontal())
                {
                    GUILayout.Label("Hotkey", Theme.Label, Widgets.W(Widgets.LabelW));
                    if (CapturingKey || noKey) GUILayout.Label(CapturingKey ? "press a key ..." : "no key", Theme.Label);
                    else Widgets.KeyCaps(PluginConfig.MenuKeyParts());               // "Shift" + "Page Up", each in a small frame (0.20.82)
                    if (!narrow) KeyButtons();
                }
                if (narrow)
                    using (Widgets.Horizontal())
                        KeyButtons();                                       // under the row, from its start
                if (CapturingKey) Capture();
                // only while a key is chosen: the other ways to open the window are in About (0.20.97, the author: "opens and
                // closes the window" is a useless row too; "smaller texts fits more" ... delete this row)
                if (CapturingKey) Widgets.Hint("Press a key · Esc keeps the old");
            }

            // Sign options before Pictures (0.20.74, the author: swap the Pictures and Sign options groups in Settings)
            Widgets.Header("Sign options", Widgets.WheelKey);
            using (Widgets.Vertical(Theme.Section))
            {
                // its own row with On / Off (0.20.36): a long toggle was switched off by a stray click, and the selected
                // sign lost its frame without a word
                bool lit = Widgets.OnOff("Highlight signs", PluginConfig.Highlight.Value, Theme.Tab);
                if (lit != PluginConfig.Highlight.Value) Window.Later(() => PluginConfig.Highlight.Value = lit);
                // the content's frame apart (0.20.76, the author: a Highlight sign contents button too, like
                // Highlight signs, on by default): it needs Highlight signs, so it is grey without
                bool lighting = GUI.enabled;
                GUI.enabled = lighting && PluginConfig.Highlight.Value;
                bool framed = Widgets.OnOff("Highlight sign contents", PluginConfig.HighlightContent.Value, Theme.Tab);
                GUI.enabled = lighting;
                if (framed != PluginConfig.HighlightContent.Value) Window.Later(() => PluginConfig.HighlightContent.Value = framed);
                // the re-read pace: the config is written once a new value is set and nothing is held or focused
                bool held = GUIUtility.hotControl != 0 || GUIUtility.keyboardControl != 0;
                if (Event.current.type == EventType.Layout && !held && Widgets.StillFor(0.5f))
                {
                    if (refreshDirty && !float.IsNaN(refresh) && Math.Abs(refresh - PluginConfig.RefreshSeconds.Value) > 0.001f)
                        PluginConfig.RefreshSeconds.Value = refresh;
                    refreshDirty = false;
                    refresh = PluginConfig.RefreshSeconds.Value;
                }
                if (float.IsNaN(refresh)) refresh = PluginConfig.RefreshSeconds.Value;
                float every = Widgets.Number("refresh", "Re-read changed signs every", refresh, 0.1f, 2f, 0.1f, "s", "0.0");
                if (Math.Abs(every - refresh) > 0.001f)
                {
                    refresh = every;
                    refreshDirty = true;
                }
                Widgets.Hint(refresh >= 1.95f ? "2 s is the default" : "Faster, costs more");
            }

            Widgets.Header("Pictures");
            using (Widgets.Vertical(Theme.Section))
            {
                PluginConfig.PngTransparency.Value = Widgets.Check(PluginConfig.PngTransparency.Value,
                    "PNG transparency");
                PluginConfig.PictureFineWidth.Value = Widgets.Check(PluginConfig.PictureFineWidth.Value,
                    "Fine Width steps: exact sizes, your Resolution kept");
                // what is worth knowing, in one line (0.20.76, the author: take out this awful "for every picture placed
                // from now on" text, or rewrite it into something truly useful and meaningful)
            }

            Widgets.Header("Playback", Widgets.WheelKey);                                     // from the Playing tab (0.20.54)
            using (Widgets.Vertical(Theme.Section))
            {
                // pausing them all is on the Playing tab, a button for GIFs and one for glyph animations (0.20.163, the author);
                // whether a GIF goes on moving is the picture's own choice (2 Content › Moving)
                bool holding = GUIUtility.hotControl != 0 || GUIUtility.keyboardControl != 0;
                if (Event.current.type == EventType.Layout && !holding && Widgets.StillFor(0.5f))
                {
                    if (distanceDirty && !float.IsNaN(distance) && Math.Abs(distance - PluginConfig.PlayDistance.Value) > 0.001f)
                        PluginConfig.PlayDistance.Value = distance;          // the config file is written once per change
                    distanceDirty = false;
                    distance = PluginConfig.PlayDistance.Value;
                }
                if (float.IsNaN(distance)) distance = PluginConfig.PlayDistance.Value;
                float got = Widgets.Number("distance", "Pause beyond", distance, 8f, 200f, 1f, "m", "0");
                if (Math.Abs(got - distance) > 0.001f)
                {
                    distance = got;
                    distanceDirty = true;
                }
                // the frames the moving GIFs may hold here (1.0.2, the author: 512 and 1024 too)
                int mem = Array.IndexOf(GifMemoryMB, PluginConfig.GifMemory.Value);
                int pickMem = Widgets.Choice("GIF memory", mem < 0 ? 0 : mem, GifMemoryNames, 330f);
                if (pickMem != Math.Max(0, mem)) Window.Later(() => PluginConfig.GifMemory.Value = GifMemoryMB[pickMem]);
                Widgets.Hint("RAM for moving GIFs; 1024 wants 16 GB");
            }

            Widgets.Header("Developer API");
            using (Widgets.Vertical(Theme.Section))
            {
                Widgets.Hint("A local HTTP API for tools that write signs; players do not need it");
                bool on = Widgets.Check(PluginConfig.ApiEnabled.Value, "Turn the API on");
                if (on != PluginConfig.ApiEnabled.Value) Window.Later(() => PluginConfig.ApiEnabled.Value = on);
                GUILayout.Label(!PluginConfig.ApiEnabled.Value ? "Disabled."
                    : Plugin.ApiRunning ? $"Listening on http://{(PluginConfig.ApiNetwork.Value ? LanAddress() : "127.0.0.1")}:{PluginConfig.ApiPort.Value} " +
                                          (PluginConfig.ApiNetwork.Value ? "(your network too)." : "(this computer only).")
                    : Plugin.ApiProblem ?? "Starting ...",
                    PluginConfig.ApiEnabled.Value && !Plugin.ApiRunning ? Theme.Warn : Theme.Label);
                GUI.enabled = PluginConfig.ApiEnabled.Value;
                bool net = Widgets.Check(PluginConfig.ApiNetwork.Value, "Also this network (unencrypted)");
                if (net != PluginConfig.ApiNetwork.Value) Window.Later(() => PluginConfig.ApiNetwork.Value = net);
                bool hasPassword = !string.IsNullOrEmpty(PluginConfig.ApiPassword.Value);
                string password = "Password: " + (!hasPassword ? "(not made yet)"
                                                   : showPassword ? PluginConfig.ApiPassword.Value : new string('*', 12));
                if (Widgets.Narrow) GUILayout.Label(password, Theme.Label);  // a narrow window: the buttons under it (0.20.74)
                using (Widgets.Horizontal())
                {
                    if (!Widgets.Narrow) GUILayout.Label(password, Theme.Label, Widgets.W(Widgets.ContentWidth - 250));
                    GUI.enabled = PluginConfig.ApiEnabled.Value && hasPassword;
                    if (Widgets.Button(showPassword ? "Hide" : "Show", Theme.Button, Widgets.W(64))) showPassword = !showPassword;
                    if (Widgets.Button("Copy", Theme.Button, Widgets.W(64)))
                        Window.Act(() =>
                        {
                            string pw = PluginConfig.ApiPassword.Value;
                            GUIUtility.systemCopyBuffer = pw;
                            Plugin.PasswordCopied(pw);                      // taken off the clipboard again in 30 s (0.20.132, review)
                            return "copied the API password to the clipboard: it is taken off it again in 30 s.";
                        });
                    GUI.enabled = PluginConfig.ApiEnabled.Value;
                    if (Widgets.Button("New", Theme.Button, Widgets.W(64)))
                        Window.Act(() =>
                        {
                            PluginConfig.ApiPassword.Value = PluginConfig.NewPassword();
                            return "made a new API password: tools need the new one now.";
                        });
                }
                GUI.enabled = true;
            }

            // a small table, one thing a row (0.20.97, the author: the /signlink info belongs elsewhere ... it can go to About,
            // and there let us tidy up and structure its texts): every way to open the window lives here
            // every setting back to how SignLink comes, on a second click (0.20.121, the author: Settings needs a Reset to
            // defaults button too, also with a confirmation)
            Widgets.Header("Reset");
            using (Widgets.Horizontal())
            {
                if (Armed.Button(Window, "settings.reset", "Reset to defaults", "Sure? Click again", Theme.Button, Widgets.W(220)))
                    Window.Act(() =>
                    {
                        int n = PluginConfig.ResetToDefaults();
                        PreviewOpacity = null;                              // nothing held over the new values
                        PreviewWidth = PreviewHeight = null;
                        CapturingKey = false;
                        refresh = distance = float.NaN;
                        refreshDirty = distanceDirty = false;
                        return n == 0 ? "every setting is at its default already." : $"{n} setting{(n == 1 ? "" : "s")} back to the default.";
                    });
                GUILayout.FlexibleSpace();
            }
            Widgets.Hint("Password stays");

            Widgets.Header("About");
            using (Widgets.Vertical(Theme.Section))
            {
                GUILayout.Label($"{Plugin.Name} {Plugin.Version}", Theme.Label);
                Widgets.Space(4);
                Widgets.Field("Steps", "1 Sign  ›  2 Content  ›  3 Place");
                Widgets.Space(4);
                bool keyless = PluginConfig.MenuKey.Value.MainKey == KeyCode.None;
                Widgets.Field("Open it", keyless ? "no hotkey" : PluginConfig.MenuKeyText());
                Widgets.Field("", "/signlink in the chat");
                Widgets.Field("", "E on a picture sign");
                Widgets.Field("", "Open in SignLink (sign editor)");
                Widgets.Space(4);
                Widgets.Field("Fine-tune", "Shift+wheel on a slider");
                Widgets.Field("", "arrow keys (3 Place)");
                Widgets.Space(4);
                using (Widgets.Horizontal())                                // the folder opened, as the Picture list does
                {                                                           // (0.20.119, the author: make it a button ... Open folder)
                    GUILayout.Label("Pictures", Theme.Small, Widgets.W(110));
                    if (Widgets.Button("Open folder", Theme.Button, Widgets.W(110))) PictureImporter.OpenFolder();
                }
            }
        }
    }
}
