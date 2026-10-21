using System;
using System.Collections.Generic;
using UnityEngine;

namespace PetyusPolisz.Kit
{
    /// <summary>The window's look (1.1.0, the author's Claude Design round): one set of shapes in two palettes, Nordic
    /// (Theme.Nordic.cs, slate and amber) and Classic (Theme.Classic.cs, dark wood and gold), painted over the styles
    /// Theme.Ensure builds (the author: one theme system, only the colours differ). Rounded, framed controls; tabs and
    /// chips in a framed track with an accent line under the chosen one; a diamond before each header, in capitals; a
    /// filled check box with a tick; the "way on" with a darker lip; a red armed button; the game's Averia Serif Libre.
    /// The textures are painted at the window's pixel scale (Paint, Painter), so corners and frames stay crisp from 1080p
    /// to 4K, with the backgrounds at the window's opacity. Should painting fail, the flat styles Ensure built stay
    /// (UseFlat) and LookError says why.</summary>
    internal static partial class Theme
    {
        public const string ClassicName = "Classic", NordicName = "Nordic";

        /// <summary>The looks in the order the Settings row shows them; the default first: a value not in the list (the
        /// config's AcceptableValueList) becomes the first.</summary>
        public static readonly string[] Names = { ClassicName, NordicName };       // Classic by default (1.1.0, the author)

        /// <summary>A painted look is built (false only when painting failed: the flat styles).</summary>
        public static bool Painted { get; private set; }

        /// <summary>Why the look could not be painted (null: it was).</summary>
        public static string LookError { get; private set; }

        /// <summary>The skin the window function draws with: the game's, with the look's own vertical scrollbar (IMGUI finds a
        /// scrollbar's thumb by name in the current skin); null: the game's skin as it is. KitWindow.Show sets it around
        /// GUILayout.Window, so no other mod's IMGUI sees it.</summary>
        public static GUISkin Skin { get; private set; }

        /// <summary>Behind a row of tabs or chips: the strip's buttons sit in it, the unselected ones without a background of
        /// their own (no background when flat).</summary>
        public static GUIStyle Track;

        /// <summary>A button armed by its first click ("Sure? Click again"): red, so it reads as a warning.</summary>
        public static GUIStyle ButtonWarn;

        /// <summary>A line that stands out without being a section's header (the target bar's sign, the pick hint).</summary>
        public static GUIStyle Lead;

        /// <summary>The diamond before a header, else null.</summary>
        public static Texture2D HeaderMark { get; private set; }

        /// <summary>A group heading's arrow (pointing right: closed), turned by Widgets.FoldHead; null: the flat look's glyphs.</summary>
        public static Texture2D FoldArrow { get; private set; }
        const int FoldArrowSize = 12;                                       // logical units

        /// <summary>The check box, drawn by Widgets.CheckBox at its own size in the middle of the row (Theme.Toggle keeps
        /// only the row's room: a box in its nine-sliced texture stretched with the row, and the tick drawn over it sat off
        /// its middle), and the tick over a ticked one; null when flat (Theme.Toggle draws its own).</summary>
        public static Texture2D CheckOff { get; private set; }
        public static Texture2D CheckOffHover { get; private set; }
        public static Texture2D CheckOn { get; private set; }
        public static Texture2D CheckOnHover { get; private set; }
        public static Texture2D Tick { get; private set; }

        /// <summary>The Meter's words, tinted by the bar's colour: white in a painted look, so the tint is the colour itself
        /// (a grey small label turned amber into brown).</summary>
        public static GUIStyle MeterWords;

        /// <summary>The title drawn once more under itself, a pixel down and right, in black; null: none.</summary>
        public static GUIStyle TitleShadow { get; private set; }

        /// <summary>The Meter's rounded track and fill (the fill white, tinted by the bar's colour); null: flat bars.</summary>
        public static GUIStyle MeterTrack { get; private set; }
        public static GUIStyle MeterFill { get; private set; }

        // before the properties that start from it: static fields are set in the order they are written (1.1.0, review: they
        // started see-through black)
        static readonly Color FlatRule = new Color(0.98f, 0.76f, 0.36f, 0.45f);

