using System;
using System.Collections.Generic;
using SignLink.Pictures;
using SignLink.Signs;
using UnityEngine;
using PetyusPolisz.Kit;

namespace SignLink.UI
{
    /// <summary>Move the sign (3 Place): the selected sign and its copies a little, as they are (SignMove, no remaking):
    /// steps, three rows of two directions in the frame of the sign as built (Draw, Sign positioning), a turn about the up
    /// axis as the hammer turns a piece (DrawTurn, Sign rotations), and Changes (DrawChanges, under both): how far it is
    /// from where and how it was built, Apply and Undo for the steps not applied yet (0.20.34), back there (a tilted or
    /// rotated sign made again standing, 0.20.66), and making the signs again where they are, so players without
    /// SignLink see the move at once. Arrow keys too (Keys, from the window's Update while 3 Place is open).</summary>
    internal sealed class MovePanel
    {
        // up to a metre a step, now that a sign goes as far as the game holds it (0.20.109)
        static readonly float[] MoveSteps = { 0.001f, 0.005f, 0.01f, 0.05f, 0.1f, 0.5f, 1f };
        static readonly string[] MoveStepNames = { "1 mm", "5 mm", "1 cm", "5 cm", "10 cm", "50 cm", "1 m" };
        /// <summary>A step's length and its way, each a whole phrase ({0}: the length) for the messages and the Changes row.</summary>
        static readonly string[] MoveWords = { "{0} left", "{0} right", "{0} down", "{0} up", "{0} towards you", "{0} away" };   // lang:keys
        static readonly KeyCode[] Arrows = { KeyCode.LeftArrow, KeyCode.RightArrow, KeyCode.UpArrow, KeyCode.DownArrow };

        readonly SignLinkWindow window;
        int moveStep = 3;                                                   // 5 cm (0.20.55, the author: 5 cm as the default)
        KeyCode held = KeyCode.None;
        float repeatAt;
        string movedText = Lang.T("where it was built");
        bool moved, posed;                                                  // moved or turned; tilted or rotated
        float turned;                                                      // the Turn row's value, on the Moved row's clock
        Sign movedOf;
        float movedAt = -1f;
        bool pendingNow;                                                    // steps not applied yet (Layout: the buttons' state)
        string unheldNow;                                                   // why the game would not hold it there (SignMove.Unheld)

        /// <summary>Where and how the sign stood before Reset to default, so Undo brings it back while the window stays
        /// open and the sign is the one the reset made (0.20.96, the author: for Reset to default in 3 Place, until I close
        /// the window the Undo button could work to revert the reset).</summary>
        sealed class BeforeReset
        {
            public Sign Root;                                               // the stack's first sign after the reset
            public bool Remade, Tipped, Edge;                               // the signs were made again; its pose
            public Vector3 Applied, Pending;                                // the move since it was built: written, and not yet
            public float AppliedTurn, PendingTurn, Tilt, Rotated;
            public Fit Chosen;                                              // 3 Place's rows as they were
        }

        BeforeReset beforeReset;

        public MovePanel(SignLinkWindow w) => window = w;

        /// <summary>The window closes: a reset stays as it is.</summary>
        public void Close() => beforeReset = null;

        /// <summary>Steps or turns wait for Apply sign coord changes (as the last Layout found).</summary>
        public bool Pending => pendingNow;

        /// <summary>The steps and turns not applied dropped, as Undo steps drops them (3 Place's Undo changes, 0.20.186):
        /// the sign as it stands for everyone.</summary>
        public void DropPending(Session s)
        {
            if (!pendingNow || !s.HasTarget) return;
            Sign root = SignFinder.Root(s.Target);
            SignMove.Revert();
            UpdateMoved(root);
        }

