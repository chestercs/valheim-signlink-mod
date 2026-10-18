using System.Collections.Generic;
using System.Text;
using SignLink.Pictures;
using SignLink.Signs;
using TMPro;
using UnityEngine;
using PetyusPolisz.Kit;

namespace SignLink.UI.Sources
{
    /// <summary>Text: one sign's text as it is, any length up to a cap the editor can take (IMGUI text areas slow down
    /// on megabytes), tags as the game reads them. Put writes it as the stack's only sign (copies go, as a one-text
    /// Paste does), sized as chosen (SizeRows), placed and posed as 3 Place says, and marks it "text", so selecting
    /// the sign later opens this source with it. The editor always holds the text as made: a sign's own text is loaded
    /// with its fit taken off (Held.Made, 0.20.68), and the live preview (ContentPreview) shows it as FitTexts.ForPut
    /// writes it (0.20.129). Its light is in the text itself: the Light row wraps the text in the glowing font or takes
    /// those tags out (SignGlow), and the choice's light stays "as made".</summary>
    internal sealed class TextSource : Source
    {
        public const int MaxChars = 50000;
        const string AreaName = "text.area";
        /// <summary>The preview is worked out this long after the text or its fit last changed.</summary>
        const float PreviewAfter = 0.3f;

        /// <summary>The editor's buffer (the text area writes it in any event) and its copy at the last Layout: what the
        /// verb, the problem and the sizes read, so a keystroke does not change the controls between Layout and Repaint.</summary>
        string text = "", shown = "";
        Vector2 scroll;
        Choice shownFor;
        string movableFor;
        bool movable = true;
        readonly TemplateRows templates = new TemplateRows();
        /// <summary>Undo and Redo (TextHistory): seen once a Layout; a change made at once (SetText) is a step of its own.</summary>
        readonly TextHistory history = new TextHistory();
        bool discrete;
        // the preview: the sign it is for, the text it was worked out from and as written (FitTexts), when the text last changed
        Sign previewOn;
        string previewText, previewSeen = "", previewOnSign, previewKey, measuredText;
        Fit previewPose;
        int previewSerial;
        float changedAt;

        public override string Title => "Text";
        public override SignRecipe.Kinds Kind => SignRecipe.Kinds.Text;

        /// <summary>A text the editor cannot take (over MaxChars) is not loaded: the bar says so (Session.Missing).</summary>
        public override bool Load(SignRecipe r, Choice into, Session s)
        {
            if (into.Text.Length > MaxChars) return false;
            into.Recipe = SignRecipe.ForText();
            return true;
        }

        /// <summary>The editor takes the choice slot's text when the session switched slots: a sign holding a text was
        /// selected, or the player's own choice is back (its text with it; a sign holding something else leaves the
        /// editor as it is).</summary>
        void Take(Session s)
        {
            shownFor = s.Chosen;
            if ((s.Chosen.Recipe.Kind == SignRecipe.Kinds.Text || !s.Editing) && s.Chosen.Text.Length <= MaxChars) text = s.Chosen.Text;
            else if (s.Editing) text = "";                                  // a sign holding something else: not the text of
                                                                            // before (0.20.89; the own one waits in its slot)
            heldSeen = s.Held.Text;
            madeSeen = s.Held.Made;
            adopt = null;
            history.Reset(text);                                            // another sign's text: nothing to go back to
        }

        /// <summary>Is the selected sign's own text loaded (a sign this source wrote)? The editor holds it as made
        /// (Held.Made: its fit taken off, 0.20.68), as every text it holds: the put sizes and places it as chosen.</summary>
        static bool Own(Session s) => s.Editing && s.Held.Recipe.Kind == SignRecipe.Kinds.Text;

        /// <summary>The sign's text as last seen, and as made: a refit rewrites it, and the editor follows unless it was
        /// edited.</summary>
        string heldSeen = "", madeSeen = "";