        /// <summary>Hand-drawn lines: a divider (Widgets.Rule), the title's rule, a resize edge and the corner grip.</summary>
        public static Color RuleColor { get; private set; } = FlatRule;
        public static Color TitleRuleColor { get; private set; } = FlatRule;
        public static float TitleRuleThickness { get; private set; } = 1.5f;
        public static Color EdgeColor { get; private set; } = new Color(0.98f, 0.76f, 0.36f, 1f);
        public static float EdgeThickness { get; private set; } = 3f;
        public static Color GripColor { get; private set; } = Color.white;
        public static Color GripHeld { get; private set; } = Color.white;

        /// <summary>The colours a rich-text row writes its dimmer part in, on its own row and on the selected one.</summary>
        public static string DimHex { get; private set; } = "#b3ab99";
        public static string DimOnHex { get; private set; } = "#4a3c22";

        /// <summary>Good, near the limit, over it, a plain figure, the track: the Meter's colours in this look.</summary>
        public static Color MeterGood { get; private set; } = new Color(0.55f, 0.85f, 0.45f, 1f);
        public static Color MeterNear { get; private set; } = new Color(0.98f, 0.76f, 0.36f);   // Amber (another file: no order)
        public static Color MeterOver { get; private set; } = new Color(1f, 0.45f, 0.35f, 1f);
        public static Color MeterInfo { get; private set; } = new Color(0.70f, 0.67f, 0.60f, 1f);
        public static Color MeterBack { get; private set; } = new Color(0.07f, 0.065f, 0.06f, 1f);

        /// <summary>Under a drop-down's open list: the panel's colour, opaque (the window's opacity would show what lies under it).</summary>
        public static Color PopupBack { get; private set; } = new Color(0.16f, 0.15f, 0.13f, 1f);

        /// <summary>A header's words: in capitals in a painted look (kept per text).</summary>
        public static string HeaderText(string text)
        {
            if (!Painted || string.IsNullOrEmpty(text)) return text;
            if (!capitals.TryGetValue(text, out string up))
            {
                if (capitals.Count > 256) capitals.Clear();
                capitals[text] = up = text.ToUpperInvariant();
            }
            return up;
        }

        static readonly Dictionary<string, string> capitals = new Dictionary<string, string>();

        /// <summary>The armed look of a button style: its size and padding, ButtonWarn's colours (kept per style).</summary>
        public static GUIStyle Armed(GUIStyle style)
        {
            if (style == null || ButtonWarn == null) return style;
            if (armedOf.TryGetValue(style, out GUIStyle a)) return a;
            a = new GUIStyle(style);
            GUIStyleState[] to = { a.normal, a.hover, a.active, a.focused },
                            from = { ButtonWarn.normal, ButtonWarn.hover, ButtonWarn.active, ButtonWarn.focused };
            for (int i = 0; i < to.Length; i++)
            {
                if (Painted) to[i].background = from[i].background;        // flat: the button's own, the words red
                to[i].textColor = from[i].textColor;
            }
            return armedOf[style] = a;
        }

        static readonly Dictionary<GUIStyle, GUIStyle> armedOf = new Dictionary<GUIStyle, GUIStyle>();

        /// <summary>Does a strip or a single toggle in this style sit in a Track (the tabs and the chips)?</summary>
        public static bool InTrack(GUIStyle style) => Painted && Track?.normal.background != null && (style == Tab || style == Chip);

        static Color Hex(string hex)
        {
            Paint.Rgba c = Paint.Rgba.Hex(hex);
            return new Color(c.R, c.G, c.B, c.A);
        }

        static Color Alpha(Color c, float a) => new Color(c.r, c.g, c.b, a);

        // ---------------------------------------------------------------- a look's colours

        /// <summary>The colours of one look; the shapes are the same for every look (PaintLook).</summary>
        sealed class Palette
        {
            /// <summary>Backgrounds (at the window's opacity): the window, a section, a track and a field's inside, a button,
            /// hovered and held; a chosen tab, chip or row, hovered; a tab or chip held.</summary>
            public Color Bg, Panel, Track, Control, ControlHover, ControlHeld, SegOn, SegOnHover, SegHeld;
            /// <summary>Lines: a section's frame and the dividers, the window's frame, a track's frame, a button's and a
            /// field's frame, hovered, a key cap's frame, the title's rule.</summary>
            public Color Line, WindowFrame, TrackFrame, ControlFrame, FrameHover, KeyFrame, TitleRule;
            /// <summary>The accent: the "way on", the chosen line, the check box, focus; hovered and held; the way on's lip.</summary>
            public Color Accent, AccentHover, AccentHeld, Lip, LipHover, LipHeld, WindowTop;
            /// <summary>Words: the title, headers, plain, chosen, an unchosen tab or chip, dim, on the accent.</summary>
            public Color Title, Heading, Text, TextOn, TextSeg, Dim, OnAccent;
            /// <summary>Warnings and good news; the armed button; a held arrow key; the scrollbar's thumb.</summary>
            public Color Red, RedHover, Green, WarnBg, WarnBgHover, WarnText, HeldKey, ScrollThumb, ScrollThumbHover;
            /// <summary>A rich-text row's dimmer part, on its own row and on the chosen one.</summary>
            public string DimHex, DimOnHex;
        }