        /// <summary>Layout events only: the Moved row and the Turn row's total on a half-second clock, and whether steps
        /// wait for Apply. Both DrawTurn and Draw call it; it works once a frame.</summary>
        void Prepare(Session s, bool can)
        {
            if (Event.current.type != EventType.Layout || preparedFrame == Time.frameCount) return;
            preparedFrame = Time.frameCount;
            bool has = s.HasTarget && can;
            Sign target = has ? s.Target : null;
            if (!ReferenceEquals(movedOf, target) || Time.unscaledTime - movedAt > 0.5f)
            {
                movedOf = target;
                movedAt = Time.unscaledTime;
                UpdateMoved(s.Stack.Count > 0 ? s.Stack[0] : target);
            }
            Sign rootNow = !has ? null : s.First;
            pendingNow = has && SignMove.HasPending(rootNow);
            unheldNow = pendingNow ? SignMove.Unheld(rootNow) : null;
            // a tilt or a rotation waiting, read once a frame: the rows change the choice within an event, and a red line
            // that came or went between the Layout and the Repaint threw (1.0.2, review)
            posePending = has && !Change.SamePose(s.Held.Fit, s.Chosen.Fit);
            poseWhy = posePending ? ContentTab.PoseProblem(s) : null;
            poseText = posePending ? SignPose.Describe((float)s.Chosen.Fit.Tilt, (float)s.Chosen.Fit.Turn) : "";
            if (beforeReset != null && !ReferenceEquals(beforeReset.Root, rootNow)) beforeReset = null;   // another sign, or made again since
        }

        bool posePending;
        string poseWhy, poseText = "";

        int preparedFrame = -1;
        float chosenTurn = float.NaN;                                       // the Turn row's own end of the circle

        /// <summary>The Turn row (the sign about its up axis, as the hammer turns a piece), drawn by 3 Place › Sign
        /// rotations under the tilt: it shows the total since the sign was built and reports a change in its own event; the stack turns
        /// at the next Layout (SignMove.Turn), as a step does.</summary>
        public void DrawTurn(Session s, bool can)
        {
            Prepare(s, can);
            bool has = s.HasTarget && can;
            bool was = GUI.enabled;
            GUI.enabled = was && has;
            // -180 and 180 are one turn, read back as 180: the row keeps the end the player chose (dragged to -180 the
            // slider jumped to the right end and let go of the mouse; typed -180 came back as 180)
            float shown = !float.IsNaN(chosenTurn) && Mathf.Abs(Mathf.DeltaAngle(turned, chosenTurn)) < 0.01f ? chosenTurn : turned;
            float want = Widgets.Number("move.turn", Lang.T("Turn (clockwise)"), shown, -180f, 180f, 5f, "°", "0.#");
            GUI.enabled = was;
            if (has && Mathf.Abs(want - shown) > 0.001f)
            {
                chosenTurn = want;                                          // a typed turn comes when it is done (Widgets.Number)
                if (Mathf.Abs(Mathf.DeltaAngle(turned, want)) > 0.01f) Turn(s, s.Target, want);   // the total wanted, not a step
            }
        }