        /// <summary>The text this editor just put on (Act): the sign's text as made replaces it when it comes back, unless
        /// it was typed on meanwhile.</summary>
        string adopt;

        public override void Tick(Session s, float now)
        {
            if (!ReferenceEquals(s.Chosen, shownFor)) Take(s);
            if (Own(s) && s.Held.Text != heldSeen)
            {
                // rewritten by a refit, or by this editor's own put (0.20.91, review: a typed "<size=3.0>" came back as
                // "<size=3>", and the button kept offering Update): follow it, as made the same text, its numbers aside
                if ((text == madeSeen || text == adopt) && s.Held.Made.Length <= MaxChars)
                {
                    text = s.Held.Made;
                    history.Reset(text);                                    // the old place is not a step back
                }
                heldSeen = s.Held.Text;
                madeSeen = s.Held.Made;
                adopt = null;
            }
            else if (s.Editing && !Own(s) && s.Held.Text != heldSeen)
            {
                // the sign now holds a piece or a picture (put on from another tab): the text it held before is not left
                // in the editor to be put back over it, unless it was typed on (0.20.147: the old first layer of Huginn
                // and Muninn stayed here, its preview showed, and a put left one sign of three)
                if (text == madeSeen || text == adopt)
                {
                    text = "";
                    history.Reset(text);
                }
                heldSeen = s.Held.Text;
                madeSeen = s.Held.Made;
                adopt = null;
            }
            history.Seen(text, now, discrete);
            discrete = false;
            shown = text;
            s.Chosen.Text = shown;
            s.Chosen.Recipe = shown.Length > 0 ? SignRecipe.ForText() : SignRecipe.None;
            s.Chosen.Light = GlowMode.AsMade;                               // the text carries its own tags
            WorkOutPreview(s, now);
        }

        /// <summary>The text as writing it leaves it (FitTexts.ForPut, as SignFit.Put: sized and placed as chosen; a text
        /// SignLink cannot resize at 1, only posed), for the live preview (ContentPreview: on this computer only, in the pose
        /// to come, 0.20.129; the sign's own stack before), worked out PreviewAfter seconds after the text or its fit last
        /// changed (not at every keystroke or slider step: a TMP rebuild each); an emptied text, another sign or a sign that
        /// cannot be changed show the sign as it is. The preview's first copy is measured once it shows it (Measure).</summary>
        void WorkOutPreview(Session s, float now)
        {
            Fit fit = s.Chosen.Fit;
            if (shown != previewSeen || !fit.Same(previewSeenFit))
            {
                previewSeen = shown;
                previewSeenFit = fit;
                changedAt = now;
            }
            bool can = Source.Ready(s);
            if (previewOn && (!can || !ReferenceEquals(previewOn, s.Target) || shown.Length == 0)) DropPreview();
            Measure(s);
            if (!can || shown.Length == 0 || now - changedAt < PreviewAfter) return;
            if (ReferenceEquals(previewOn, s.Target) && previewText == shown && fit.Same(previewFit)) return;
            FitTexts.Result r;
            try { r = FitTexts.ForPut(new[] { shown }, 0, 0, false, fit, false); }
            catch (System.ArgumentException)                                // too large or too far for a sign: the put
            {                                                               // refuses it; shown as typed
                r = new FitTexts.Result { Texts = new[] { shown }, Fit = fit.PoseOnly() };
            }
            previewOn = s.Target;
            previewText = shown;
            previewFit = fit;
            previewOnSign = r.Texts[0];
            previewPose = r.Fit;
            previewMade = r;
            previewKey = "text|" + ++previewSerial;
        }

        Fit previewFit, previewSeenFit;
        FitTexts.Result previewMade;

        void DropPreview()
        {
            previewOn = null;
            previewText = null;
            previewOnSign = null;
        }