        // ---------------------------------------------------------------- building

        static readonly List<Texture2D> lookTextures = new List<Texture2D>();
        static Font face, faceBold;
        static bool facesLooked;

        /// <summary>The look whose painting failed (with its scale and opacity): not tried again for them.</summary>
        static string failedLook;

        /// <summary>Paints the look over the styles Ensure just built; the flat styles when it fails. Painting changes the
        /// styles as it goes, so a failure has Ensure build them again whole and flat (1.1.0, review: a half-painted window
        /// with no backgrounds and no check boxes stayed).</summary>
        static void UseLook(string theme, float scale, float alpha)
        {
            string key = $"{theme}|{scale:0.###}|{alpha:0.##}";
            if (key == failedLook)
            {
                UseFlat();
                return;
            }
            try
            {
                ReleaseLook();
                PaintLook(theme == NordicName ? NordicColours : ClassicColours, new Painter(scale, alpha));
                MeterWords = new GUIStyle(SmallLabel);
                MeterWords.normal.textColor = Color.white;
                UseFaces();
                Painted = true;
                LookError = null;
            }
            catch (Exception e)
            {
                LookError = $"{theme}: {e.GetType().Name}: {e.Message}";
                failedLook = key;
                UseFlat();
                built = -1;                                                 // the next Ensure: the styles again, flat
            }
        }

        static void ReleaseLook()
        {
            foreach (Texture2D t in lookTextures)
                if (t) UnityEngine.Object.Destroy(t);
            lookTextures.Clear();
            HeaderMark = FoldArrow = CheckOff = CheckOffHover = CheckOn = CheckOnHover = Tick = null;
            if (Skin) UnityEngine.Object.Destroy(Skin);
            Skin = null;
        }

        /// <summary>The flat styles as Ensure built them, with their hand-drawn parts (painting failed).</summary>
        static void UseFlat()
        {
            ReleaseLook();
            Painted = false;
            TitleShadow = MeterTrack = MeterFill = null;
            MeterWords = SmallLabel;
            RuleColor = TitleRuleColor = FlatRule;
            TitleRuleThickness = 1.5f;
            EdgeColor = new Color(0.98f, 0.76f, 0.36f, 1f);
            EdgeThickness = 3f;
            GripColor = GripHeld = Color.white;
            DimHex = "#b3ab99";
            DimOnHex = "#4a3c22";
            MeterGood = new Color(0.55f, 0.85f, 0.45f, 1f);
            MeterNear = Amber;
            MeterOver = new Color(1f, 0.45f, 0.35f, 1f);
            MeterInfo = new Color(0.70f, 0.67f, 0.60f, 1f);
            MeterBack = new Color(0.07f, 0.065f, 0.06f, 1f);
            PopupBack = new Color(0.16f, 0.15f, 0.13f, 1f);
        }