        public void Draw(Session s, bool can)
        {
            Prepare(s, can);
            bool has = s.HasTarget && can;
            Sign target = has ? s.Target : null;
            bool was = GUI.enabled;
            GUI.enabled = has;
            int dir = -1;

            using (Widgets.Vertical(Theme.Section))
            {
                int pick = Widgets.Choice(Lang.T("Move by"), moveStep, MoveStepNames, 440f);   // small (0.20.78)
                if (pick != moveStep) window.Later(() => moveStep = pick);
                Widgets.Space(4);
                // the steps laid out as the arrow keys lie (0.20.78, the author: place them a bit smarter and
                // nicer, even just swap them for arrows): ▲ over ◄ ▼ ►, and beside them Shift's pair, away over towards
                // you; the gaps keep a button's margins, so the two rows line up
                const float k = 46f, kh = 32f, side = 132f;
                using (Widgets.Horizontal())
                {
                    GUILayout.FlexibleSpace();
                    GUILayout.Label("", Theme.ButtonGap, Widgets.W(k), Widgets.H(kh));
                    if (Widgets.Button("▲", KeyStyle(3), Widgets.W(k), Widgets.H(kh))) dir = 3;
                    GUILayout.Label("", Theme.ButtonGap, Widgets.W(k), Widgets.H(kh));
                    Widgets.Space(20);
                    if (Widgets.Button(Lang.T("Away"), KeyStyle(5), Widgets.W(side), Widgets.H(kh))) dir = 5;
                    GUILayout.FlexibleSpace();
                }
                using (Widgets.Horizontal())
                {
                    GUILayout.FlexibleSpace();
                    if (Widgets.Button("◄", KeyStyle(0), Widgets.W(k), Widgets.H(kh))) dir = 0;
                    if (Widgets.Button("▼", KeyStyle(2), Widgets.W(k), Widgets.H(kh))) dir = 2;
                    if (Widgets.Button("►", KeyStyle(1), Widgets.W(k), Widgets.H(kh))) dir = 1;
                    Widgets.Space(20);
                    if (Widgets.Button(Lang.T("Towards you"), KeyStyle(4), Widgets.W(side), Widgets.H(kh))) dir = 4;
                    GUILayout.FlexibleSpace();
                }
                // the keyboard moves it too (0.20.56): its keys at the part's title (PlaceTab, 1.0.2)
            }
            GUI.enabled = was;
            if (dir >= 0)
            {
                Widgets.DropFocus();                                        // the arrow keys are the sign's again
                Move(s, target, dir);
            }
        }