        /// <summary>The text as the put writes it, in the pose the rows say now (a Tilt slider moves it at once; the text
        /// is worked out again once the rows stand still).</summary>
        public override PreviewContent Preview(Session s)
        {
            if (previewOnSign == null || !ReferenceEquals(previewOn, s.Target)) return null;
            return PreviewContent.Made(new[] { previewOnSign }, previewMade, previewKey, anySize: true).Posed(s.Chosen.Fit);
        }

        public override void OnClose() => DropPreview();

        /// <summary>The templates, the Light row, the editor's row of buttons (Insert, then the box's own) and the box
        /// under them, the line of what holds it back last (1.0.2, the author: the buttons by Insert, Insert in the
        /// way-on yellow whenever it can be pressed, the light up over the editor; before, the buttons and the light
        /// sat under the box, Insert in the template's group).</summary>
        public override void Draw(Session s, SignLinkWindow w, float now)
        {
            bool can = Source.Ready(s);                                     // a text is written for a sign
            bool tooLong = s.HasTarget && s.Held.Text.Length > MaxChars;
            int room = MaxChars - text.Length;                              // a QR code must fit the box
            GUI.enabled = can;
            templates.Draw(w, room);

            // the light, read off the text (a glowing tag anywhere: glowing; none: lit like any sign text); a click
            // rewrites the text at a later Layout
            {
                bool empty = shown.Length == 0;
                int at = empty ? -1 : SignGlow.IsGlowing(shown) ? 0 : 1;
                GUI.enabled = can && !empty;
                int pick = Widgets.Choice("Light", at, LightRow.PictureNames, 320f);
                if (pick >= 0 && pick != at)
                {
                    GlowMode m = pick == 0 ? GlowMode.Glowing : GlowMode.Lit;
                    w.Later(() => SetText(SignGlow.Apply(text, m)));
                }
            }
            Widgets.Space(6);
            Widgets.Rule();                                                 // the light above, the editor below (1.0.2, the owner: a separation)
            Widgets.Space(4);

            // Reset: what the sign holds (0.20.147, the author: on a sign holding Huginn and Muninn it loaded the first
            // layer as a text, and putting that on left one sign of three; why not fix it instead?): a text into
            // the editor; a piece or a picture back in its own source, the sign's own (the pose and size chosen stay)
            bool aText = s.Held.Recipe.Kind == SignRecipe.Kinds.Text || (s.Held.Recipe.Kind == SignRecipe.Kinds.None && s.Stack.Count <= 1);
            bool known = s.Held.Recipe.Kind == SignRecipe.Kinds.Art || s.Held.Recipe.Kind == SignRecipe.Kinds.Picture;
            using (Widgets.Horizontal())
            {
                GUI.enabled = can && templates.Problem(room) == null;       // a template picked and ready: Insert lights up
                if (Widgets.SmallGoButton("Insert", Widgets.W(100)))        // the picked template's lines at the caret
                {
                    List<Snippets.Line> lines = templates.Lines();
                    int caret = Caret();
                    if (lines != null && lines.Count > 0) w.Later(() => SetText(Snippets.Insert(text, lines, caret)));
                }
                GUI.enabled = can && !s.Held.Empty && (aText ? !tooLong : known && s.Missing == null);
                if (Widgets.Button("Reset", Theme.SmallButton, Widgets.W(80)))   // the sign's text as made (0.20.71: Reset instead of "load from the sign")
                    w.Later(() =>
                    {
                        if (aText) SetText(s.Held.Made);
                        else if (s.LoadInto != null)
                        {
                            SetText("");                                    // not offered again over it (0.20.148, review)
                            if (s.LoadInto(s.Held.Recipe.Copy(), s.Chosen)) s.Chosen.Light = Change.LightOf(s.Held);
                        }
                    });
                GUI.enabled = can && shown.Length > 0;
                if (Widgets.Button("Clear", Theme.SmallButton, Widgets.W(80))) w.Later(() => SetText(""));
                GUI.enabled = can && history.CanUndo;
                if (Widgets.Button("Undo", Theme.SmallButton, Widgets.W(70))) w.Later(() => Step(history.Undo()));
                GUI.enabled = can && history.CanRedo;
                if (Widgets.Button("Redo", Theme.SmallButton, Widgets.W(70))) w.Later(() => Step(history.Redo()));
                GUI.enabled = true;
                GUILayout.Label("Ctrl+Z", Theme.KeyHint, GUILayout.ExpandWidth(false));   // Keys (1.0.2: keys shown)
                GUILayout.Label("Ctrl+Y", Theme.KeyHint, GUILayout.ExpandWidth(false));
            }
            Keys(w);                                                        // Ctrl+Z, Ctrl+Y: before the box takes the keys
            GUI.enabled = can;
            GUI.SetNextControlName(AreaName);
            using (Widgets.Scroll(ref scroll, 220))
                text = GUILayout.TextArea(text, MaxChars, Theme.Area, GUILayout.ExpandHeight(true));   // the verb follows at the next Layout (Tick)
            GUI.enabled = true;
            // what holds the editor back, under the box
            if (!can) Widgets.Hint(Source.NotReady(s));
            else if (s.HasTarget && !s.Held.Empty && !aText)                // a picture or a piece first: its length is no text's
                GUILayout.Label(known && s.Missing == null ? "Holds a piece" : "Holds a piece (unreadable)", Theme.SideNote);
            else if (tooLong)
                GUILayout.Label($"{s.Held.Text.Length} characters: too long", Theme.Warn);
        }