        /// <summary>Turns the styles Ensure just built into the look in these colours: the handoff's style map (Nordic,
        /// option 1c), its sizes at the scale, its textures painted at that scale with the backgrounds at the opacity.</summary>
        static void PaintLook(Palette c, Painter p)
        {
            float s = p.Scale;
            int F(float basePx) => Mathf.Max(12, Mathf.RoundToInt(basePx * s));
            int lw = p.Line, b3 = p.P(3), b4 = p.P(4), b5 = p.P(5), b6 = p.P(6);

            // ---- textures
            Texture2D Framed4(Color fill, Color frame) => p.Box(b4, p.Outer(3), fill, true, frame);
            Texture2D Filled4(Color fill) => p.Box(b4, 3 * s, fill, true, null);
            Texture2D Band(Color fill, int band) =>                         // a chosen tab or chip: the accent underline
                p.Box(b4, 3 * s, fill, true, null, (q, w, h) => q.Box(0, 0, w, h, 3 * s, Rgba(c.Accent), clip: new double[] { 0, h - band, w, h }));
            Texture2D NextTex(Color face, Color lip)                        // the way on: opaque, a darker lip under it
            {
                int w = 2 * b4 + 2, h = w;
                return Made(Drawn(w, h, q =>
                {
                    q.Box(0, 0, w, h, 3 * s, Rgba(lip));
                    q.Box(0, 0, w, h - p.P(2), 3 * s, Rgba(face), Paint.TopCorners);
                }));
            }
            Texture2D KeyTex(Color frame)                                   // a key cap: a frame, its lower edge thicker
            {
                int w = 2 * b3 + 2, h = b3 + p.P(4) + 2;
                return Made(Drawn(w, h, q =>
                {
                    q.Box(0, 0, w, h, 3 * s, Rgba(frame));
                    q.Box(lw, lw, w - lw, h - 2 * lw, 2 * s, p.See(c.Bg), replace: true);
                }));
            }

            Texture2D windowTex = p.Box(b5, b6, b5, b5, p.Outer(4), c.Bg, true, c.WindowFrame,
                                        (q, w, h) => q.Box(0, 0, w, h, p.Outer(4), Rgba(c.WindowTop), clip: new double[] { 0, 0, w, p.P(3) }));
            Texture2D panelTex = p.Box(b5, p.Outer(4), c.Panel, true, c.Line);
            Texture2D trackTex = p.Box(b4, p.Outer(4), c.Track, true, c.TrackFrame);   // framed: the strips stand apart (the author)
            Texture2D button = Framed4(c.Control, c.ControlFrame), buttonHover = Framed4(c.ControlHover, c.FrameHover),
                      buttonHeld = Framed4(c.ControlHeld, c.Accent);
            Texture2D buttonDown = p.Box(b4, p.Outer(3), c.HeldKey, true, c.Accent,
                                         (q, w, h) => q.Box(lw, lw, w - lw, lw + p.P(2), 0, new Paint.Rgba(0, 0, 0, 0.35f)));
            Texture2D warn = Framed4(c.WarnBg, c.Red), warnHover = Framed4(c.WarnBgHover, c.RedHover);
            Texture2D next = NextTex(c.Accent, c.Lip), nextHover = NextTex(c.AccentHover, c.LipHover), nextHeld = NextTex(c.AccentHeld, c.LipHeld);
            Texture2D segHover = Filled4(c.Control), segHeld = Filled4(c.SegHeld);
            Texture2D segOn = Band(c.SegOn, p.P(3)), segOnHover = Band(c.SegOnHover, p.P(3)), segOnSmall = Band(c.SegOn, p.P(2)),
                      segOnSmallHover = Band(c.SegOnHover, p.P(2));
            Texture2D itemOn = p.Box(b4, 3 * s, c.SegOn, true, null,
                                     (q, w, h) => q.Box(0, 0, w, h, 3 * s, Rgba(c.Accent), clip: new double[] { 0, 0, p.P(3), h }));
            Texture2D fieldTex = Framed4(c.Track, c.ControlFrame), fieldHover = Framed4(c.Track, c.FrameHover), fieldFocus = Framed4(c.Track, c.Accent);
            Texture2D keyTex = KeyTex(c.KeyFrame), keyStrong = KeyTex(c.FrameHover);
            int sliderH = p.R(22), barH = p.P(6), barY = (sliderH - barH) / 2;
            Texture2D sliderTex = Made(Drawn(2 * b4 + 2, sliderH, q =>
            {
                q.Box(0, barY, q.Width, barY + barH, barH / 2f, p.See(c.Track), replace: true);
                q.Frame(0, barY, q.Width, barY + barH, barH / 2f, lw, Rgba(c.Line));
            }));
            Texture2D thumbTex = p.Box(b3, 3 * s, c.Text, false, null), thumbHover = p.Box(b3, p.Outer(3), Color.white, false, c.Accent),
                      thumbHeld = p.Box(b3, 3 * s, c.Accent, false, null);
            Texture2D scrollTrack = p.Pill(p.P(10), 2 * b5 + 2, c.Track, true);
            Texture2D scrollThumb = p.Pill(p.P(6), 2 * b3 + 2, c.ScrollThumb, false), scrollThumbHover = p.Pill(p.P(6), 2 * b3 + 2, c.ScrollThumbHover, false),
                      scrollThumbHeld = p.Pill(p.P(6), 2 * b3 + 2, c.Accent, false);
            Texture2D meterTrack = p.Pill(2 * b3 + 2, p.P(6), c.Track, true), meterFill = p.Pill(2 * b3 + 2, p.P(6), Color.white, false);
            HeaderMark = p.Diamond(p.P(8), 0.84f, c.Heading);
            FoldArrow = p.Arrow(p.P(FoldArrowSize));

            // the check box at its own size (Widgets.CheckBox draws it in the middle of the row), the tick over it
            int box = p.R(18), m = p.P(1);
            Texture2D CheckTex(bool on, bool hover) => Made(Drawn(box, box, q =>
            {
                if (on) q.Box(0, 0, box, box, 3 * s, Rgba(hover ? c.AccentHover : c.Accent));
                else
                {
                    q.Box(m, m, box - m, box - m, p.Outer(3), p.See(c.Track), replace: true);
                    q.Frame(m, m, box - m, box - m, p.Outer(3), lw, Rgba(hover ? c.Accent : c.FrameHover));
                }
            }));
            CheckOff = CheckTex(false, false);
            CheckOffHover = CheckTex(false, true);
            CheckOn = CheckTex(true, false);
            CheckOnHover = CheckTex(true, true);
            double k = box / 18.0;                                          // the handoff's tick, in the box's middle
            Tick = Made(Drawn(box, box, q => q.Line(new[] { 4.5 * k, 9.5 * k, 7.5 * k, 12.5 * k, 13.5 * k, 5.5 * k }, 2.2 * k, Rgba(c.OnAccent))));

            // ---- styles (the handoff's style map; L, R, T, B)
            RectOffset in4 = Insets(b4, b4, b4, b4), in5 = Insets(b5, b5, b5, b5);
            Window.normal.background = Window.onNormal.background = Window.focused.background = Window.onFocused.background =
                Window.hover.background = Window.onHover.background = windowTex;
            Window.border = Insets(b5, b6, b5, b5);
            TextAll(Window, c.Title);
            TitleName.normal.textColor = c.Title;
            TitleVersion.normal.textColor = Alpha(c.Title, 0.65f);
            TitleShadow = new GUIStyle(TitleName);
            TitleShadow.normal.textColor = Color.black;
            TitleArea.normal.textColor = c.Text;
            TitleAreaOver.normal.textColor = c.Red;

            Track = new GUIStyle { padding = p.Off(2, 2, 2, 2), margin = p.Off(0, 0, 0, 0), border = in4 };
            Track.normal.background = trackTex;
            foreach (GUIStyle st in new[] { Tab, Chip })
            {
                Look(st, null, segHover, segHeld, c.TextSeg, in4);
                st.hover.textColor = st.active.textColor = c.Text;
                st.margin = p.Off(2, 2, 0, 0);                              // the gap between two (Widgets.Strip)
            }
            On(Tab, segOn, segOnHover, c.TextOn);
            Tab.fontSize = F(16);
            Tab.padding = p.Off(10, 10, 4, 6);
            On(Chip, segOnSmall, segOnSmallHover, c.TextOn);
            Chip.padding = p.Off(8, 8, 2, 4);

            Section.normal.background = panelTex;
            Section.border = in5;
            Section.margin = p.Off(0, 0, 4, 8);
            Header.fontSize = F(15);
            Header.padding = p.Off(14, 0, 0, 0);                            // room for the diamond (Widgets.Header)
            Header.margin = p.Off(0, 0, 16, 4);
            Header.normal.textColor = c.Heading;
            SubHeader.margin = p.Off(0, 0, 4, 2);
            SubHeader.normal.textColor = c.Heading;
            Lead.normal.textColor = c.Heading;
            Fold.margin = FoldOn.margin = p.Off(0, 0, 12, 4);
            Fold.normal.textColor = c.Text;
            Fold.hover.textColor = c.Accent;
            Look(FoldOn, segOn, segOn, segOn, c.TextOn, in4);
            FoldOn.hover.textColor = c.Accent;                               // hovered beats focused: amber as every heading (the author)
            // room for the arrow at the left (FoldHead), and air between the words and the focused heading's amber line;
            // the same for both, so moving the focus never moves a row (1.1.0, the author: the line touched the words)
            Fold.padding = FoldOn.padding = p.Off(FoldArrowSize + 14, 6, 4, 9);
            Fold.fontSize = FoldOn.fontSize = F(18);                        // a little bigger than the flat 17 (the author: they
                                                                            // looked smaller beside their painted arrow)
            Label.normal.textColor = CheckText.normal.textColor = ListEmpty.normal.textColor = ViewText.normal.textColor = c.Text;
            CheckText.hover.textColor = CheckText.active.textColor = c.Accent;
            foreach (GUIStyle st in new[] { Small, SideNote, SmallLabel, BoxText, ViewEmpty }) st.normal.textColor = c.Dim;
            Warn.normal.textColor = c.Red;
            Good.normal.textColor = c.Green;
            Notice.normal.textColor = c.Heading;
            foreach (GUIStyle st in new[] { ViewEmpty, ViewText })
            {
                st.normal.background = panelTex;
                st.border = in5;
            }

            foreach (GUIStyle st in new[] { Button, ButtonLess, SmallButton })
                Look(st, button, buttonHover, buttonHeld, c.Text, in4);
            Look(ButtonDown, buttonDown, buttonDown, buttonDown, c.TextOn, in4);
            ButtonWarn = new GUIStyle(Button);
            Look(ButtonWarn, warn, warnHover, warn, c.WarnText, in4);
            foreach (GUIStyle st in new[] { Next, NextLess, ButtonGo, SmallGo })
                Look(st, next, nextHover, nextHeld, c.OnAccent, in4);
            Next.padding = p.Off(10, 10, 5, 7);
            NextLess.padding = p.Off(9, 9, 4, 6);
            ButtonGo.fontStyle = FontStyle.Bold;
            SmallGo.padding = p.Off(8, 8, 2, 4);

            foreach (GUIStyle st in new[] { Item, ItemRich })
            {
                Look(st, null, segHover, segHeld, c.Text, in4);
                On(st, itemOn, itemOn, c.TextOn);
                st.padding = p.Off(12, 10, 6, 6);
            }
            foreach (GUIStyle st in new[] { Field, Area })
            {
                Look(st, fieldTex, fieldHover, fieldTex, c.Text, in4);
                st.focused.background = fieldFocus;
                st.padding = p.Off(7, 7, 4, 4);
            }
            foreach (GUIStyleState st in new[] { Toggle.normal, Toggle.hover, Toggle.active, Toggle.focused,
                                                 Toggle.onNormal, Toggle.onHover, Toggle.onActive, Toggle.onFocused })
                st.background = null;                                       // Widgets.CheckBox draws the box (CheckOff, CheckOn)
            TextAll(Toggle, c.Text);
            Slider.normal.background = Slider.hover.background = Slider.active.background = Slider.focused.background = sliderTex;
            Slider.border = Insets(b4, 0, b4, 0);
            Slider.padding = new RectOffset(Slider.padding.left, Slider.padding.right, p.R(1), p.R(1));
            Thumb.normal.background = Thumb.focused.background = thumbTex;
            Thumb.hover.background = thumbHover;
            Thumb.active.background = thumbHeld;
            Thumb.border = Insets(b3, b3, b3, b3);
            Thumb.fixedWidth = p.R(12);
            Thumb.fixedHeight = p.R(20);
            Look(KeyCap, keyStrong, keyStrong, keyStrong, c.Heading, Insets(b3, b3, b3, p.P(4)));
            Look(KeyHint, keyTex, keyTex, keyTex, c.Dim, Insets(b3, b3, b3, p.P(4)));

            MeterTrack = new GUIStyle { border = Insets(b3, 0, b3, 0) };
            MeterTrack.normal.background = meterTrack;
            MeterFill = new GUIStyle { border = Insets(b3, 0, b3, 0) };
            MeterFill.normal.background = meterFill;

            var bar = new GUIStyle(GUI.skin.verticalScrollbar)
            {
                fixedWidth = p.P(10), padding = p.Off(2, 2, 2, 2), border = Insets(0, b5, 0, b5), margin = p.Off(4, 0, 0, 0),
            };
            bar.normal.background = bar.hover.background = bar.active.background = bar.focused.background = scrollTrack;
            var thumb = new GUIStyle(GUI.skin.verticalScrollbarThumb) { fixedWidth = 0, border = Insets(0, b3, 0, b3), padding = new RectOffset(0, 0, b3, b3) };
            thumb.normal.background = thumb.focused.background = scrollThumb;
            thumb.hover.background = scrollThumbHover;
            thumb.active.background = scrollThumbHeld;
            Skin = UnityEngine.Object.Instantiate(GUI.skin);
            Skin.hideFlags = HideFlags.HideAndDontSave;
            Skin.verticalScrollbar = bar;
            Skin.verticalScrollbarThumb = thumb;

            RuleColor = c.Line;
            TitleRuleColor = c.TitleRule;
            TitleRuleThickness = 1f;
            EdgeColor = c.Accent;
            EdgeThickness = 2f;
            GripColor = c.FrameHover;
            GripHeld = c.Accent;
            DimHex = c.DimHex;
            DimOnHex = c.DimOnHex;
            MeterGood = c.Green;
            MeterNear = c.Accent;
            MeterOver = c.Red;
            MeterInfo = c.Dim;
            MeterBack = c.Track;
            PopupBack = Alpha(c.Panel, 1f);
        }

