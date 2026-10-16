using System;
using SignLink.Pictures;
using SignLink.Signs;
using SignLink.UI.Sources;
using UnityEngine;

namespace SignLink.UI
{
    /// <summary>3 Place: how big the content is, where it sits, where the sign stands and how it turns, four sections
    /// that open and close (0.20.59, 0.20.66). Size: the picked item's size rows (its source's DrawSize), or what the sign
    /// holds (the FitPanel's). Content positioning: the FitPanel's content rows (Right, Up) and As made. Sign positioning:
    /// Move the sign's steps (MovePanel). Sign rotations: the sign's pose (the FitPanel's: standing, face up, face down,
    /// tilt, rotate) and its Turn (MovePanel's). Changes (MovePanel) under them both. The footer holds
    /// Undo changes for the whole menu and the one verb button (2 Content leads here: what goes on is chosen there, where and how it sits
    /// here, and here it goes on). An empty sign still takes a pose (Apply makes it again standing, or lying). The
    /// content's changes wait for their own Apply under the content's groups, beside Undo and Reset to default, as the
    /// sign's do (0.20.179, the author: odd mechanism, applying our changes at once; a small stack took a fit
    /// change live, half a second after the rows stopped, 0.20.x-0.20.178).</summary>
    internal sealed class PlaceTab : Tab
    {
        readonly FitPanel panel = new FitPanel("place");
        readonly MovePanel move;
        readonly ContentTab content;
        readonly Confirm confirm = new Confirm();
        // the rows show the choice; when the choice changed elsewhere (another sign's pose, Undo, a load) they take it,
        // else the choice takes the rows
        Choice slot;
        Fit seen = Fit.AsMade;

        public PlaceTab(SignLinkWindow w, ContentTab content) : base(w)
        {
            this.content = content;
            move = new MovePanel(w);
        }

        public override string Title => "3 Place";
        public override bool UsesTarget => true;
        public override void OnClose()
        {
            confirm.Disarm();
            move.Close();                                                   // a reset stays as it is (no Undo after the window)
        }

        /// <summary>Arrow keys move the sign while this tab is open (the window's Update).</summary>
        public void Keys() => move.Keys();

        public override void Body()
        {
            Session s = Window.Session;
            Source src = content.Current;
            float now = Time.unscaledTime;
            bool has = s.HasTarget;
            if (Event.current.type == EventType.Layout)
            {
                src.Tick(s, now);                                           // its keys and jobs go on while its list is not drawn
                bool pickedNow = s.Chosen.Recipe.Kind != SignRecipe.Kinds.None;
                panel.NaturalM = pickedNow ? (src.Size(s, out Vector2 m) ? m : Vector2.zero) : s.NaturalM;
                if (!ReferenceEquals(slot, s.Chosen) || !s.Chosen.Fit.Same(seen))
                    panel.Fit = s.Chosen.Fit;                               // changed elsewhere: the rows take it
            }
            bool picked = s.Chosen.Recipe.Kind != SignRecipe.Kinds.None;
            bool holds = picked || !s.Held.Empty;                           // content the content rows can act on
            bool can = has && s.Reason == null;                             // a ward: nothing here can happen (the bar says why)

            // two groups, the content's and the sign's, a rule between them, each opening and closing, its parts under
            // titles of their own (0.20.180, the author: only 2 groups, content and [sign]; inside them, of course,
            // titled separators may part things; four groups 0.20.59-0.20.179)
            bool movable = picked ? src.Movable(s) : s.Held.Movable;
            bool contentRows = can && holds && movable;                // nothing usable in them: closed (0.20.77)
            if (Widgets.Foldout("place.content", "Content", usable: contentRows))
            {
                GUI.enabled = can && holds;
                Widgets.SubHeader("Positioning", first: true);
                panel.DrawContent(movable);
                Widgets.SubHeader("Size");
                if (picked) src.DrawSize(s, Window);                        // a piece's or a text's scale, a picture's metres
                else panel.DrawSize(movable);                               // what the sign holds
                GUI.enabled = true;
            }
            if (Event.current.type == EventType.Layout && picked)
                panel.SizePct = (float)(s.Chosen.Fit.Scale * 100);          // the source's rows set the scale: the panel keeps it
            DrawContentChanges(s, src, can, holds, movable, picked);

            Widgets.Space(6);
            Widgets.Rule();                                                 // the content above, the sign below
            Widgets.Space(4);
            // how the sign turns, then where it stands; Changes under the group covers moves, turns and the pose
            if (Widgets.Foldout("place.signs", "Sign"))
            {
                GUI.enabled = can;
                Widgets.SubHeader("Rotations", first: true);
                panel.DrawRotations(Window, () => move.DrawTurn(s, can));   // poses, Tilt, Turn, Rotate
                GUI.enabled = true;
                Widgets.SubHeader("Positioning fine-tune");                 // 0.20.85, the author
                move.Draw(s, can);
            }
            move.DrawChanges(s, can);
            if (Event.current.type == EventType.Layout)                     // ... and the choice takes the rows
            {
                s.Chosen.Fit = panel.Fit;
                slot = s.Chosen;
                seen = panel.Fit;
            }
            string live = Plugin.Preview.Showing ? "The sign shows the result now, for you only; Apply puts it on for everyone. "   // 0.20.129
                : s.Verb == Verb.Refit || s.Verb == Verb.Apply ? "The button below applies the changes; everyone nearby loads the sign again. "
                : "";
            string hint;
            if (!has) hint = "Select a sign first.";
            else if (picked)
                hint = live + "Its light is on 2 Content; here its size, its place and the sign's pose." +
                       (src.Movable(s) ? "" : " This item keeps the place it was made with; Rotate turns it with the sign.");
            else if (s.Held.Empty)
                hint = live + "The sign is empty. Its pose can still change (the button below); 2 Content puts content on it.";
            else
                hint = live + (!s.Held.Movable
                    ? "This content cannot be resized or moved (the game sizes it, or tags SignLink does not know); the sign can still tilt and turn."
                    : s.CentreKnown ? (Math.Abs(s.C0x) < 0.01 && Math.Abs(s.C0y) < 0.01 ? "Size and place count from the sign's middle."
                                       : "Size and place count from where the piece was made; put it on again to centre it.")
                    : "SignLink did not put this piece on, so Rotate turns it around the sign's middle.");
            Widgets.Hint(hint);
        }