        /// <summary>Changes (0.20.56, the author: what is "moved the move"?? ... name the field Changes ... in one
        /// group): what the moves, turns and the pose add up to since the sign was built, and the four things
        /// to do with them. Under both Sign positioning and Sign rotations (0.20.66), whichever is open. Apply makes the
        /// signs again where they stand, so every player sees the change at once (0.20.72, the author: the Rebuild for everyone
        /// button should go ... illogical and legacy); Reset to default puts the sign back where and as it was
        /// built, at once, for everyone (the author: "back to where it was built" should simply be Reset to default).</summary>
        public void DrawChanges(Session s, bool can)
        {
            Prepare(s, can);
            bool has = s.HasTarget && can;
            Sign target = has ? s.Target : null;
            bool was = GUI.enabled;
            bool reset, apply, undo;
            // a tilt or a rotation waiting is the sign's too (1.0.2, the author: the group's buttons should govern the whole
            // Sign block, not only the fine-tune: I could not apply a tilt here)
            // (posePending, poseWhy, poseText: Prepare, once a frame)
            bool waiting = pendingNow || (has && posePending);
            string stepsText = moved || posed || pendingNow ? movedText : "";   // movedText holds the pose it stands in
            Widgets.Space(4);                                               // no rule: Changes belongs to the groups above (0.20.146)
            using (Widgets.Vertical(Theme.Section))
            {
                using (Widgets.Horizontal())
                {
                    GUILayout.Label(Lang.T("Changes"), Theme.Label, Widgets.W(Widgets.LabelW));
                    GUILayout.Label(!has ? "" : !moved && !posed && !waiting ? Lang.T("none") : ChangesLine(stepsText, waiting),
                                    waiting ? Theme.Notice : Theme.Label, Widgets.W(Widgets.RestW));
                }
                float half = (Widgets.ContentWidth - 30) / 2f - 4f;
                using (Widgets.Horizontal())
                {
                    // steps, turns and a pose wait for one of these; refused where the game would not hold the sign up
                    GUI.enabled = has && waiting && unheldNow == null && poseWhy == null;
                    float wide = (Widgets.ContentWidth - 30) * 0.62f, rest = Widgets.ContentWidth - 30 - wide - 14f;
                    apply = Widgets.NextButton(Lang.T("Apply sign coord changes"), 26f, wide, less: true);   // 0.20.77, the author: the apply button to read "Apply sign coord changes"
                    GUI.enabled = has && (waiting || beforeReset != null);   // steps and a pose not applied, else the reset
                    Widgets.Space(6);                                       // air between the two (1.1.0, the author)
                    undo = Widgets.Button(Lang.T("Undo steps"), Theme.ButtonLess, Widgets.W(rest), Widgets.H(26));
                }
                Widgets.Space(4);
                using (Widgets.Horizontal())
                {
                    GUI.enabled = has && (moved || posed || pendingNow);    // moved, turned, tilted or rotated
                    reset = Widgets.Button(Lang.T("Reset to default"), Theme.ButtonLess, Widgets.W(180), Widgets.H(26));   // a size under Content positioning's (190 wide)
                    GUI.enabled = was;
                    GUILayout.Label(Lang.T("as built"), Theme.SideNote, Widgets.H(26));
                }
                // the steps go there, only the apply is refused (1.0.2, the author: allow it, just do not let it apply, with
                // the red text that it has no support)
                if (has && unheldNow != null)
                    GUILayout.Label(Lang.F("No support: {0}", unheldNow), Theme.Warn);
                else if (has && poseWhy != null) GUILayout.Label(poseWhy, Theme.Warn);
            }
            GUI.enabled = was;
            if (reset || apply || undo) window.KeepOpen();                  // not a put: its message keeps the window
            if (reset) window.Act(() => Reset(s, target));
            // what the sign holds in the pose the rows say, its place along the face kept as the rows keep it (Laid: Posed
            // within a quarter turn, the world place when the face turns round; 1.0.2, review: WithPose moved off-centre
            // content along the face)
            Fit poseTo = s.Held.Fit.Laid(s.Chosen.Fit.Tilt, s.Chosen.Fit.Edge);
            poseTo.Turn = s.Chosen.Fit.Turn;
            SignRecipe heldRecipe = s.Held.Recipe;
            if (apply && has && posePending)
                window.Act(() =>
                {
                    beforeReset = null;
                    Fit chosen = s.Chosen.Fit;
                    bool contentWaits = !chosen.Same(poseTo);               // a size or place changed on Content, not applied
                    // the stack made again in the new pose, the steps not applied with it (SignPose.Set); the content as it
                    // is, so content changes waiting on 3 Place stay waiting
                    List<Sign> made = SignFit.Refit(target, poseTo, GlowMode.AsMade, out Fit got);
                    if (made.Count > 0 && heldRecipe.Kind != SignRecipe.Kinds.None)
                        SignMarks.SetSource(made[0], heldRecipe.ToString());   // a known item keeps its recipe (as Perform)
                    s.Chosen.Fit = contentWaits ? chosen.WithPose(got) : got;
                    if (made.Count > 0)
                    {
                        window.Select(made[0], sameContent: true);
                        UpdateMoved(made[0]);
                    }
                    return Lang.F("applied: every player sees it {0} now.", SignPose.Describe((float)got.Tilt, (float)got.Turn));
                });
            else if (apply)
                window.Act(() =>
                {
                    beforeReset = null;                                     // a new place: the reset is behind it
                    Sign root = SignFinder.Root(target);
                    SignMove.Write();                                       // SignLink players see it at once
                    // and the signs made again where they stand, so players without SignLink see it at once too: the game
                    // places a piece only when it loads (Rebuild for everyone did this apart, before 0.20.72)
                    List<Sign> made;
                    try { made = SignPose.Remake(root); }
                    catch (InvalidOperationException e)
                    {
                        UpdateMoved(root);
                        return Lang.F("applied for SignLink players; players without SignLink see it when they come back (the signs could not be made again: {0}).", e.Message);
                    }
                    if (made.Count > 0)
                    {
                        window.Select(made[0], sameContent: true);
                        UpdateMoved(made[0]);
                    }
                    return Lang.T("applied: every player sees the sign there now.");
                });
            BeforeReset back = undo && !waiting ? beforeReset : null;       // steps not applied go first, the reset after them
            if (back != null)
            {
                beforeReset = null;
                window.Act(() => UndoReset(s, back, back.Root, 0));
            }
            else if (undo)
                window.Act(() =>
                {
                    Sign root = SignFinder.Root(target);
                    SignMove.Revert();
                    UpdateMoved(root);
                    Fit back = s.Chosen.Fit.Laid(s.Held.Fit.Tilt, s.Held.Fit.Edge);   // the tilt and the rotation back too, the
                    back.Turn = s.Held.Fit.Turn;                            // place along the face as the rows had it (review)
                    s.Chosen.Fit = back.Same(s.Held.Fit) ? s.Held.Fit : back;
                    return Lang.T("undone: the sign is back as it was last applied.");
                });
        }