        // ---------------------------------------------------------------- shared helpers

        static RectOffset Insets(int l, int t, int r, int b) => new RectOffset(l, r, t, b);

        /// <summary>A button-like style's backgrounds (normal, hover, active; focused as normal) and its words' colour.</summary>
        static void Look(GUIStyle s, Texture2D normal, Texture2D hover, Texture2D active, Color text, RectOffset border)
        {
            s.normal.background = normal;
            s.hover.background = hover;
            s.active.background = active;
            s.focused.background = normal;
            s.normal.textColor = s.hover.textColor = s.active.textColor = s.focused.textColor = text;
            if (border != null) s.border = border;
        }

        /// <summary>A toggle-like style's chosen state.</summary>
        static void On(GUIStyle s, Texture2D on, Texture2D onHover, Color text)
        {
            s.onNormal.background = s.onActive.background = s.onFocused.background = on;
            s.onHover.background = onHover;
            s.onNormal.textColor = s.onHover.textColor = s.onActive.textColor = s.onFocused.textColor = text;
        }

        static void TextAll(GUIStyle s, Color c)
        {
            foreach (GUIStyleState st in new[] { s.normal, s.hover, s.active, s.focused, s.onNormal, s.onHover, s.onActive, s.onFocused })
                st.textColor = c;
        }

