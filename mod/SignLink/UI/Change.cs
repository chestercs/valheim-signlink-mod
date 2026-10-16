using SignLink.Pictures;
using SignLink.Signs;

namespace SignLink.UI
{
    /// <summary>What the footer's main button does: Place a new picture or text, Put a new piece, Update the held item
    /// made again with other settings, Refit the held item's texts (another fit or light, no remaking), Apply a fit
    /// to content no source knows.</summary>
    internal enum Verb { None, Place, Put, Update, Refit, Apply }

    /// <summary>What the selected sign holds, as far as the sources know it (Session reads it; no Unity: tested).</summary>
    internal sealed class Held
    {
        public bool HasSign;
        public bool Empty = true;
        /// <summary>Its texts can be resized and moved (TextTransform.Supports).</summary>
        public bool Movable;
        public SignRecipe Recipe = SignRecipe.None;
        public Fit Fit = Fit.AsMade;
        /// <summary>The first sign's text as it is on the sign.</summary>
        public string Text = "";
        /// <summary>The same as made, what the Text source edits: a text it put has its fit taken off again
        /// (TextTransform.Unfit), any other text as it is (0.20.68).</summary>
        public string Made = "";
        public long Bytes;
        /// <summary>A glyph art piece on several signs, put before 0.20.145: its layers carry no anchors, so the game may
        /// draw a rear layer over a front one (docs/sign-rendering.md §5: the Dragon crest's rune hid behind its red disc).
        /// Update puts it on again from the library, anchored (0.20.169).</summary>
        public bool OldLayers;
    }

    /// <summary>What the tabs chose: an item with its settings (a recipe) or none, and how it should sit.</summary>
    internal sealed class Choice
    {
        public SignRecipe Recipe = SignRecipe.None;
        public Fit Fit = Fit.AsMade;
        public GlowMode Light = GlowMode.AsMade;
        public string Text = "";
        /// <summary>The Art source: an animated piece goes on as its first frame (Moving off). A piece picked from the list
        /// starts moving (0.20.167, the author: it always starts switched on; still by default 0.20.144-0.20.166, and once
        /// switched off it stayed off for every piece after); a sign's own piece starts as it was put.</summary>
        public bool Still;

        public Choice Copy() => new Choice { Recipe = Recipe.Copy(), Fit = Fit, Light = Light, Text = Text, Still = Still };
    }

    /// <summary>The footer rule (the spec's table): Place a new picture or text, Put a new piece, Update the held item
    /// with other settings, Refit it to another fit or light, Apply another fit to content no source knows, else
    /// nothing.</summary>
    internal static class Change
    {
        /// <summary>Stacks up to this size take fit and light changes live (half a second after the slider stops).</summary>
        public const long LiveBytes = 150 * 1024;

        public static Verb Decide(Held h, Choice c)
        {
            if (!h.HasSign) return Verb.None;
            if (c.Recipe.Kind == SignRecipe.Kinds.None)
                return h.Empty ? (SamePose(h.Fit, c.Fit) ? Verb.None : Verb.Apply)   // an empty sign still takes a pose
                     : !c.Fit.Same(h.Fit) || c.Light != GlowMode.AsMade ? Verb.Apply : Verb.None;
            if (!h.Recipe.SameItem(c.Recipe))
                return c.Recipe.Kind == SignRecipe.Kinds.Art ? Verb.Put : Verb.Place;
            if (!SameSettings(h, c)) return Verb.Update;                    // made again from its file or piece
            if (h.OldLayers && c.Recipe.Kind == SignRecipe.Kinds.Art) return Verb.Update;   // its layers drawn in order again
            if (c.Fit.Same(h.Fit) && LightOf(h) == c.Light) return Verb.None;
            return Verb.Refit;                                              // its texts move, nothing is made again (a moving
        }                                                                   // GIF's screen comes back from memory: ScreenRestore)

        /// <summary>A Refit or Apply that may happen by itself, half a second after the rows stop: the stack is small
        /// and the pose is not part of the change (a new tilt or turn makes every sign again: that waits for the
        /// button, so a dragged slider does not rebuild the stack at every pause).</summary>
        public static bool Live(Held h, Choice c, Verb v) =>
            (v == Verb.Refit || v == Verb.Apply) && h.Bytes < LiveBytes && SamePose(h.Fit, c.Fit);