        /// <summary>Undo after Reset to default: the sign back where and as it stood, for everyone, the reverse of the reset,
        /// one part a frame (the signs made in one frame stand beside the old ones until the frame ends): 0 its pose (made
        /// again tilted or rotated where it was built: the moves were made in that pose, so they go along its face again),
        /// 1 its moves and turns (made again there), 2 the steps that were not applied (only this player sees them again).
        /// The rows take what they held before the reset.</summary>
        string UndoReset(Session s, BeforeReset b, Sign root, int part)
        {
            if (!root || SignFinder.Zdo(root) == null) return Lang.T("the reset stays: the sign is gone (unloaded or removed).");
            s.Chosen.Fit = b.Chosen;
            if (part == 0 && b.Remade && b.Tipped)
            {
                string recipe = SignMarks.Source(root);
                Fit on = SignFit.Read(root, out _, out _, out _);
                List<Sign> posed = SignFit.Refit(root, on.Posed(b.Rotated, b.Tilt, b.Edge), GlowMode.AsMade, out _);
                if (posed.Count == 0) return Lang.T("the sign is gone.");
                if (recipe.Length > 0) SignMarks.SetSource(posed[0], recipe);
                window.Select(posed[0], sameContent: true);
                root = posed[0];
                Sign next = root;
                window.Act(() => UndoReset(s, b, next, 1));
                return Lang.T("undoing the reset ...");
            }
            if (part <= 1 && b.Remade && (b.Applied.sqrMagnitude > 1e-8f || Mathf.Abs(b.AppliedTurn) > 0.05f))
            {
                List<Sign> stack = SignFinder.Stack(root);
                if (b.Applied.sqrMagnitude > 1e-8f) SignMove.Nudge(stack, b.Applied, root);
                if (Mathf.Abs(b.AppliedTurn) > 0.05f) SignMove.Turn(stack, b.AppliedTurn, root);
                List<Sign> made = SignPose.Remake(root);                    // where it stood, for every player at once
                if (made.Count == 0) return Lang.T("the sign is gone.");
                window.Select(made[0], sameContent: true);
                if (b.Pending.sqrMagnitude > 1e-8f || Mathf.Abs(b.PendingTurn) > 1e-4f)
                {
                    Sign next = made[0];
                    window.Act(() => UndoReset(s, b, next, 2));
                    return Lang.T("undoing the reset ...");
                }
                UpdateMoved(made[0]);
                return Lang.T("the reset is undone: the sign stands where and as it stood before, for everyone.");
            }
            bool steps = b.Pending.sqrMagnitude > 1e-8f || Mathf.Abs(b.PendingTurn) > 1e-4f;
            if (steps)
            {
                List<Sign> stack = SignFinder.Stack(root);
                if (b.Pending.sqrMagnitude > 1e-8f) SignMove.Nudge(stack, b.Pending, root);
                if (Mathf.Abs(b.PendingTurn) > 1e-4f) SignMove.Turn(stack, b.PendingTurn, root);
            }
            UpdateMoved(root);
            return b.Remade ? (steps ? Lang.T("the reset is undone: the sign stands where and as it stood before; the steps not applied are back (only you see them).")
                                     : Lang.T("the reset is undone: the sign stands where and as it stood before."))
                            : Lang.T("the reset is undone: the steps not applied are back (only you see them).");
        }