        /// <summary>The game's own UI font (Averia Serif Libre) on every style, when the game has it as a font IMGUI can size
        /// (a dynamic one); else the skin's font.</summary>
        static void UseFaces()
        {
            if (!facesLooked)
            {
                facesLooked = true;
                foreach (Font f in Resources.FindObjectsOfTypeAll<Font>())
                {
                    if (!f || !f.dynamic) continue;
                    string n = f.name.Replace(" ", "").ToLowerInvariant();
                    if (n == "averiaseriflibre-regular" || (n == "averiaseriflibre" && face == null)) face = f;
                    else if (n == "averiaseriflibre-bold") faceBold = f;
                }
            }
            if (!face && !faceBold) return;
            foreach (GUIStyle s in AllStyles())
            {
                if (s == null) continue;
                bool bold = s.fontStyle == FontStyle.Bold || s.fontStyle == FontStyle.BoldAndItalic;
                if (bold && faceBold)
                {
                    s.font = faceBold;
                    s.fontStyle = s.fontStyle == FontStyle.BoldAndItalic ? FontStyle.Italic : FontStyle.Normal;
                }
                else if (face) s.font = face;
            }
        }

        /// <summary>The fonts the look found (for the log once).</summary>
        public static string Faces => Painted ? $"{(face ? face.name : "none")} / {(faceBold ? faceBold.name : "none")}" : "the skin's";