        /// <summary>The content's changes, as the sign's (MovePanel.DrawChanges): nothing goes on by itself (0.20.179, the
        /// author: there could be a block there too, as for the sign ... an Undo that resets to what was on the sign when
        /// I started editing, and an Apply button of its own). Apply puts the size, the place and the light on (the held item made
        /// again where its settings changed); Undo drops them, back to what the sign holds now (0.20.187, the author:
        /// revert to the original, the sign's current settings; to what it held when selected 0.20.179-186,
        /// which after an Apply was another pose's place); Reset to default the content in the sign's middle. A new item
        /// goes on with the footer's button.</summary>
        void DrawContentChanges(Session s, Source src, bool can, bool holds, bool movable, bool picked)
        {
            Verb v = s.Verb;
            Fit c = s.Chosen.Fit, h = s.Held.Fit;
            bool size = Change.SizeChanged(s.Held, s.Chosen) || src.SizeDiffers(s, s.Held.Recipe);
            bool place = Change.PlaceChanged(s.Held, s.Chosen);
            bool light = Change.LightChanged(s.Held, s.Chosen);
            // settings only where they differ: an Update the sign is offered for its layers (Held.OldLayers) is no change made
            // here (0.20.187: a Dragon crest opened with "settings changed · not applied yet")
            bool settings = v == Verb.Update && !Change.SameSettings(s.Held, s.Chosen);
            bool pending = can && (v == Verb.Refit || v == Verb.Apply || v == Verb.Update) && (size || place || light || settings);
            // a new tilt or turn of the sign waits for the footer's button, which puts both on (0.20.189, review: this Apply
            // put the whole choice on, the pose too)
            bool posed = !Change.SamePose(h, c);
            var what = new System.Collections.Generic.List<string>();
            if (size) what.Add("size");
            if (place) what.Add("place");
            if (light) what.Add("light");
            if (settings && what.Count == 0) what.Add("settings");         // 2 Content's: a resolution, a text
            string problem = v == Verb.Update ? src.Problem(s) ?? src.Limit(s) : null;
            bool busy = Window.Busy || ContentTab.SourcesBusy || src.Busy;
            bool was = GUI.enabled;
            bool apply, undo, reset;
            Widgets.Space(4);
            using (Widgets.Vertical(Theme.Section))
            {
                using (Widgets.Horizontal())
                {
                    GUILayout.Label("Changes", Theme.Label, Widgets.W(Widgets.LabelW));
                    GUILayout.Label(!s.HasTarget || !holds ? ""
                                    : pending ? string.Join(", ", what.ToArray()) + " changed · not applied yet: only you see it" +
                                                (posed ? "; with the sign's new pose: the button below" : "")
                                    : v == Verb.Place || v == Verb.Put ? "a new item: the button below puts it on"
                                    : "none: as the sign holds it",
                                    pending ? Theme.Notice : Theme.Label, Widgets.W(Widgets.RestW));
                }
                using (Widgets.Horizontal())
                {
                    float wide = (Widgets.ContentWidth - 30) * 0.62f, rest = Widgets.ContentWidth - 30 - wide - 8f;
                    GUI.enabled = was && pending && !posed && problem == null && !busy;
                    apply = Widgets.NextButton(busy && pending ? "Working ..." : "Apply content changes", 26f, wide, less: true);
                    GUI.enabled = was && pending;                           // what waits here, not a new pick (0.20.189, review)
                    undo = Widgets.Button("Undo", Theme.ButtonLess, Widgets.W(rest), Widgets.H(26));
                }
                using (Widgets.Horizontal())
                {
                    // back to the middle, what the positioning group holds (0.20.71, the author: the "original size, middle"
                    // button is a lie, it only puts it in the middle and does not change the size): the size is Content size's
                    bool off = Math.Abs(c.Right) > 0.01 || Math.Abs(c.Up) > 0.01 || Math.Abs(c.Out) > 0.01;
                    GUI.enabled = was && can && holds && movable && off;
                    reset = Widgets.Button("Reset to default", Theme.ButtonLess, Widgets.W(180), Widgets.H(26));
                    GUI.enabled = was;
                    GUILayout.Label("the content in the sign's middle", Theme.SideNote, Widgets.H(26));
                }
                if (pending && problem != null && !busy) Widgets.Hint(problem);
            }
            GUI.enabled = was;
            if (apply)
            {
                Window.KeepOpen();                                          // not a put: its message keeps the window
                if (v == Verb.Update && src.Heavy(s, out double load, out long save, out long largest))   // as the footer's button asks
                    confirm.Arm(Window, s.Chosen.Recipe.ToString() + "|" + s.Chosen.Fit.Describe(s.Chosen.Light), src.What(s), load, save,
                                largest, src.Lighter(s), keepOpen: true);
                else ContentTab.Perform(Window, src, v);
            }
            if (undo)
                Window.Later(() =>
                {
                    Fit back = s.Chosen.Fit;                            // the content's part: the sign's pose is Sign's
                    back.Scale = s.Held.Fit.Scale;
                    back.Right = s.Held.Fit.Right;
                    back.Up = s.Held.Fit.Up;
                    back.Out = s.Held.Fit.Out;
                    s.Chosen.Fit = back;
                    s.Chosen.Light = picked ? Change.LightOf(s.Held) : GlowMode.AsMade;
                    src.SizeAs(s, s.Held.Recipe);                           // a picture's metres
                    if (settings) src.Revert(s);                            // a Resolution, a text: as the sign holds them
                });
            if (reset)
                Window.Later(() =>
                {
                    Fit middle = s.Chosen.Fit;
                    middle.Right = middle.Up = middle.Out = 0;
                    s.Chosen.Fit = middle;
                });
        }

