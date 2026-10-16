using System.Collections.Generic;
using UnityEngine;

namespace SignLink.UI
{
    /// <summary>IMGUI styles for the SignLink window, in the game's dark wood and amber. Unity's default skin draws
    /// windows half transparent, which is hard to read over a bright world; these are as solid as the player's Opacity
    /// (Settings, 0.8 by default: the backgrounds take the alpha, the text and the frames stay opaque).
    /// The fonts are built at their pixel size for the window's text scale (Ensure), so the text is crisp at every
    /// screen size instead of being drawn small and magnified; paddings and margins scale with them.</summary>
    internal static class Theme
    {
        public static GUIStyle Window, Section, Header, Label, Small, Warn, Good, Notice, Button, Next, Tab, Field, Item, ItemRich, BoxText,
                               TitleName, TitleVersion, TitleArea, TitleAreaOver, Chip, SmallLabel, Fold, FoldOn, ViewEmpty, ViewText, SideNote, SubHeader,
                               ListEmpty, CheckText, ButtonDown, ButtonGap, KeyCap, SmallButton, ButtonLess, NextLess, ButtonGo, Toggle, Slider, Thumb;
        static Texture2D bg, panel, button, hover, active, accent, field, fieldOn, thumb, thumbHover, thumbActive, toggleOff, toggleOn,
                         next, nextHover, nextActive;
        static int built = -1, builtScale = -1, builtOpacity = -1;

        /// <summary>The generated textures go (the plugin unloads, 0.20.135, the author: now let us do the OnDestroy solution mentioned); the next Ensure makes them again.</summary>
        public static void Release()
        {
            foreach (Texture2D t in new[] { bg, panel, button, hover, active, accent, field, fieldOn, thumb, thumbHover, thumbActive, toggleOff,
                                            toggleOn, next, nextHover, nextActive })
                if (t) Object.Destroy(t);
            bg = panel = button = hover = active = accent = field = fieldOn = thumb = thumbHover = thumbActive = toggleOff = toggleOn =
                next = nextHover = nextActive = null;
            built = builtScale = builtOpacity = -1;
        }

        /// <summary>The hint font's pixel size (rich-text rows name it).</summary>
        public static int SmallSize { get; private set; } = 14;

        static readonly Color Text = new Color(0.90f, 0.87f, 0.80f);
        static readonly Color Amber = new Color(0.98f, 0.76f, 0.36f);

        static readonly Dictionary<GUIStyle, Dictionary<int, GUIStyle>> variants = new Dictionary<GUIStyle, Dictionary<int, GUIStyle>>();

        /// <summary>The style that shows this label whole in width pixels (0.20.50, the author: on some buttons the
        /// text slides into the button's padding and does not show): the style itself when it fits; else with a third of
        /// its side padding; else that a pixel smaller at a time, down to 70 % of its size (never under 11 px); else that,
        /// wrapped. Layout events get the style itself (the rect is laid out with it, the fit only draws).</summary>
        public static GUIStyle Fitted(GUIStyle style, GUIContent c, float width)
        {
            if (style == null || width <= 1f || Event.current == null || Event.current.type == EventType.Layout) return style;
            // the answer for this style, label and width, kept: up to eight CalcSize calls a label every Repaint before
            // (0.20.70, review); cleared with the styles (Ensure), and when a style has seen many labels
            string text = c.text ?? "";
            int w = Mathf.RoundToInt(width * 2f);
            if (!fits.TryGetValue(style, out Dictionary<string, FitMemo> byText)) fits[style] = byText = new Dictionary<string, FitMemo>();
            if (byText.TryGetValue(text, out FitMemo memo) && memo.Width == w) return memo.Style;
            GUIStyle got = FitNow(style, c, width);
            if (memo == null)
            {
                if (byText.Count >= 512) byText.Clear();
                byText[text] = memo = new FitMemo();
            }
            memo.Width = w;
            memo.Style = got;
            return got;
        }

        sealed class FitMemo
        {
            public int Width;
            public GUIStyle Style;
        }

        static readonly Dictionary<GUIStyle, Dictionary<string, FitMemo>> fits = new Dictionary<GUIStyle, Dictionary<string, FitMemo>>();

        static GUIStyle FitNow(GUIStyle style, GUIContent c, float width)
        {
            if (style.CalcSize(c).x <= width + 0.5f) return style;
            int size = style.fontSize > 0 ? style.fontSize : 16, least = Mathf.Max(11, Mathf.RoundToInt(size * 0.7f));
            for (int f = size; f >= least; f--)
            {
                GUIStyle v = Variant(style, f, false);
                if (v.CalcSize(c).x <= width + 0.5f) return v;
            }
            return Variant(style, least, true);
        }