        public override void DrawSize(Session s, SignLinkWindow w)
        {
            bool empty = shown.Length == 0, movable = !empty && Movable(s), was = GUI.enabled;
            GUI.enabled = was && Source.Ready(s);
            size.Draw(s, Size(s, out Vector2 shownM) ? shownM : Vector2.zero, movable, empty ? null
                : movable ? null : "Fixed size",
                SnapScale);                                                 // its exact sizes for Shift + wheel (0.20.98)
            GUI.enabled = was;
        }

        public override bool Reverts(Session s) => s.HasTarget && (Own(s) ? text != s.Held.Made : text.Length > 0);

        /// <summary>The sign's own text again, or none typed when it holds something else.</summary>
        public override void Revert(Session s) => SetText(Own(s) && s.Held.Made.Length <= MaxChars ? s.Held.Made : "");

        /// <summary>A change made at once (a template, the light, Load, Clear, Undo changes): a step of its own.</summary>
        void SetText(string t)
        {
            text = t ?? "";
            discrete = true;
        }

        /// <summary>Undo or Redo gave this text: the box shows it, its caret at the end (the editor keeps its own copy and
        /// caret while it has the focus).</summary>
        void Step(string t)
        {
            text = t;
            if (GUI.GetNameOfFocusedControl() != AreaName) return;
            if (GUIUtility.GetStateObject(typeof(TextEditor), GUIUtility.keyboardControl) is TextEditor editor)
            {
                editor.text = t;
                editor.cursorIndex = editor.selectIndex = t.Length;
            }
        }

        /// <summary>Ctrl+Z steps back, Ctrl+Y or Ctrl+Shift+Z forward (Cmd on a Mac), while the box or nothing has the
        /// keys; the keys are used up so the box does not take them as typing.</summary>
        void Keys(SignLinkWindow w)
        {
            Event e = Event.current;
            if (e.type != EventType.KeyDown || !(e.control || e.command)) return;
            string focused = GUI.GetNameOfFocusedControl();
            if (GUIUtility.keyboardControl != 0 && focused != AreaName) return;   // another field's own keys
            bool undo = e.keyCode == KeyCode.Z && !e.shift, redo = e.keyCode == KeyCode.Y || (e.keyCode == KeyCode.Z && e.shift);
            if (undo && history.CanUndo) w.Later(() => Step(history.Undo()));
            else if (redo && history.CanRedo) w.Later(() => Step(history.Redo()));
            if (undo || redo || e.character == '\u001a' || e.character == '\u0019') e.Use();
        }

