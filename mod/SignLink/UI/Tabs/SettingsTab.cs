using System;
using System.Collections.Generic;
using BepInEx.Configuration;
using SignLink.Pictures;
using SignLink.Signs;
using UnityEngine;
using PetyusPolisz.Kit;

namespace SignLink.UI
{
    /// <summary>The window (language, look, text size, opacity, size, the key that opens it), the sign options (the two
    /// highlights), how pictures are made (PNG transparency, Fine Width steps), playback (animations and moving GIFs, how
    /// far they play, the GIF memory), Reset to defaults and
    /// About.</summary>
    internal sealed class SettingsTab : Tab
    {
        static readonly string[] GifMemoryNames = { "256 MB", "512 MB", "1024 MB" };
        static readonly int[] GifMemoryMB = { 256, 512, 1024 };

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

        /// <summary>The hotkey's frame was clicked (Change key until 1.1.0): the next key pressed (with Ctrl, Shift or Alt held) opens and closes the
        /// window from then on; Esc keeps the old one. The window's own key does not toggle it meanwhile.</summary>
        public static bool CapturingKey { get; private set; }

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

        static string[] langNames, langChoices;
        // the looks by their names in the language in use, in Theme.Names' order
        static readonly ChoiceNames ThemeNames = new ChoiceNames("Classic", "Nordic");   // lang:keys
        static string langNamesIn;

        /// <summary>The Language row's names: Game default in the language shown, the languages by their own names; made again
        /// only when the language or the list changes.</summary>
        static string[] LanguageNames()
        {
            string[] choices = Lang.Choices;
            string now = Lang.Current;
            if (langNames != null && ReferenceEquals(choices, langChoices) && now == langNamesIn) return langNames;
            langChoices = choices;
            langNamesIn = now;
            langNames = Array.ConvertAll(choices, Lang.Name);              // Game default in the words in use, a table's language by its own name
            return langNames;
        }

        static int Nearest(float[] values, float v)
        {
            int best = 0;
            for (int i = 1; i < values.Length; i++)
                if (Math.Abs(values[i] - v) < Math.Abs(values[best] - v)) best = i;
            return best;
        }

        public SettingsTab(SignLinkWindow w) : base(w) { }

        public override string Title => Lang.T("Settings");

        /// <summary>A slider let go by closing the window still counts.</summary>
        public override void OnClose()
        {
            if (PreviewOpacity.HasValue) PluginConfig.MenuOpacity.Value = Mathf.Clamp(PreviewOpacity.Value, 0.5f, 1f);
            PreviewOpacity = null;
            KeepSize();
            // a value typed and left by closing the window (Esc) counts too, and none waits to be written later (0.20.130)
            if (distanceDirty && !float.IsNaN(distance) && Math.Abs(distance - PluginConfig.PlayDistance.Value) > 0.001f)
                PluginConfig.PlayDistance.Value = distance;
            distanceDirty = false;
            distance = float.NaN;
            StopCapture();
        }