        static GUIStyle Variant(GUIStyle style, int fontSize, bool wrap)
        {
            if (!variants.TryGetValue(style, out Dictionary<int, GUIStyle> byKey)) variants[style] = byKey = new Dictionary<int, GUIStyle>();
            int key = fontSize * 2 + (wrap ? 1 : 0);
            if (byKey.TryGetValue(key, out GUIStyle v)) return v;
            v = new GUIStyle(style) { fontSize = fontSize, wordWrap = wrap, clipping = TextClipping.Clip };
            v.padding = new RectOffset(Mathf.Max(1, style.padding.left / 3), Mathf.Max(1, style.padding.right / 3), style.padding.top, style.padding.bottom);
            return byKey[key] = v;
        }

        static Texture2D Solid(Color c)
        {
            var t = new Texture2D(1, 1, TextureFormat.RGBA32, false) { hideFlags = HideFlags.HideAndDontSave };
            t.SetPixel(0, 0, c);
            t.Apply();
            return t;
        }

        /// <summary>Builds the styles (inside OnGUI: GUI.skin exists only there) for a text scale, screen pixels per
        /// logical unit, and an opacity (0.5-1: the alpha of every background; text, frames and the toggle's mark stay
        /// opaque); again when the rounded scale, the rounded opacity or the skin changes. Base sizes: 14 hints, 16
        /// labels, buttons and fields, 17 headers, 18 the title, never under 12 px.</summary>
        public static void Ensure(float scale, float opacity = 1f)
        {
            int skin = GUI.skin.GetInstanceID(), s100 = Mathf.RoundToInt(scale * 100f);
            int o100 = Mathf.RoundToInt(Mathf.Clamp(opacity, 0.5f, 1f) * 100f);
            if (built == skin && builtScale == s100 && builtOpacity == o100 && Window != null && bg) return;
            built = skin;
            variants.Clear();                                               // the styles are new: so are their variants
            fits.Clear();
            builtScale = s100;
            if (!bg || builtOpacity != o100)
            {
                builtOpacity = o100;
                float a = o100 / 100f;
                foreach (Texture2D t in new[] { bg, panel, button, hover, active, accent, field, fieldOn, thumb, thumbHover, thumbActive,
                                                next, nextHover, nextActive })
                    if (t) Object.Destroy(t);
                bg = Solid(new Color(0.11f, 0.10f, 0.09f, a));
                panel = Solid(new Color(0.16f, 0.15f, 0.13f, a));
                button = Solid(new Color(0.27f, 0.23f, 0.18f, a));
                hover = Solid(new Color(0.36f, 0.30f, 0.22f, a));
                active = Solid(new Color(0.55f, 0.40f, 0.18f, a));
                accent = Solid(new Color(0.62f, 0.45f, 0.18f, a));
                field = Framed(new Color(0.07f, 0.065f, 0.06f, a), new Color(0.45f, 0.37f, 0.26f, 1f));
                fieldOn = Framed(new Color(0.07f, 0.065f, 0.06f, a), Amber);
                thumb = Framed(new Color(0.36f, 0.30f, 0.22f, a), new Color(0.62f, 0.45f, 0.18f, 1f));
                thumbHover = Framed(new Color(0.45f, 0.37f, 0.26f, a), Amber);
                thumbActive = Framed(new Color(0.55f, 0.40f, 0.18f, a), Amber);
                float na = Mathf.Max(a, 0.9f);                              // the way on stays bright on a see-through window
                next = Solid(new Color(0.86f, 0.60f, 0.16f, na));
                nextHover = Solid(new Color(0.97f, 0.73f, 0.27f, na));
                nextActive = Solid(new Color(0.70f, 0.48f, 0.12f, na));
            }
            float alpha = o100 / 100f;
            int R(float x) => Mathf.RoundToInt(x * scale);
            int F(float basePx) => Mathf.Max(12, Mathf.RoundToInt(basePx * scale));

            // a bigger title (0.20.50) with room under it for the rule SignLinkWindow draws (TitleRule)
            Window = new GUIStyle(GUI.skin.window) { padding = new RectOffset(R(12), R(12), R(44), R(16)), fontSize = F(23), fontStyle = FontStyle.Bold };
            // the title R(6) under the top edge: the skin's own offset (-18 px, for its own padding) would leave it on the
            // tab strip at a bigger scale (the content starts at padding.top)
            Window.contentOffset = new Vector2(0f, R(7) - Window.padding.top);
            foreach (GUIStyleState s in new[] { Window.normal, Window.onNormal, Window.focused, Window.onFocused, Window.hover, Window.onHover })
            {
                s.background = bg;
                s.textColor = Amber;
            }
            // air between groups: a header stands off what is above it, a section and a hint off what follows
            Section = new GUIStyle(GUI.skin.box) { padding = new RectOffset(R(12), R(12), R(10), R(10)), margin = new RectOffset(0, 0, R(2), R(8)) };
            Section.normal.background = panel;
            Header = new GUIStyle(GUI.skin.label) { fontSize = F(17), fontStyle = FontStyle.Bold, margin = new RectOffset(0, 0, R(10), R(4)) };
            Fold = new GUIStyle(Header) { alignment = TextAnchor.MiddleLeft, wordWrap = false };   // a header that opens and closes
            Fold.hover.textColor = Amber;
            Header.normal.textColor = Amber;
            Label = new GUIStyle(GUI.skin.label) { fontSize = F(16), wordWrap = true };
            Label.normal.textColor = Text;
            SmallSize = F(14);
            Small = new GUIStyle(Label) { fontSize = SmallSize, margin = new RectOffset(R(4), R(4), R(2), R(8)) };
            Small.normal.textColor = new Color(0.70f, 0.67f, 0.60f);
            BoxText = new GUIStyle(Small) { alignment = TextAnchor.MiddleCenter, wordWrap = false };   // "24 MP" in an empty thumbnail box
            // a quick view with nothing to show (0.20.60): its box filled, the words in its middle
            ViewEmpty = new GUIStyle(Small) { alignment = TextAnchor.MiddleCenter, wordWrap = true, padding = new RectOffset(R(10), R(10), R(6), R(6)),
                                              margin = new RectOffset(0, 0, R(2), R(6)) };
            ViewEmpty.normal.background = panel;
            // a quick view showing a sign's words (0.20.174): the content, so in the labels' size and colour
            ViewText = new GUIStyle(Label) { alignment = TextAnchor.MiddleCenter, wordWrap = true, padding = ViewEmpty.padding, margin = ViewEmpty.margin };
            ViewText.normal.background = panel;
            // what a button does, beside it on its row (0.20.65: 1 Sign's actions), in the middle of its height
            SideNote = new GUIStyle(Small) { alignment = TextAnchor.MiddleLeft, wordWrap = true, margin = new RectOffset(R(12), R(4), R(2), R(2)) };
            // an empty list (0.20.71: Playing's "none nearby"): the labels' size, in the middle of a row-high box
            ListEmpty = new GUIStyle(Label) { alignment = TextAnchor.MiddleCenter, wordWrap = true, padding = new RectOffset(R(8), R(8), R(8), R(8)) };
            ListEmpty.normal.textColor = new Color(0.86f, 0.82f, 0.74f);
            // a switch's words beside its box (Widgets.Check, 0.20.74): a label that wraps and flips the switch on a click
            CheckText = new GUIStyle(Label) { alignment = TextAnchor.MiddleLeft, wordWrap = true, padding = new RectOffset(R(2), R(4), R(3), R(3)),
                                              margin = new RectOffset(R(2), R(2), R(3), R(3)) };
            CheckText.hover.textColor = Amber;
            CheckText.active.textColor = Amber;
            // a key of the hotkey in a small frame (Widgets.KeyCaps, 0.20.82)
            KeyCap = new GUIStyle(GUI.skin.box) { fontSize = F(15), fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter, wordWrap = false,
                                                  padding = new RectOffset(R(9), R(9), R(3), R(4)), margin = new RectOffset(R(2), R(2), R(4), R(4)) };
            KeyCap.normal.textColor = Amber;
            Warn = new GUIStyle(Label);
            Warn.normal.textColor = new Color(1f, 0.45f, 0.35f);
            Good = new GUIStyle(Label);
            Good.normal.textColor = new Color(0.55f, 0.85f, 0.45f);
            Notice = new GUIStyle(Label);                                   // amber: worth a look, not wrong
            Notice.normal.textColor = Amber;
            // the title, drawn by the window itself (SignLinkWindow.TitleBar, 0.20.51): the name, the version smaller beside
            // it, the multiplayer area on the right
            TitleName = new GUIStyle(GUI.skin.label) { fontSize = F(23), fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleLeft,
                                                       wordWrap = false, padding = new RectOffset(0, 0, 0, 0), margin = new RectOffset(0, 0, 0, 0) };
            TitleName.normal.textColor = Amber;
            TitleVersion = new GUIStyle(TitleName) { fontSize = F(14), fontStyle = FontStyle.Normal, alignment = TextAnchor.LowerLeft };
            TitleVersion.normal.textColor = new Color(Amber.r, Amber.g, Amber.b, 0.65f);
            TitleArea = new GUIStyle(TitleName) { fontSize = F(17), alignment = TextAnchor.MiddleRight, clipping = TextClipping.Clip };
            TitleAreaOver = new GUIStyle(TitleArea);
            TitleAreaOver.normal.textColor = Warn.normal.textColor;
            Button = new GUIStyle(GUI.skin.button) { fontSize = F(16), padding = new RectOffset(R(10), R(10), R(5), R(5)) };
            Set(Button, button, hover, active, Text);
            // a button as small as a chip (0.20.85, the author: the text editor's, the light's and Resolution's buttons as
            // small as 3 Place's Standing / Face up / Face down)
            SmallButton = new GUIStyle(Button) { fontSize = F(13), padding = new RectOffset(R(8), R(8), R(2), R(3)) };
            // a button as the mouse holds it down (0.20.77: Move the sign's arrow keys show the step's button pressed)
            ButtonDown = new GUIStyle(Button);
            Set(ButtonDown, active, active, active, Text);
            // an empty place a button's size, its margins too (0.20.78: the gaps of Move the sign's arrow pad)
            ButtonGap = new GUIStyle(Button);
            Set(ButtonGap, null, null, null, Text);
            Next = new GUIStyle(Button) { fontStyle = FontStyle.Bold, fontSize = F(17) };   // the way on (Widgets.NextButton)
            Set(Next, next, nextHover, nextActive, new Color(0.13f, 0.09f, 0.04f));
            // a size under Button (0.20.146, the author: 3 Place's Changes buttons one size smaller than the Reset to
            // default button above in content positioning)
            ButtonLess = new GUIStyle(Button) { fontSize = F(15), padding = new RectOffset(R(9), R(9), R(4), R(4)) };
            NextLess = new GUIStyle(Next) { fontSize = F(15), padding = new RectOffset(R(9), R(9), R(4), R(4)) };
            // a button the size of Button in the way-on yellow (0.20.176, the author: Play on the Playing tab shown in yellow ... so it
            // feels pressable): what to press to get something going again
            ButtonGo = new GUIStyle(Button);
            Set(ButtonGo, next, nextHover, nextActive, Next.normal.textColor);
            Tab = new GUIStyle(Button) { fontStyle = FontStyle.Bold };
            Set(Tab, button, hover, active, Text);
            Tab.onNormal.background = accent;
            Tab.onHover.background = accent;
            Tab.onNormal.textColor = Tab.onHover.textColor = Color.white;
            // a minor choice drawn small (0.20.57; 0.20.78: the pose buttons, Move by): small chips, the chosen one lit
            Chip = new GUIStyle(Tab) { fontSize = F(13), fontStyle = FontStyle.Normal, padding = new RectOffset(R(8), R(8), R(2), R(3)),
                                       margin = new RectOffset(R(2), R(2), R(3), R(3)) };
            // a part's title inside a group (Widgets.SubHeader, 0.20.180): the labels' size, bold, in the headers' amber
            SubHeader = new GUIStyle(Label) { fontStyle = FontStyle.Bold, margin = new RectOffset(R(4), R(4), R(4), R(2)) };
            SubHeader.normal.textColor = Amber;
            SmallLabel = new GUIStyle(Label) { fontSize = F(13), wordWrap = false, alignment = TextAnchor.MiddleLeft };
            Item = new GUIStyle(Button) { alignment = TextAnchor.MiddleLeft, wordWrap = true, padding = new RectOffset(R(10), R(10), R(6), R(6)),
                                          margin = new RectOffset(0, 0, R(1), R(1)) };
            Set(Item, panel, hover, active, Text);                          // a list row; the selected one is amber
            Item.onNormal.background = Item.onHover.background = Item.onActive.background = accent;
            Item.onNormal.textColor = Item.onHover.textColor = Item.onActive.textColor = Color.white;
            ItemRich = new GUIStyle(Item) { richText = true };             // a row with a smaller, dimmer part (Signs nearby)
            FoldOn = new GUIStyle(Fold) { padding = new RectOffset(R(6), R(6), R(2), R(2)) };   // a group's heading the arrows are on
            FoldOn.normal.background = FoldOn.hover.background = FoldOn.active.background = accent;
            FoldOn.normal.textColor = FoldOn.hover.textColor = FoldOn.active.textColor = Color.white;
            Field = new GUIStyle(GUI.skin.textField) { fontSize = F(16), border = new RectOffset(2, 2, 2, 2), padding = new RectOffset(R(6), R(6), R(4), R(4)) };
            Field.normal.background = Field.hover.background = field;     // a dark box with a frame: visible before you click
            Field.focused.background = fieldOn;
            Field.normal.textColor = Field.hover.textColor = Field.focused.textColor = Text;

            // a check box drawn at the text's size (the skin's is 14 px whatever the screen): a box of R(18) at the left
            // of a texture whose transparent rest stretches under the label (9-sliced, the box itself never stretched)
            int box = R(18), pad = R(4);
            if (toggleOff) Object.Destroy(toggleOff);
            if (toggleOn) Object.Destroy(toggleOn);
            toggleOff = ToggleBox(box, pad, false, alpha);
            toggleOn = ToggleBox(box, pad, true, alpha);
            Toggle = new GUIStyle(GUI.skin.toggle)
            {
                fontSize = F(16), alignment = TextAnchor.MiddleLeft, wordWrap = false, imagePosition = ImagePosition.TextOnly,
                padding = new RectOffset(box + R(8), R(4), R(3), R(3)), margin = new RectOffset(R(2), R(2), R(3), R(3)),
                border = new RectOffset(box, 1, pad + box / 2 - 1, pad + box / 2 - 1), fixedHeight = 0, fixedWidth = 0,
            };
            foreach (GUIStyleState s in new[] { Toggle.normal, Toggle.hover, Toggle.active, Toggle.focused })
            {
                s.background = toggleOff;
                s.textColor = Text;
            }
            foreach (GUIStyleState s in new[] { Toggle.onNormal, Toggle.onHover, Toggle.onActive, Toggle.onFocused })
            {
                s.background = toggleOn;
                s.textColor = Text;
            }
            // a slider as tall as the row's field, with a thumb one can see at 4K
            Slider = new GUIStyle(GUI.skin.horizontalSlider) { fixedHeight = R(22), border = new RectOffset(2, 2, 2, 2), margin = new RectOffset(R(4), R(4), R(2), R(2)) };
            Slider.normal.background = Slider.hover.background = Slider.active.background = Slider.focused.background = field;
            Thumb = new GUIStyle(GUI.skin.horizontalSliderThumb) { fixedWidth = R(16), fixedHeight = R(22), border = new RectOffset(2, 2, 2, 2) };
            Thumb.normal.background = Thumb.focused.background = thumb;
            Thumb.hover.background = thumbHover;
            Thumb.active.background = thumbActive;
        }