        /// <summary>Reset to default: the sign back where and as it was built, at once, for everyone. Steps never applied
        /// are simply dropped; else the moves and turns go back and the signs are made again there, standing as built when
        /// it was tilted or rotated (0.20.66: the content stays where it sits on the face and keeps its record).</summary>
        string Reset(Session s, Sign target)
        {
            Sign root = SignFinder.Root(target);
            List<Sign> stack = SignFinder.Stack(root);
            bool applied = (SignMove.Moved(root) - SignMove.Pending(root)).sqrMagnitude > 1e-8f ||
                           Mathf.Abs(Mathf.DeltaAngle(SignMove.PendingTurn(root), SignMove.Turned(root))) > 0.05f;
            SignPose.Read(root, out float tilt, out float rotated, out bool edge);
            bool tipped = Mathf.Abs(tilt) >= 0.05f || Mathf.Abs(rotated) >= 0.05f;
            var before = new BeforeReset                                    // for Undo, while the window stays open
            {
                Root = root, Tipped = tipped, Edge = edge, Tilt = tilt, Rotated = rotated, Chosen = s.Chosen.Fit,
                Applied = SignMove.Moved(root) - SignMove.Pending(root), Pending = SignMove.Pending(root),
                AppliedTurn = Mathf.DeltaAngle(SignMove.PendingTurn(root), SignMove.Turned(root)), PendingTurn = SignMove.PendingTurn(root),
            };
            if (!applied && !tipped)
            {
                SignMove.Revert();                                          // only steps not applied: dropping them is the way back
                UpdateMoved(root);
                beforeReset = before;
                return Lang.T("the sign is back where it was built (Undo brings the steps back).");
            }
            // the way back as steps first; refused (another sign there, a place nothing is built, nothing would hold it
            // there now), the sign goes back to where it stood a moment ago, its steps not applied with it (1.0.2, review:
            // the reset stopped halfway, as steps; then it dropped the player's steps)
            Vector3 moved0 = SignMove.Moved(root);
            float turned0 = SignMove.Turned(root);
            bool nudged = false, turnedBack = false;
            void Undo()
            {
                try
                {
                    if (turnedBack) SignMove.Turn(stack, turned0, root);
                    if (nudged) SignMove.Nudge(root, moved0);
                }
                catch (InvalidOperationException) { SignMove.Revert(); }   // not even that: where it stands for everyone
                UpdateMoved(root);
            }
            try
            {
                SignMove.Nudge(root, -moved0);
                nudged = true;
                SignMove.Turn(stack, -turned0, root);
                turnedBack = true;
            }
            catch (InvalidOperationException)
            {
                Undo();
                throw;
            }
            string unheld = SignMove.Unheld(root);
            if (unheld != null)
            {
                Undo();
                throw new InvalidOperationException(Lang.F("no support where it was built: {0}", unheld));
            }
            List<Sign> made;
            try
            {
                if (tipped)
                {
                    // the pose is the signs' own, so they are made again standing as built, the moves undone with them
                    string recipe = SignMarks.Source(root);
                    Fit on = SignFit.Read(root, out _, out _, out _);
                    made = SignFit.Refit(root, on.Posed(0, 0, false), GlowMode.AsMade, out _);
                    if (made.Count > 0 && recipe.Length > 0) SignMarks.SetSource(made[0], recipe);
                    Fit chosen = s.Chosen.Fit;                              // the rows stand too; what else they hold stays
                    s.Chosen.Fit = chosen.Posed(0, 0, chosen.Edge);
                }
                else made = SignPose.Remake(root);                          // where it was built, for every player at once
            }
            catch (Exception e) when (e is InvalidOperationException || e is ArgumentException)
            {
                Undo();                                                     // refused at the last step too (review: a sign over a
                throw;                                                      // server's limit, a ward, copies loading): the steps come back
            }
            if (made.Count == 0) return Lang.T("the sign is gone.");
            window.Select(made[0], sameContent: true);
            UpdateMoved(made[0]);
            before.Root = made[0];
            before.Remade = true;
            beforeReset = before;
            return Lang.T("the sign stands where and as it was built: its signs were made again, every player sees it so now (Undo brings it back).");
        }