        /// <summary>The footer, in view whatever the scroll: Undo changes for the whole menu (0.20.59, the author: an Undo
        /// changes for the whole menu ... sticky like the footer button: the fit, the light, and what the source holds otherwise,
        /// a size, a resolution, another pick, a typed text) beside the verb button.</summary>
        public override void Footer()
        {
            Session s = Window.Session;
            Source src = content.Current;
            bool can = s.HasTarget && s.Reason == null;
            AreaMeter.Draw(s, Change.AreaBytes(s.Verb, src.Bytes(s)));      // multiplayer: the area limit, with the pick in place
            using (Widgets.Horizontal())
            {
                // the content's and the sign's at once (0.20.186, the author: it should undo as if we pressed Undo
                // in both the sign and the content; revert to the original, the sign's current settings): all back to what
                // the sign holds and how it stands now, the steps not applied dropped too
                GUI.enabled = can && (!s.Chosen.Fit.Same(s.Held.Fit) || s.Chosen.Light != Change.LightOf(s.Held) || src.Reverts(s) ||
                                      move.Pending);
                if (Widgets.Button("Undo changes", Theme.Button, Widgets.W(150), Widgets.H(36)))
                    Window.Later(() =>
                    {
                        move.DropPending(s);
                        s.Chosen.Fit = s.Held.Fit;
                        s.Chosen.Light = Change.LightOf(s.Held);             // the light row on 2 Content shows it again
                        src.Revert(s);
                        // the sign holds another kind than this source: that item back, in its own source (0.20.148, review:
                        // with nothing picked here the light alone made an Apply that dropped the item's recipe)
                        if (s.Editing && s.Held.Recipe.Kind != SignRecipe.Kinds.None && s.Held.Recipe.Kind != src.Kind &&
                            s.Missing == null && s.LoadInto != null)
                            s.LoadInto(s.Held.Recipe.Copy(), s.Chosen);
                    });
                GUI.enabled = true;
                using (Widgets.Vertical())
                    ContentTab.DrawVerbButton(Window, src, confirm);
            }
        }
    }
}