        /// <summary>The content's own changes against what the sign holds, one rule for 3 Place's Changes block and the
        /// footer's words (0.20.190; each had its own copy): its size (the fit's scale; a picture's metres are its
        /// source's), its place along the sign's face, its light (a pick's against the held item's, else any but as made).</summary>
        public static bool SizeChanged(Held h, Choice c) => System.Math.Abs(c.Fit.Scale - h.Fit.Scale) > 1e-4;

        public static bool PlaceChanged(Held h, Choice c) =>
            System.Math.Abs(c.Fit.Right - h.Fit.Right) >= 1e-3 || System.Math.Abs(c.Fit.Up - h.Fit.Up) >= 1e-3 ||
            System.Math.Abs(c.Fit.Out - h.Fit.Out) >= 1e-3;

        public static bool LightChanged(Held h, Choice c) =>
            c.Recipe.Kind != SignRecipe.Kinds.None ? c.Light != LightOf(h) : c.Light != GlowMode.AsMade;

        public static bool SamePose(Fit a, Fit b) =>
            System.Math.Abs(a.Tilt - b.Tilt) < 0.05 && System.Math.Abs(Fit.Angle(a.Turn - b.Turn)) < 0.05 && a.SamePivot(b);

        internal static bool SameSettings(Held h, Choice c) =>
            h.Recipe.SameSettings(c.Recipe) && (c.Recipe.Kind != SignRecipe.Kinds.Text || c.Text == h.Made);   // both as made

        /// <summary>The light the Picture source gives the choice slot (0.20.91, review): a picture it holds glows or is lit
        /// as its options say (glow) while the slot says nothing; with nothing picked the slot's light stays as it is. It
        /// lit the slot before, so a sign holding what no source loads got Apply, live: 3 Place relit and rewrote it half a
        /// second after it opened, with no click.</summary>
        public static GlowMode PictureLight(GlowMode chosen, bool picked, bool glow) =>
            !picked || chosen != GlowMode.AsMade ? chosen : glow ? GlowMode.Glowing : GlowMode.Lit;

        /// <summary>What the Area line counts as the sign's bytes after the button (AreaMeter): the pick's when the button
        /// writes it (Place, Put, Update), else -1, the area as it is (0.20.91, review: an empty Text editor over a
        /// picture sign read as the sign emptied).</summary>
        public static long AreaBytes(Verb v, long bytes) => v == Verb.Place || v == Verb.Put || v == Verb.Update ? bytes : -1;

        /// <summary>A picture or piece other than the held one, chosen over a sign that holds something: the sign shows
        /// nothing on this computer meanwhile (HeldVeil, 0.20.95, the author: do not show the existing picture while editing
        /// once I pick new content), so only the new pick's frame shows where it goes. Since 0.20.129 HeldVeil hides the
        /// sign whenever the live preview shows too; this rule covers the picks the preview cannot show (yet: a picture or
        /// a piece still being read, a piece of more glyphs than a preview builds). The held item again with other
        /// settings keeps showing.</summary>
        public static bool HidesHeld(Held h, Choice c) =>
            h.HasSign && !h.Empty && (c.Recipe.Kind == SignRecipe.Kinds.Picture || c.Recipe.Kind == SignRecipe.Kinds.Art) &&
            !h.Recipe.SameItem(c.Recipe);

        /// <summary>How the held content is lit, as its recipe says (a picture's glow switch, a piece's light); as it is
        /// when nothing says.</summary>
        public static GlowMode LightOf(Held h) => LightOf(h.Recipe);

        /// <summary>How a recipe's content is lit: a picture's glow switch, a piece's light; as it is when nothing says.</summary>
        public static GlowMode LightOf(SignRecipe r) =>
            r.Kind == SignRecipe.Kinds.Picture ? (r.Glow ? GlowMode.Glowing : GlowMode.Lit)
            : r.Kind == SignRecipe.Kinds.Art ? r.Light
            : GlowMode.AsMade;
    }
}