        public override void Body()
        {
            Widgets.Header(Lang.T("Window"), Widgets.WheelKey);
            using (Widgets.Vertical(Theme.Section))
            {
                // the rows in an order of their own (1.1.0, the author: reorder this group): the three lists first (Language,
                // Theme, Text size, as drop-down lists), then Opacity, then the two rows with a button (Window size, Hotkey)
                bool wasOn = GUI.enabled;
                // the window's language (1.1.0, the author: Game default, the game's language, English where SignLink has no
                // table for it): the words change at the next event
                int li = Math.Max(0, Array.IndexOf(Lang.Choices, PluginConfig.MenuLanguage.Value));
                int lp = Widgets.DropChoice(Lang.T("Language"), "set.language", li, LanguageNames(), 240f);
                if (lp != li && lp >= 0) Window.Later(() => PluginConfig.MenuLanguage.Value = Lang.Choices[lp]);
                // the look (1.1.0, the author: keep both, Classic and Nordic, with a picker in Settings): built at the next Layout
                int ti = Array.IndexOf(Theme.Names, PluginConfig.MenuTheme.Value);
                if (ti < 0) ti = 0;                                      // the default, first
                int tp = Widgets.DropChoice(Lang.T("Theme"), "set.theme", ti, ThemeNames.Shown, 240f);
                if (tp != ti) Window.Later(() => PluginConfig.MenuTheme.Value = Theme.Names[tp]);
                float[] scales = SignLinkWindow.Scales;
                int si = Nearest(scales, PluginConfig.MenuScale.Value);
                int pick = Widgets.DropChoice(Lang.T("Text size"), "set.scale", si, ScaleNames(scales), 240f);
                if (pick != si) Window.Later(() => PluginConfig.MenuScale.Value = scales[pick]);
                float shownOpacity = PreviewOpacity ?? PluginConfig.MenuOpacity.Value;
                float opacity = Widgets.Number("set.opacity", Lang.T("Opacity"), Mathf.Round(shownOpacity * 100f), 50f, 100f, 5f, "%", "0") / 100f;
                // the slider and the field report a change in their own event (a drag, a key), never in Layout: take it
                // there, so the next event shows it (Theme.Ensure); the config takes it once nothing is held or focused
                if (Mathf.Abs(opacity - shownOpacity) > 0.0005f) PreviewOpacity = opacity;
                if (Event.current.type == EventType.Layout && PreviewOpacity.HasValue &&
                    GUIUtility.hotControl == 0 && GUIUtility.keyboardControl == 0 && Widgets.StillFor(0.5f))
                {
                    PluginConfig.MenuOpacity.Value = Mathf.Clamp(PreviewOpacity.Value, 0.5f, 1f);   // let go: kept
                    PreviewOpacity = null;
                }
                // the window's size: by its edges and corners (0.20.50); the rows that set it in percent went (0.20.53, the
                // author: drop the sliders ... we can resize it by hand now); Default size stays, in the row at its right
                // (1.1.0, the author: one row, the button floating right)
                float setW = PreviewWidth ?? PluginConfig.MenuWidth.Value, setH = PreviewHeight ?? PluginConfig.MenuHeight.Value;
                bool custom = setW > 0f || Mathf.Abs(setH - (float)PluginConfig.MenuHeight.DefaultValue) > 0.001f;
                Vector2 now = Window.Size;
                using (Widgets.Horizontal())
                {
                    GUILayout.Label(Lang.T("Window size"), Theme.Label, Widgets.W(Widgets.LabelW));
                    GUILayout.Label($"{now.x:0} x {now.y:0} px", Theme.Label, GUILayout.ExpandWidth(false));
                    GUILayout.FlexibleSpace();
                    GUI.enabled = wasOn && custom;
                    if (Widgets.Button(Lang.T("Default size"), Theme.Button, Widgets.W(150)))
                        Window.Later(() =>
                        {
                            PreviewWidth = PreviewHeight = null;
                            PluginConfig.MenuWidth.Value = 0f;              // automatic: fits any screen shape
                            PluginConfig.MenuHeight.Value = (float)PluginConfig.MenuHeight.DefaultValue;
                        });
                    GUI.enabled = wasOn;
                }
                bool noKey = PluginConfig.MenuKey.Value.MainKey == KeyCode.None;
                // one row (1.1.0, the author: the Change key button was bad UX): the key in its frame is the button that changes
                // it (a click, then the new key; a click again keeps the old), Unbind at the row's right as Default size
                using (Widgets.Horizontal())
                {
                    GUILayout.Label(Lang.T("Hotkey"), Theme.Label, Widgets.W(Widgets.LabelW));
                    bool change = CapturingKey || noKey
                        ? Widgets.Button(Lang.T(CapturingKey ? "press a key ..." : "no key"), Theme.KeyButton, GUILayout.ExpandWidth(false))
                        : Widgets.KeyCapButtons(PluginConfig.MenuKeyParts());       // "Shift" + "Page Up", each in a small frame (0.20.82)
                    if (change)
                        Window.Later(() =>
                        {
                            CapturingKey = !CapturingKey;
                            Widgets.DropFocus();                            // a focused field would eat the key
                        });
                    GUILayout.FlexibleSpace();
                    GUI.enabled = wasOn && !noKey && !CapturingKey;
                    if (Widgets.Button(Lang.T("Unbind"), Theme.Button, Widgets.W(150)))
                        Window.Later(() => PluginConfig.MenuKey.Value = KeyboardShortcut.Empty);
                    GUI.enabled = wasOn;
                }
                if (CapturingKey) Capture();
                // only while a key is chosen: the other ways to open the window are in About (0.20.97, the author: "opens and
                // closes the window" is a useless row too; "smaller texts fits more" ... delete this row)
                if (CapturingKey) Widgets.Hint(Lang.T("Press a key · Esc keeps the old"));
            }

            // Sign options before Pictures (0.20.74, the author: swap the Pictures and Sign options groups in Settings)
            Widgets.Header(Lang.T("Sign options"));                               // no slider left in it (1.1.0: the re-read pace went)
            using (Widgets.Vertical(Theme.Section))
            {
                // its own row with On / Off (0.20.36): a long toggle was switched off by a stray click, and the selected
                // sign lost its frame without a word
                bool lit = Widgets.OnOff(Lang.T("Highlight signs"), PluginConfig.Highlight.Value, Theme.Tab);
                if (lit != PluginConfig.Highlight.Value) Window.Later(() => PluginConfig.Highlight.Value = lit);
                // the content's frame apart (0.20.76, the author: a Highlight sign contents button too, like Highlight signs, on
                // by default), and on its own (1.1.0, the author: Highlight signs switched it off; two switches, one does this,
                // the other that): Highlight signs is the board's frame, this the content's
                bool framed = Widgets.OnOff(Lang.T("Highlight sign contents"), PluginConfig.HighlightContent.Value, Theme.Tab);
                if (framed != PluginConfig.HighlightContent.Value) Window.Later(() => PluginConfig.HighlightContent.Value = framed);
            }

            Widgets.Header(Lang.T("Pictures"));
            using (Widgets.Vertical(Theme.Section))
            {
                PluginConfig.PngTransparency.Value = Widgets.Check(PluginConfig.PngTransparency.Value,
                    Lang.T("PNG transparency"));
                PluginConfig.PictureFineWidth.Value = Widgets.Check(PluginConfig.PictureFineWidth.Value,
                    Lang.T("Fine Width steps: exact sizes, your Resolution kept"));
                // what is worth knowing, in one line (0.20.76, the author: take out this awful "for every picture placed
                // from now on" text, or rewrite it into something truly useful and meaningful)
            }

            Widgets.Header(Lang.T("Playback"), Widgets.WheelKey);                                    // from the Playing tab (0.20.54)
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
                float got = Widgets.Number("distance", Lang.T("Pause beyond"), distance, 8f, 200f, 1f, "m", "0");
                if (Math.Abs(got - distance) > 0.001f)
                {
                    distance = got;
                    distanceDirty = true;
                }
                // the frames the moving GIFs may hold here (1.0.2, the author: 512 and 1024 too)
                int mem = Array.IndexOf(GifMemoryMB, PluginConfig.GifMemory.Value);
                int pickMem = Widgets.Choice(Lang.T("GIF memory"), mem < 0 ? 0 : mem, GifMemoryNames, 330f);
                if (pickMem != Math.Max(0, mem)) Window.Later(() => PluginConfig.GifMemory.Value = GifMemoryMB[pickMem]);
                Widgets.Hint(Lang.T("RAM for moving GIFs; 1024 wants 16 GB"));
            }