        static IEnumerable<GUIStyle> AllStyles() => new[]
        {
            Window, Section, Header, Label, Small, Warn, Good, Notice, Button, Next, Tab, Field, Area, Item, ItemRich, BoxText,
            TitleName, TitleVersion, TitleArea, TitleAreaOver, Chip, SmallLabel, Fold, FoldOn, ViewEmpty, ViewText, SideNote, SubHeader,
            ListEmpty, CheckText, ButtonDown, ButtonGap, KeyCap, KeyHint, SmallButton, ButtonLess, NextLess, ButtonGo, SmallGo, Toggle,
            ButtonWarn, Lead, TitleShadow, MeterWords,
        };

        /// <summary>Paints a look's textures at the window's pixel scale (P: logical units to whole pixels), its backgrounds
        /// at the window's opacity.</summary>
        sealed class Painter
        {
            public readonly float Scale, Opacity;
            public readonly int Line;                                       // a frame's width

            public Painter(float scale, float opacity)
            {
                Scale = scale;
                Opacity = opacity;
                Line = P(1);
            }

            public int P(float x) => Mathf.Max(1, Mathf.RoundToInt(x * Scale));
            public int R(float x) => Mathf.RoundToInt(x * Scale);
            public RectOffset Off(float l, float r, float t, float b) => new RectOffset(R(l), R(r), R(t), R(b));