        /// <summary>The toggle's texture: the box (a frame, a dark inside at the window's opacity, an amber mark when on)
        /// in the first `box` columns, centred in a `box + 2 pad` tall image; the rest clear.</summary>
        static Texture2D ToggleBox(int box, int pad, bool on, float alpha)
        {
            int w = box + 2, h = box + 2 * pad, inset = Mathf.Max(2, box / 5);
            var t = new Texture2D(w, h, TextureFormat.RGBA32, false) { hideFlags = HideFlags.HideAndDontSave, filterMode = FilterMode.Point };
            var clear = new Color(0f, 0f, 0f, 0f);
            var frame = new Color(0.45f, 0.37f, 0.26f, 1f);
            var inner = new Color(0.07f, 0.065f, 0.06f, alpha);
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    int by = y - pad;
                    Color c = clear;
                    if (x < box && by >= 0 && by < box)
                    {
                        bool edge = x == 0 || x == box - 1 || by == 0 || by == box - 1;
                        bool mark = on && x >= inset && x < box - inset && by >= inset && by < box - inset;
                        c = edge ? frame : mark ? Amber : inner;
                    }
                    t.SetPixel(x, y, c);
                }
            t.Apply();
            return t;
        }

        /// <summary>A 5 x 5 box: inner colour with a frame, drawn nine-sliced (border 2).</summary>
        static Texture2D Framed(Color inner, Color frame)
        {
            var t = new Texture2D(5, 5, TextureFormat.RGBA32, false) { hideFlags = HideFlags.HideAndDontSave, filterMode = FilterMode.Point };
            for (int y = 0; y < 5; y++)
                for (int x = 0; x < 5; x++)
                    t.SetPixel(x, y, x == 0 || y == 0 || x == 4 || y == 4 ? frame : inner);
            t.Apply();
            return t;
        }

        static void Set(GUIStyle s, Texture2D normal, Texture2D hov, Texture2D act, Color text)
        {
            s.normal.background = normal;
            s.hover.background = hov;
            s.active.background = act;
            s.focused.background = normal;
            s.normal.textColor = s.hover.textColor = s.active.textColor = s.focused.textColor = text;
        }
    }
}