        readonly SizeRows size = new SizeRows("size.text");

        /// <summary>Where the text area's caret is (a template goes in there), or the end of the text when the area
        /// has no focus.</summary>
        int Caret()
        {
            if (GUI.GetNameOfFocusedControl() != AreaName) return text.Length;
            var editor = GUIUtility.GetStateObject(typeof(TextEditor), GUIUtility.keyboardControl) as TextEditor;
            return editor != null ? Mathf.Clamp(editor.cursorIndex, 0, text.Length) : text.Length;
        }

        /// <summary>The text's size as made (metres), measured off the preview (Measure), so the frame in the world and the
        /// Size rows follow whatever is typed, templates or tags, and stay put while the text is resized.</summary>
        public override bool Size(Session s, out Vector2 metres)
        {
            metres = Vector2.zero;
            if (!Measured(s)) return false;
            metres = measuredM;
            return true;
        }

        /// <summary>Where the text's middle sits as made (wu from the sign's centre), measured with its size.</summary>
        public override bool Centre(Session s, out double x, out double y)
        {
            x = y = 0;
            if (!Measured(s)) return false;
            x = measuredX;
            y = measuredY;
            return true;
        }

        /// <summary>The size the text goes on at: the chosen scale snapped to exact line heights, 1 for a text SignLink
        /// cannot resize (FitTexts.ForPut, as the put does), for the frame in the world.</summary>
        public override double ShownScale(Session s)
        {
            if (!Movable(s)) return 1;
            double k = SnapScale(s.Chosen.Fit.Scale);
            return k > 0 ? k : 1;
        }

        /// <summary>The size a scale really takes for the text shown (exact line heights), 0 when none does.</summary>
        double SnapScale(double k)
        {
            if (!ReferenceEquals(heightsFor, shown))
            {
                heightsFor = shown;
                heights = TextTransform.Heights(new[] { shown });
            }
            return TextTransform.Snap(heights, k);
        }

        string heightsFor;
        List<double> heights = new List<double>();

        /// <summary>The last measurement belongs to the selected sign and its preview still shows (the last one shown
        /// while the next waits its third of a second: no flicker of the frame while typing).</summary>
        bool Measured(Session s) => measuredOn && previewOn && ReferenceEquals(measuredFor, s.Target);

        /// <summary>The text's size and middle as made, read off the preview's first copy once it shows the text
        /// (ContentPreview.First: TMP's text bounds, its mesh built; 1 text unit = 5 cm = 2 wu) through the fit it is shown
        /// with (0.20.68: the size was read through the sign's stored fit, a factor off whenever the editor did not hold
        /// exactly the sign's fitted text; 0.20.91: a text the put leaves as it is, at 1). The last one stands until then
        /// (no flicker of the frame while typing).</summary>
        void Measure(Session s)
        {
            if (previewOnSign == null || ReferenceEquals(measuredText, previewOnSign)) return;
            TMP_Text on = Plugin.Preview.First;
            if (on == null || on.text != previewOnSign)
            {
                // nothing new to put on (the sign's own text, or one just put): the sign shows it itself (0.20.130, review)
                Sign real = s.Verb == Verb.None ? (s.Stack.Count > 0 ? s.Stack[0] : s.Target) : null;
                on = real && real.m_textWidget && real.m_textWidget.isActiveAndEnabled ? real.m_textWidget : null;
            }
            if (on == null) return;
            if (!Ink.Rect(on, out Vector2 lo, out Vector2 hi)) return;     // no mesh yet: measured at a later Layout
            measuredText = previewOnSign;                                   // where its glyphs draw, as Show all frames it (0.20.155)
            Fit fit = previewPose;
            double k = fit.Scale > 0 ? fit.Scale : 1;
            measuredM = new Vector2((float)((hi.x - lo.x) * 0.05 / k), (float)((hi.y - lo.y) * 0.05 / k));
            fit.MadeCentre((lo.x + hi.x), (lo.y + hi.y), out measuredX, out measuredY);
            measuredPx = lo.x + hi.x;                                       // the middle where it showed (wu), and the fit
            measuredPy = lo.y + hi.y;                                       // that showed it
            measuredPose = fit;
            measuredOn = previewOn;
            measuredFor = s.Target;
        }