        /// <summary>A turn (the Turn row) to a total since it was built: the stack the session knows, about its first sign,
        /// refreshing only the Moved row. The total, not a step: two changes in one frame (typing fast) turn it to the
        /// last one instead of adding both.</summary>
        void Turn(Session s, Sign target, float to)
        {
            window.KeepOpen();                                              // not a put: its message keeps the window
            List<Sign> stack = s.Stack.Count > 0 && ReferenceEquals(s.Target, target) ? s.Stack : null;
            window.Later(() =>
            {
                try
                {
                    if (stack != null && (!stack[0] || SignFinder.Zdo(stack[0]) == null)) stack = null;   // made again meanwhile:
                    Sign at = target && SignFinder.Zdo(target) != null ? target : window.Session.Target;   // the new one (0.20.130)
                    if (!at) return;
                    Sign root = stack != null ? stack[0] : SignFinder.Root(at);
                    float degrees = Mathf.DeltaAngle(SignMove.Turned(root), to);
                    if (Mathf.Abs(degrees) < 0.01f) return;
                    SignMove.Turn(stack ?? SignFinder.Stack(root), degrees, root);
                    UpdateMoved(root);
                    window.Say(Lang.F(degrees > 0 ? "turned the sign {0:0.#}° clockwise ({1})." : "turned the sign {0:0.#}° anticlockwise ({1}).",
                                      Mathf.Abs(degrees), movedText) + NoSupport(root));
                }
                catch (Exception e)
                {
                    window.Say("error: " + e.Message);
                }
            });
        }

        /// <summary>One step: the stack the session knows (no second search for it), refreshing only the Moved row (the
        /// bar and the table have their half-second clock).</summary>
        void Move(Session s, Sign target, int dir)
        {
            window.KeepOpen();                                              // a step (button or arrow key) is not a put
            float step = MoveSteps[moveStep];
            List<Sign> stack = s.Stack.Count > 0 && ReferenceEquals(s.Target, target) ? s.Stack : null;
            window.Later(() =>
            {
                try
                {
                    if (stack != null && (!stack[0] || SignFinder.Zdo(stack[0]) == null)) stack = null;   // made again meanwhile:
                    Sign at = target && SignFinder.Zdo(target) != null ? target : window.Session.Target;   // the new one (0.20.130)
                    if (!at) return;
                    Sign root = stack != null ? stack[0] : SignFinder.Root(at);
                    SignPose.Frame(root, out Vector3 right, out Vector3 towards);
                    Vector3 d = dir == 0 ? -right : dir == 1 ? right : dir == 2 ? Vector3.down : dir == 3 ? Vector3.up : dir == 4 ? towards : -towards;
                    if (stack != null) SignMove.Nudge(stack, d * step);
                    else SignMove.Nudge(at, d * step);
                    UpdateMoved(root);
                    string way = Lang.F(MoveWords[dir], Length(step));
                    window.Say((moved ? Lang.F("moved the sign {0} ({1} from where it was built).", way, movedText)
                                      : Lang.F("moved the sign {0} (back where it was built).", way)) + NoSupport(root));
                }
                catch (Exception e)
                {
                    window.Say("error: " + e.Message);
                }
            });
        }

        /// <summary>Arrow keys (from the window's Update) while 3 Place is open and no text field has the keyboard; held,
        /// they repeat.</summary>
        public void Keys()
        {
            if (!window.HasTarget || GUIUtility.keyboardControl != 0 || Console.IsVisible() ||
                (Chat.instance && Chat.instance.HasFocus()))
            {
                held = KeyCode.None;
                return;
            }
            bool shift = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
            foreach (KeyCode k in Arrows)
                if (Input.GetKeyDown(k))
                {
                    held = k;
                    repeatAt = Time.unscaledTime + 0.4f;
                    Key(k, shift);
                    return;
                }
            if (held == KeyCode.None) return;
            if (!Input.GetKey(held)) held = KeyCode.None;
            else if (Time.unscaledTime >= repeatAt)
            {
                repeatAt = Time.unscaledTime + 0.08f;
                Key(held, shift);
            }
        }