            // a small table, one thing a row (0.20.97, the author: the /signlink info belongs elsewhere ... it can go to About,
            // and there let us tidy up and structure its texts): every way to open the window lives here
            // every setting back to how SignLink comes, on a second click (0.20.121, the author: Settings needs a Reset to
            // defaults button too, also with a confirmation)
            Widgets.Header(Lang.T("Reset"));
            using (Widgets.Horizontal())
            {
                if (Armed.Button(Window, "settings.reset", Lang.T("Reset to defaults"), Lang.T("Sure? Click again"), Theme.Button, Widgets.W(220)))
                    Window.Act(() =>
                    {
                        int n = PluginConfig.ResetToDefaults();
                        PreviewOpacity = null;                              // nothing held over the new values
                        PreviewWidth = PreviewHeight = null;
                        CapturingKey = false;
                        distance = float.NaN;
                        distanceDirty = false;
                        return n == 0 ? Lang.T("every setting is at its default already.")
                             : n == 1 ? Lang.T("1 setting back to the default.") : Lang.F("{0} settings back to the default.", n);
                    });
                GUILayout.FlexibleSpace();
            }

            Widgets.Header(Lang.T("About"));
            using (Widgets.Vertical(Theme.Section))
            {
                GUILayout.Label($"{Plugin.Name} {Plugin.Version}", Theme.Label);
                Widgets.Space(4);
                // the tabs' own titles, so the steps read as the tab strip does
                Widgets.Field(Lang.T("Steps"), Lang.T("1 Sign") + "  ›  " + Lang.T("2 Content") + "  ›  " + Lang.T("3 Place"));
                Widgets.Space(4);
                bool keyless = PluginConfig.MenuKey.Value.MainKey == KeyCode.None;
                Widgets.Field(Lang.T("Open it"), keyless ? Lang.T("no hotkey") : PluginConfig.MenuKeyText());
                Widgets.Field("", Lang.T("/signlink in the chat"));
                Widgets.Field("", Lang.T("E on a picture sign"));
                Widgets.Field("", Lang.T("Open in SignLink (sign editor)"));
                Widgets.Space(4);
                Widgets.Field(Lang.T("Fine-tune"), Lang.T("Shift+wheel on a slider"));
                Widgets.Field("", Lang.T("arrow keys (3 Place)"));
                if (Plugin.Patched(typeof(Patches.LookCursorPatch)) && Plugin.Patched(typeof(Patches.LookPatch)))   // only where it works
                    Widgets.Field(Lang.T("Look around"), Lang.T("hold the right mouse button"));   // off the window (RightLook, 1.1.0)
                Widgets.Space(4);
                // 1.1.0, the author: recommend turning Bloom off (the game's Graphics setting: glowing pictures blur with it)
                Widgets.Field(Lang.T("Graphics tip"), Lang.T("Bloom off: sharper pictures"));
                Widgets.Space(4);
                using (Widgets.Horizontal())                                // the folder opened, as the Picture list does
                {                                                           // (0.20.119, the author: make it a button ... Open folder)
                    GUILayout.Label(Lang.T("Pictures"), Theme.Small, Widgets.W(110));
                    if (Widgets.Button(Lang.T("Open folder"), Theme.Button, Widgets.W(110))) PictureImporter.OpenFolder();
                }
            }
        }
    }
}