        Sign measuredOn, measuredFor;
        Vector2 measuredM;
        double measuredX, measuredY, measuredPx, measuredPy;
        Fit measuredPose;

        /// <summary>The text's middle in a fit, moved from where it was measured the way the put moves a text (made at the
        /// sign's middle, c0 0: Fit.Between), so a Rotate turns it round the sign's middle with the put, not round its own
        /// (0.20.157, review); a text the put cannot move stays where it showed.</summary>
        public override bool Middle(Session s, Fit fit, bool still, out double x, out double y)
        {
            x = y = 0;
            if (!Measured(s)) return false;
            if (still)
            {
                x = measuredPx;
                y = measuredPy;
                return true;
            }
            Fit.Between(measuredPose, fit, 0, 0, out double k, out double dx, out double dy);
            x = k * measuredPx + dx;
            y = k * measuredPy + dy;
            return true;
        }

        /// <summary>Its text can be resized and moved (TextTransform.Supports), checked once per text.</summary>
        public override bool Movable(Session s)
        {
            if (!ReferenceEquals(movableFor, shown))
            {
                movableFor = shown;
                movable = shown.Length == 0 || TextTransform.Supports(new[] { shown }, out _);
            }
            return movable;
        }

        public override string Problem(Session s) => shown.Length == 0 ? "Type a text above" : null;

        public override string What(Session s) => "This text";

        /// <summary>The red level of the text shown (the same in every event of a frame), counted once per text (0.20.132,
        /// review: the footer, the area meter and the tabs asked up to four times an event, each a pass over 50 000
        /// characters), as Movable and SnapScale keep theirs.</summary>
        public override bool Heavy(Session s, out double loadSeconds, out long saveBytes, out long largest)
        {
            if (!ReferenceEquals(heavyFor, shown))
            {
                heavyFor = shown;
                heavyGlyphs = SaveCost.Glyphs(new[] { shown });
                heavyBytes = SaveCost.Bytes(new[] { shown });
                heavyLargest = Encoding.UTF8.GetByteCount(shown);
            }
            loadSeconds = SaveCost.LoadSeconds(heavyGlyphs);
            saveBytes = heavyBytes;
            largest = heavyLargest;
            return SaveCost.Heavy(loadSeconds, saveBytes, largest);
        }

        string heavyFor;
        long heavyGlyphs, heavyBytes, heavyLargest;

        /// <summary>Writes the text as the stack's only sign, as typed (its tags are its light), sized, placed and posed
        /// as chosen (SignFit.Put; a text SignLink cannot resize goes on as it is), and marks it as this source's.</summary>
        public override void Act(Session s, SignLinkWindow w, Verb v)
        {
            if (!s.HasTarget) return;
            string t = text;
            Fit fit = s.Chosen.Fit;
            Sign target = s.Target;
            w.Act(() =>
            {
                DropPreview();                                              // the saved text is written: it shows itself
                // the editor holds every text as made, the sign's own too (Held.Made): sized and placed as chosen, as
                // the preview showed it (FitTexts.ForPut)
                List<Sign> made = SignFit.Put(target, new[] { t }, 0, 0, false, fit, "", 0f);
                SignMarks.SetSource(made[0], SignRecipe.ForText().ToString());
                adopt = t;
                Fit put = SignFit.Read(made[0], out _, out _, out _);
                s.Chosen.Fit = put;                                         // as put (a snapped size): nothing left to apply (0.20.148)
                w.Select(made[0]);
                string how = put.Describe();
                return $"the text is on the sign{how}.";
            });
        }
    }
}