            /// <summary>A framed box's outer corner for a corner rx at the frame's middle (the SVGs' way).</summary>
            public float Outer(float rx) => rx * Scale + Line / 2f;

            /// <summary>A background colour: at the window's opacity.</summary>
            public Paint.Rgba See(Color c) => Rgba(c, Opacity);

            /// <summary>A nine-sliced box, bl, bt, br, bb pixels of border and a 2 px middle: filled (a background at the
            /// opacity when see), framed when frame is set, then more paints over it.</summary>
            public Texture2D Box(int bl, int bt, int br, int bb, float r, Color fill, bool see, Color? frame, Action<Paint, int, int> more = null)
            {
                int w = bl + br + 2, h = bt + bb + 2;
                var p = new Paint(w, h);
                p.Box(0, 0, w, h, r, see ? See(fill) : Rgba(fill), replace: true);
                if (frame.HasValue) p.Frame(0, 0, w, h, r, Line, Rgba(frame.Value));
                more?.Invoke(p, w, h);
                return Made(p);
            }

            public Texture2D Box(int b, float r, Color fill, bool see, Color? frame, Action<Paint, int, int> more = null) =>
                Box(b, b, b, b, r, fill, see, frame, more);

            /// <summary>Round ends along its length.</summary>
            public Texture2D Pill(int w, int h, Color c, bool see) =>
                Made(Drawn(w, h, p => p.Box(0, 0, w, h, Math.Min(w, h) / 2f, see ? See(c) : Rgba(c), replace: true)));

            /// <summary>A square standing on a corner in an n x n texture, share of its half-width.</summary>
            public Texture2D Diamond(int n, float share, Color c) =>
                Made(Drawn(n, n, p => p.Diamond(n / 2f, n / 2f, n / 2f * share, Rgba(c))));

            /// <summary>A white arrow head pointing right in the middle of an n x n texture, smooth when turned (a group
            /// heading's arrow, tinted as its words: Widgets.FoldHead turns it).</summary>
            public Texture2D Arrow(int n)
            {
                Texture2D t = Made(Drawn(n, n, p => p.Triangle(0.3 * n, 0.16 * n, 0.3 * n, 0.84 * n, 0.82 * n, 0.5 * n, Rgba(Color.white))));
                t.filterMode = FilterMode.Bilinear;
                return t;
            }
        }

        static Paint Drawn(int w, int h, Action<Paint> draw)
        {
            var p = new Paint(w, h);
            draw(p);
            return p;
        }

        static Paint.Rgba Rgba(Color c, float a = 1f) => new Paint.Rgba(c.r, c.g, c.b, c.a * a);

        /// <summary>A painted texture (rows flipped: Unity's start at the bottom), kept to be destroyed with the look.</summary>
        static Texture2D Made(Paint p)
        {
            var t = new Texture2D(p.Width, p.Height, TextureFormat.RGBA32, false)
            {
                hideFlags = HideFlags.HideAndDontSave, filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp,
            };
            var pixels = new Color[p.Width * p.Height];
            for (int y = 0; y < p.Height; y++)
                for (int x = 0; x < p.Width; x++)
                {
                    Paint.Rgba c = p.Get(x, y);
                    pixels[(p.Height - 1 - y) * p.Width + x] = new Color(c.R, c.G, c.B, c.A);
                }
            t.SetPixels(pixels);
            t.Apply();
            lookTextures.Add(t);
            return t;
        }
    }
}