        void Key(KeyCode k, bool shift)
        {
            int dir = k == KeyCode.LeftArrow ? 0 : k == KeyCode.RightArrow ? 1 : k == KeyCode.DownArrow ? (shift ? 4 : 2) : (shift ? 5 : 3);
            keyDir = dir;                                                   // its button shows pressed (KeyStyle)
            keyAt = Time.unscaledTime;
            Move(window.Session, window.Target, dir);
        }

        int keyDir = -1;
        float keyAt = -100f;

        /// <summary>A step's button pressed while its arrow key moves the sign (held, or a moment after a tap), as the
        /// mouse would show it (0.20.77, the author: when I push it with the hotkey ... show it on the buttons as if I had
        /// pressed them with the mouse).</summary>
        GUIStyle KeyStyle(int dir) =>
            keyDir == dir && (held != KeyCode.None || Time.unscaledTime - keyAt < 0.15f) ? Theme.ButtonDown : Theme.Button;

        /// <summary>What a step adds to its message where the game would not hold the sign up: it went, but it cannot be applied.</summary>
        static string NoSupport(Sign root) =>
            SignMove.Unheld(root) != null ? " " + Lang.T("No support.") : "";

        /// <summary>The Changes row: the moves and the pose to come (a tilt or a rotation waiting), and whether only this player
        /// sees them yet.</summary>
        string ChangesLine(string stepsText, bool waiting)
        {
            string line = stepsText + (posePending ? (stepsText.Length > 0 ? " → " : "") + poseText : "");
            return waiting ? Lang.F("{0} · only you see it", line) : line;
        }

        void UpdateMoved(Sign root)
        {
            Vector3 m = root ? SignMove.Moved(root) : Vector3.zero;
            turned = root ? SignMove.Turned(root) : 0f;
            moved = m.sqrMagnitude > 1e-8f || Mathf.Abs(turned) > 0.05f;
            float tilt = 0f, rotated = 0f;
            if (root) SignPose.Read(root, out tilt, out rotated);
            posed = Mathf.Abs(tilt) >= 0.05f || Mathf.Abs(rotated) >= 0.05f;   // the pose the signs were made in (Sign rotations)
            if (!moved && !posed)
            {
                movedText = Lang.T("where it was built");
                return;
            }
            var parts = new List<string>();
            if (m.sqrMagnitude > 1e-8f)
            {
                SignPose.Frame(root, out Vector3 right, out Vector3 towards);
                Part(parts, Vector3.Dot(m, right), MoveWords[1], MoveWords[0]);
                Part(parts, m.y, MoveWords[3], MoveWords[2]);
                Part(parts, Vector3.Dot(m, towards), MoveWords[4], MoveWords[5]);
            }
            if (Mathf.Abs(turned) > 0.05f) parts.Add(Lang.F(turned > 0 ? "turned {0:0.#}° clockwise" : "turned {0:0.#}° anticlockwise", Mathf.Abs(turned)));
            string tipped = Fit.TiltWords(tilt);
            if (tipped.Length > 0) parts.Add(tipped);
            if (Mathf.Abs(rotated) >= 0.05f) parts.Add(Lang.F(rotated > 0 ? "rotated {0:0.#}° clockwise" : "rotated {0:0.#}° anticlockwise", Mathf.Abs(rotated)));
            movedText = parts.Count > 0 ? string.Join(Lang.Comma, parts.ToArray()) : Lang.T("where it was built");
        }

        /// <summary>plus, minus: the way's phrase (MoveWords, {0}: the length).</summary>
        static void Part(List<string> parts, float v, string plus, string minus)
        {
            if (Mathf.Abs(v) >= 0.0005f) parts.Add(Lang.F(v > 0 ? plus : minus, Length(Mathf.Abs(v))));
        }

        static string Length(float m) => m < 0.0095f ? $"{m * 1000:0} mm" : $"{m * 100:0.#} cm";
    }
}
