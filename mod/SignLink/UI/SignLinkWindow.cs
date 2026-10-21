using System;
using System.Collections.Generic;
using BepInEx.Configuration;
using SignLink.Pictures;
using SignLink.Signs;
using UnityEngine;
using PetyusPolisz.Kit;

namespace SignLink.UI
{
    /// <summary>The SignLink window (PageUp): a shell around the tabs in UI/Tabs (1 Sign, 2 Content, 3 Place, Playing,
    /// Settings). Above the tabs' bodies sits the target sign bar (the sign you looked at when opening it, or picked
    /// later); below them each tab's footer (its main button, always in view) and the one status line. IMGUI with its own
    /// styles (Theme, at the player's Opacity): no assets, no dependencies; sizes are logical units drawn through Widgets.U at the text scale.
    /// While it is open the game takes no input (InputBlockPatch, ScrollBlockPatch, InventoryBlockPatch); Select sign
    /// hides it until a click on a sign.</summary>
    internal sealed partial class SignLinkWindow : MonoBehaviour, IDeferred
    {
        public static bool IsOpen { get; private set; }

        /// <summary>Select sign: the window is hidden and the next click selects the sign under the cursor (the game's
        /// input stays blocked, so the cursor is free).</summary>
        public static bool Picking { get; private set; }

        /// <summary>The window (one, on the plugin's object), for the sign patches: E on a picture opens it.</summary>
        public static SignLinkWindow Instance { get; private set; }

        /// <summary>The mouse is over the window as it was drawn last (its rows may widen it): the wheel is the window's
        /// there (ScrollBlockPatch). Not while picking (the window is hidden then).</summary>
        public static bool PointerOver
        {
            get
            {
                SignLinkWindow w = Instance;
                if (!IsOpen || !w || Picking) return false;
                Vector3 m = Input.mousePosition;
                var at = new Vector2(m.x, Screen.height - m.y);              // GUI pixels from the top left
                return w.kit != null && w.kit.Covers(at);
            }
        }

        const int WindowId = 0x51C9;
        /// <summary>The frame: size, place, edges, scroll, the parts' heights, clicks' work later (the kit, 1.0.2).</summary>
        KitWindow kit;
        Tab[] tabs;
        ContentTab content;
        int tab;
        string status = "";
        /// <summary>The window's shared state: the selected sign, its stack, what it holds, what the tabs chose.</summary>
        public readonly Session Session = new Session();

        /// <summary>The frame in the world where the chosen content goes (Settings › Sign options › Highlight signs).</summary>
        readonly Ghost ghost = new Ghost();
        /// <summary>The frame around the sign under the cursor (Select sign, a row of Signs nearby): the content green
        /// and red as the selection's, the board cool white instead of neon lime.</summary>
        readonly Ghost hover = new Ghost(Ghost.Fill, Ghost.Line, Ghost.HoverBoard);
        bool hovering;
        /// <summary>The selected sign shows nothing on this computer while the live preview shows, or a new picture or
        /// piece is picked over it.</summary>
        readonly HeldVeil veil = new HeldVeil();
        readonly BoardDouble boardDouble = new BoardDouble();             // the board in a pending tilt or rotation (1.0.2)
        // the row of Signs nearby the mouse is over (set while the row repaints; stale after a couple of frames)
        Sign listHover;
        int listHoverAt = -10;

        bool ListHovering => IsOpen && showNearby && listHover && Time.frameCount - listHoverAt <= 2;

        /// <summary>The sign the tabs work on: for a stack, its rearmost sign (SignFinder.Root).</summary>
        public Sign Target => Session.Target;

        /// <summary>An action is queued and not yet run: the footer's button says "Working ..." for the pass it was
        /// queued in (the action runs at the next frame's Layout, so the frame with the label draws first).</summary>
        public bool Busy { get; private set; }

        /// <summary>Why the window could not load (its tabs threw, a patch did not apply), or null. The chat command and
        /// the main menu say it, instead of a window that silently never opens (0.13.0).</summary>
        public string Broken { get; private set; }

        /// <summary>The game is closing (Application.quitting): the window is not shut as a player shuts it then.</summary>
        static bool quitting;
        static void Quitting() => quitting = true;

        void Awake()
        {
            Instance = this;
            kit = new KitWindow(WindowId, new SignLinkLook(), m => Plugin.Log.LogInfo(m), m => Plugin.Log.LogWarning(m));                                                // useGUILayout stays on: shut, OnGUI draws only the badge and the editor's button
            Application.quitting += Quitting;
            Session.Changed += () =>
            {
                barAt = -1f;                                                // the bar reads the sign again at once
                LookAgain();                                                // the hover frame looks again (Select sign, the list)
                Plugin.Restore.Soon();                                      // a moving GIF's screen comes back at once after a change
            };
            try
            {
                content = new ContentTab(this);
                tabs = new Tab[] { new SignTab(this, content), content, new PlaceTab(this, content), new PlayingTab(this), new SettingsTab(this) };
                if (!Plugin.Patched(typeof(Patches.InputBlockPatch)))
                    Broken = Lang.T("the input blocker did not apply (a game update?)");   // a window the game plays through is worse than none
            }
            catch (Exception e)
            {
                Broken = $"{e.GetType().Name}: {e.Message}";
                Plugin.Log.LogError($"The SignLink window could not load: {e}");
            }
        }

        float openAt = -1f;

        /// <summary>Opens the window once the game lets it (the chat or console that asked is still in front): the
        /// /signlink command.</summary>
        public void OpenSoon() => openAt = Time.unscaledTime;

        Sign openForSign;

        /// <summary>Opens on this sign as soon as the game's own UI allows: the game's sign editor handed it over (its
        /// panel counts as visible until the next frame).</summary>
        public void OpenSoonFor(Sign s)
        {
            openForSign = s;
            openAt = Time.unscaledTime;
        }

        public void Later(Action a) => kit.Later(a);

        /// <summary>Runs a click's work at the next frame's Layout event and shows its result in the status line; the
        /// session re-reads its stack afterwards.</summary>
        public void Act(Func<string> f)
        {
            Busy = true;
            Later(() =>
            {
                string said;
                try { said = f(); }
                catch (Exception e) { said = "error: " + e.Message; }
                Say(said);
                Session.Touch();
            });
        }

        /// <summary>Runs what was queued in earlier frames (Layout events); all of it on Close (a click in the same frame
        /// as Esc runs now, not minutes later on another sign).</summary>
        void RunLater(bool all)
        {
            kit.RunLater(all, e => Say("error: " + e.Message));
            if (!kit.Waiting) Busy = false;
        }

        /// <summary>The last action's result shows for this long, then the line empties.</summary>
        const float StatusSeconds = 10f;
        float statusAt = -100f;

        /// <summary>The status line (Done: / Could not: by its first word). The result of a put the verb button asked for
        /// closes the window when it worked (CloseAfterPut).</summary>
        public void Say(string message)
        {
            status = message ?? "";
            statusAt = Time.unscaledTime;
            if (!closeAfterPut) return;
            closeAfterPut = false;
            if (status.StartsWith("error", StringComparison.Ordinal) || Time.unscaledTime - closeArmedAt > CloseArmedSeconds) return;
            string said = status;
            int cut = said.IndexOf(" (", StringComparison.Ordinal);
            if (cut > 0) said = said.Substring(0, cut);                     // the first part: what went where
            said = said.TrimEnd('.', ' ');
            if (said.Length > 0) said = char.ToUpperInvariant(said[0]) + said.Substring(1);
            try { MessageHud.instance?.ShowMessage(MessageHud.MessageType.TopLeft, Lang.F("SignLink: {0}", said)); }
            catch (Exception) { }                                           // the corner message is a courtesy
            closeNow = true;                                                // at the next OnGUI, outside whatever said it
        }

        /// <summary>The verb button was pressed (Place, Put, Update, Refit, Apply: 2 Content's choice goes on): when its
        /// result is in and it worked, the window closes and the result shows in the game's message corner (0.20.61, the
        /// author: once a placing happens ... the mod window should close by itself afterwards); an error keeps it open.
        /// Not the live refit of a small stack (no button), nor Move the sign's Apply. Another tab, closing, another
        /// action of the player's (KeepOpen) or a minute without a result lets it go.</summary>
        public void CloseAfterPut()
        {
            closeAfterPut = true;
            closeArmedAt = Time.unscaledTime;
        }

        /// <summary>The player did something else while a put was under way (a move, a turn, Changes, picking another
        /// sign, a live refit): its message must not close the window, so the put's close-after lets go (0.20.70,
        /// review: a step during a GIF's encoding closed the window, which undid the step and stopped the encoding).</summary>
        public void KeepOpen() => closeAfterPut = false;

        const float CloseArmedSeconds = 60f;
        bool closeAfterPut, closeNow;
        float closeArmedAt = -100f;

        int updateFailures;
        float retryAt;                                                      // after a failed Update: not again before this
        float lastUpdateWarning = -100f;
        float pickingAt;

        /// <summary>Guarded: a game update that breaks one of the UI checks (CanOpen) must not throw once a frame for
        /// 30 s. After a failure it waits a second; ten in a row (some ten seconds of nothing working) mark the window
        /// Broken, which the command and the main menu say (1.1.0, review: three failed frames, a burst while a zone
        /// loaded, ended the window for the session). Broken or waiting, an open window still closes, so the game's input
        /// is never left blocked.</summary>
        void Update()
        {
            if (Broken != null || Time.unscaledTime < retryAt)
            {
                if (IsOpen && (Broken != null || !Player.m_localPlayer))
                {
                    try { Close(); }
                    catch (Exception e) { Plugin.Log.LogDebug($"window: close: {e.Message}"); }
                }
                return;
            }
            try
            {
                if (IsOpen && !Player.m_localPlayer) Close();              // died, logged out, lost the connection
                showAll.Step(Time.unscaledTime);                            // Show all SignLink signs: open or shut
                // every sign's board in faint grey while a click could select one (0.20.172, the author); a board outline, so only
                // with Highlight signs on (1.1.0, the author: each switch its own part)
                boards.Step(Time.unscaledTime, IsOpen && (Picking || !HasTarget) && !ShowAll.On && PluginConfig.Highlight.Value);
                // a click on a sign selects it, without Pick in view (0.20.172, the author: whenever I click a sign, select
                // it, without Pick in view); a click on the window, or off any sign, does nothing here
                if (IsOpen && !Picking && Input.GetMouseButtonDown(0) && !PointerOver && GUIUtility.hotControl == 0)   // Show all too (1.1.0)
                    ClickAt(Input.mousePosition);
                if (IsOpen && Picking)
                {
                    if (Input.GetMouseButtonDown(0) && Time.unscaledTime - pickingAt > 0.2f) PickAt(Input.mousePosition);
                    else HoverAt(Input.mousePosition);                    // the right button turns the view (RightLook, 1.1.0)
                }
                else if (IsOpen && tabs[tab] is PlaceTab placeTab) placeTab.Keys();   // arrow keys move the sign
                else if (IsOpen && tabs[tab] is ContentTab contentTab) contentTab.Keys(Session);   // up and down step the list
                else if (IsOpen && showNearby && tabs[tab] is SignTab) NearbyKeys();         // ... and Signs nearby (1.1.0)
                // nothing selected: the sign under the mouse wears the hover frame, as while picking (0.20.173, the author:
                // the sign under my mouse has no hover state now ... it could work as with Pick in view when nothing is
                // selected); a click selects it (ClickAt)
                bool hoverFree = IsOpen && !Picking && !HasTarget && !ShowAll.On && !PointerOver;
                if (hoverFree) HoverAt(Input.mousePosition);
                if (hovering && !(IsOpen && Picking) && !hoverFree && !ListHovering)
                {
                    hover.Hide();
                    hovering = false;
                    LookAgain();
                }
                // not while a new key is being chosen, nor while a text field takes the keys (a letter bound as the key)
                if (!SettingsTab.CapturingKey && !(IsOpen && GUIUtility.keyboardControl != 0) && Pressed(PluginConfig.MenuKey.Value))
                {
                    if (IsOpen && Picking) Picking = false;                  // ends Select sign, as Esc does (1.1.0, the author)
                    else if (IsOpen) Close();
                    else if (CanOpen()) Open();
                }
                if (openAt >= 0f)
                {
                    if (IsOpen || Time.unscaledTime - openAt > 30f)
                    {
                        openAt = -1f;
                        openForSign = null;
                    }
                    else if (CanOpen())
                    {
                        openAt = -1f;
                        Sign forSign = openForSign;
                        openForSign = null;
                        if (forSign) OpenFor(forSign);
                        else Open();
                    }
                }
                updateFailures = 0;
            }
            catch (Exception e)
            {
                openAt = -1f;
                if (++updateFailures >= 10) Broken = $"{e.GetType().Name}: {e.Message}";
                else retryAt = Time.unscaledTime + 1f;
                if (Time.unscaledTime - lastUpdateWarning > 10f)
                {
                    lastUpdateWarning = Time.unscaledTime;
                    Plugin.Log.LogWarning($"window: {e}");
                }
            }
        }

        void LateUpdate()
        {
            if (!IsOpen || !Input.GetKeyDown(KeyCode.Escape)) return;      // the pause menu stays shut (InputBlockPatch)
            if (Picking) Picking = false;
            else if (SettingsTab.CapturingKey) SettingsTab.StopCapture();   // Esc keeps the old key, the window stays
            else Close();
        }

        /// <summary>The main key with its modifiers; other held keys (W while walking) do not matter, unlike
        /// KeyboardShortcut.IsDown.</summary>
        static bool Pressed(KeyboardShortcut key)
        {
            if (key.MainKey == KeyCode.None || !Input.GetKeyDown(key.MainKey)) return false;
            foreach (KeyCode m in key.Modifiers)
                if (!Input.GetKey(m)) return false;
            return true;
        }

        static bool CanOpen() =>
            Player.m_localPlayer && !Menu.IsVisible() && !InventoryGui.IsVisible() && !Minimap.IsOpen() && !Console.IsVisible() &&
            !TextInput.IsVisible() && !(Chat.instance && Chat.instance.HasFocus()) && !StoreGui.IsVisible() &&
            !Hud.IsPieceSelectionVisible() && !Hud.InRadial() && !UnifiedPopup.IsVisible() &&
            !(TextViewer.instance && TextViewer.instance.IsVisible());

        /// <summary>Opens on the sign picked before (a pick stays while the window is closed; it goes when its sign unloads,
        /// the player gone far, 0.20.80), never on the one in the crosshair: a player looking at one sign and meaning
        /// another got the wrong one picked (0.20.81, the author: what you aim at when you open the window by hotkey or
        /// otherwise ... should not be auto-picked ... our Pick in view tool is great, use that). Opening on a sign is
        /// OpenFor's (Open in SignLink beside the game's editor, E on a SignLink sign). It opens on the tab OpeningTab
        /// says (0.20.110; before, the tab it was closed on).</summary>
        void Open()
        {
            foreach (Tab t in tabs) t.OnOpen();
            Widgets.ForgetTyping();
            Session.Fresh();
            Picking = false;
            GUIUtility.keyboardControl = 0;
            kit.Opened();
            IsOpen = true;
            ShowTab(OpeningTab(Session.HasTarget ? Target : null));        // clears the status line too
        }

        /// <summary>The sign whose shown content (a picture, a text: ContentHit, 1.1.0) or board a ray meets first, nothing
        /// solid in front of it (its stack's first sign), else the flat sign whose spot it meets; the stack found on the way,
        /// or null (ShowHover finds it then).</summary>
        static Sign AlongRay(Vector3 origin, Vector3 direction, out List<Sign> stack)
        {
            stack = null;
            const float Reach = 60f;
            Sign board = null;
            float solid = Reach;
            if (Physics.Raycast(origin, direction, out RaycastHit hit, Reach, SignPose.Mask, QueryTriggerInteraction.Ignore))
            {
                solid = hit.distance;                                       // a wall, the ground or a board: what lies behind is hidden
                board = hit.collider ? hit.collider.GetComponentInParent<Sign>() : null;
            }
            Sign shown = ContentHit.Nearest(origin, direction.normalized, solid, out _);
            Sign s = shown ? shown : board;
            if (s)
            {
                stack = SignFinder.Stack(s);                                // its first sign, as SignFinder.Root finds it
                return stack.Count > 0 ? stack[0] : s;
            }
            return SignPose.Looked(origin, direction);
        }

        /// <summary>The tab the window opens on for this picked sign: 3 Place for content SignLink made, an edit (0.20.110,
        /// the author: with a sign already selected, open straight in 3 Place); 2 Content for an empty sign or one the
        /// game's editor wrote, which wants content first (0.20.118, the author: only when we open a sign made with
        /// SignLink that already has content); 1 Sign with none picked.</summary>
        static string OpeningTab(Sign picked)
        {
            if (!picked) return Lang.T("1 Sign");
            List<Sign> stack = SignFinder.Stack(picked);
            Sign root = stack.Count > 0 ? stack[0] : picked;
            bool edit = SignMarks.IsSignLinks(stack.Count > 0 ? stack : new List<Sign> { root }) && SignText.SavedText(root).Length > 0;
            return edit ? Lang.T("3 Place") : Lang.T("2 Content");
        }

        /// <summary>The body's scroll back to the top (a tab's content changed under it).</summary>
        public void ScrollToTop() => kit.Scroll = Vector2.zero;

        /// <summary>Shows the tab with this title, as Title says it in the language in use (Lang.T("2 Content")).</summary>
        public void ShowTab(string title)
        {
            for (int i = 0; i < tabs.Length; i++)
                if (tabs[i].Title == title || tabs[i].Title == Lang.T(title))   // the English name too: one call works in every language
                {
                    tab = i;
                    kit.Scroll = Vector2.zero;
                    status = "";
                    Widgets.DropFocus();
                    SettingsTab.StopCapture();
                    return;
                }
        }

        public void Close()
        {
            closeAfterPut = false;
            kit.RunAll(e => Say("error: " + e.Message));                     // queued by queued ones too (0.20.130)
            Busy = false;
            IsOpen = false;
            Picking = false;
            openAt = -1f;
            openForSign = null;
            SettingsTab.StopCapture();
            kit.StopResize();
            SignMove.Revert();                                              // steps not applied are undone (0.20.34: Apply / Undo)
            Armed.Disarm();
            GUIUtility.keyboardControl = 0;
            if (tabs != null)
                foreach (Tab t in tabs)
                {
                    try { t.OnClose(); }
                    catch (Exception e) { Plugin.Log.LogWarning($"window: {e}"); }
                }
            Thumbs.Clear();                                                 // the lists' textures go with the window
            ContentHit.Forget();                                            // no sign kept alive by the hover's measures
            Widgets.DropClose();                                            // an open drop-down list with it
            try { veil.End(); }                                           // the sign as it is, the window shut
            catch (Exception e) { Plugin.Log.LogWarning($"window: {e}"); }
            try { boardDouble.End(); }
            catch (Exception e) { Plugin.Log.LogWarning($"window: {e}"); }
            Plugin.Preview.Destroy();                                       // the preview's copies go with the window
            ghost.Destroy();                                                // made anew when the window opens again (the author:
            hover.Hide();                                                   // reopened on a placed picture, no frame showed)
            hovering = false;
            LookAgain();
        }

        /// <summary>Never leave the game's input blocked (InputBlockPatch reads IsOpen) when the window goes away.</summary>
        void OnDisable()
        {
            // unloaded in a running game (ScriptEngine's reload): shut as the player would, its moves written and its
            // tabs' work stopped (0.20.135, the author: now let us do the OnDestroy solution mentioned); not while the
            // game closes
            if (IsOpen && !quitting && ZNet.instance)
            {
                try { Close(); }
                catch (Exception e) { Plugin.Log.LogDebug($"window: shut on unload: {e.Message}"); }
            }
            IsOpen = false;
            Picking = false;
            try { veil.End(); }
            catch (Exception) { }                                           // shutting down: the signs go too
            try { boardDouble.End(); }
            catch (Exception) { }
            Plugin.Preview?.Hide();
            ghost.Hide();
            hover.Hide();
            hovering = false;
            LookAgain();
        }

        /// <summary>Everything the window made goes: a reload in a running game leaves nothing behind (frames over signs,
        /// the preview's copies, quick views, textures, 0.20.135, the author: now let us do the OnDestroy solution
        /// mentioned).</summary>
        void OnDestroy()
        {
            Application.quitting -= Quitting;
            Thumbs.Clear();
            ghost.Destroy();
            hover.Destroy();
            try
            {
                showAll.Clear();
                boards.Clear();
                Plugin.Preview?.Destroy();
                if (tabs != null) foreach (Tab t in tabs) t.OnDestroy();
                Session.View.Clear();
                Ghost.ReleaseShared();
                Theme.Release();
            }
            catch (Exception e) { Plugin.Log.LogDebug($"window: unload: {e.Message}"); }
            if (ReferenceEquals(Instance, this)) Instance = null;
        }

        /// <summary>Opens the window with this sign's stack selected (E on a picture: the stack's copies are one thing with
        /// their original), on the tab OpeningTab says (on 1 Sign before 0.20.110).</summary>
        public void OpenFor(Sign s)
        {
            if (!s) return;
            if (Broken != null)
            {
                Player.m_localPlayer?.Message(MessageHud.MessageType.Center, Lang.F("SignLink {0} could not load: see BepInEx/LogOutput.log", Plugin.Version));
                return;
            }
            List<Sign> stack = SignFinder.Stack(s);
            if (!IsOpen)
            {
                if (!CanOpen()) return;
                Open();
            }
            if (ShowAll.On) SetShowAll(false);                              // a pick switches Show all off (0.20.88)
            SelectSign(stack.Count > 0 ? stack[0] : s);
            ShowTab(OpeningTab(stack.Count > 0 ? stack[0] : s));
            Say(stack.Count > 1 ? Lang.F("opened ({0} signs).", stack.Count) : Lang.T("opened."));
        }

        /// <summary>Selects what an action just made (a stack made again in a new pose); nothing if it is the target
        /// already. sameContent: the new stack holds what the old one held (Session keeps its view and rows).</summary>
        public void Select(Sign s, bool sameContent = false)
        {
            if (!s || ReferenceEquals(s, Target)) return;
            SelectSign(s, sameContent);
        }

        string[] areaTitle = new string[0];
        bool areaOver;
        float areaTitleAt = -1f;

        /// <summary>The multiplayer area limit in the title (author, 2026-10-11: put it in the title when we are on a
        /// multiplayer server): what SignLink's signs within SaveCost.AreaRadiusM of the selected sign hold, or of the
        /// player with none selected, against SaveCost.AreaLimit; in single player what they hold alone (1.1.0, the author:
        /// there too, without the limit, which means nothing there). Read twice a second. The ways to say it, longest
        /// first: the title takes the longest its room holds (TitleBar).</summary>
        string[] AreaTitle()
        {
            if (Time.unscaledTime - areaTitleAt < 0.5f) return areaTitle;
            areaTitleAt = Time.unscaledTime;
            long used;
            if (Session.HasTarget) used = Session.AreaNow;                 // SignLink's signs only, everyone's (0.20.67)
            else used = Player.m_localPlayer ? SignArea.Others(Player.m_localPlayer.transform.position, null, out _) : 0;
            string u = SaveCost.Label(used), l = SaveCost.Label(SaveCost.AreaLimit);
            if (!SignText.Shared())
            {
                areaOver = false;
                return areaTitle = new[]
                {
                    Lang.F("{0} of SignLink signs within {1} m", u, SaveCost.AreaRadiusM),
                    Lang.F("{0} within {1} m", u, SaveCost.AreaRadiusM),
                    u,
                };
            }
            areaOver = used > SaveCost.AreaLimit;
            return areaTitle = new[]
            {
                areaOver ? Lang.F("{0} / {1} multiplayer area limit (over: only lighter content)", u, l) : Lang.F("{0} / {1} multiplayer area limit", u, l),   // 0.20.108, the author
                areaOver ? Lang.F("{0} / {1} area limit (over)", u, l) : Lang.F("{0} / {1} area limit", u, l),
                $"{u} / {l}",
            };
        }

        /// <summary>The title (0.20.51, the author: the version written smaller, with room; one centred line ran
        /// out of a narrow window at both ends): the name on the left with the version smaller beside it, the area on the
        /// right with room between, its longest wording that fits, else the shortest drawn smaller (Theme.Fitted).</summary>
        void TitleBar() => kit.TitleBar(Plugin.Name, Plugin.Version, AreaTitle(), areaOver);

        /// <summary>How many signs were chosen this session: the footer's kept height starts again with another (KitWindow.Footer).</summary>
        int choices;

        void SelectSign(Sign s, bool sameContent = false)
        {
            SignMove.RevertUnless(s);                                       // another sign: the steps not applied are undone
            if (s && ReferenceEquals(s, Session.Target) && Session.HasTarget)
            {
                Session.Touch();                                            // the same sign again (opening while looking at it):
                showNearby = false;                                         // its quick view, size and frame stay
                Widgets.DropFocus();
                return;
            }
            Session.Select(s, sameContent);
            if (!sameContent) choices++;                                    // another sign: the footer's height starts again
            showNearby = false;                                             // chosen: the list has done its job
            Widgets.DropFocus();
        }

        GUIStyle badge;

        void OnGUI()
        {
            if (closeNow)
            {
                closeNow = false;
                if (IsOpen) Close();                                        // a put worked: Say asked for it
            }
            if (!IsOpen)
            {
                Badge();
                try { EditorButton(); }
                catch (Exception e) { if (Time.unscaledTime - lastUpdateWarning > 10f) { lastUpdateWarning = Time.unscaledTime; Plugin.Log.LogWarning($"window: {e}"); } }
                return;
            }
            if (Event.current.type == EventType.Layout)
            {
                RunLater(all: false);
                Widgets.ApplyFocusDrop();                                   // a field's focus goes only between clicks
            }
            Layout();                                                       // the text scale, the styles, the window's size
            if (Picking)
            {
                PickHint();
                return;
            }
            kit.Show(Draw, () => tabs[tab].Title);                         // kept on the screen, its place kept (KitWindow)
        }

        /// <summary>The text scale, the styles and the window's size and place (KitWindow.Layout, from SignLinkLook).</summary>
        void Layout() => kit.Layout();

        /// <summary>The window's size on screen now, in pixels (the Settings rows say it).</summary>
        public Vector2 Size => kit.Size;

        string[] names;
        string namesFor;
        bool[] usable;

        readonly ShowAll showAll = new ShowAll();
        readonly AllBoards boards = new AllBoards();

        /// <summary>Show all SignLink signs on or off (1 Sign): on drops the pick, and picking waits until it is off.</summary>
        void SetShowAll(bool on)
        {
            ShowAll.Set(on);
            if (!on) return;
            Picking = false;
            showNearby = false;
            if (Session.HasTarget) SelectSign(null);
        }

        string[] closeKeys;
        string closeKeysFor;

        /// <summary>The keys that close the window, beside Close (1.0.2, the author: hotkeys shown where they work, no long
        /// texts); made again only when the hotkey changes (0.20.91, review: every event).</summary>
        string[] CloseKeys()
        {
            string key = PluginConfig.MenuKeyText();
            if (closeKeys != null && ReferenceEquals(key, closeKeysFor)) return closeKeys;
            closeKeysFor = key;
            return closeKeys = key.Length == 0 ? new[] { "Esc" } : new[] { key, "Esc" };
        }

        /// <summary>A tab that shows nothing without a selected sign (2 Content, 3 Place).</summary>
        static bool NeedsSign(Tab t) => t is ContentTab || t is PlaceTab;

        void Draw(int id)
        {
            kit.BeginDraw();
            TitleBar();
            string lang = Lang.Current;
            if (names == null || namesFor != lang)                          // the titles (in the language in use) again when it changes
            {
                names = new string[tabs.Length];
                for (int i = 0; i < tabs.Length; i++) names[i] = tabs[i].Title;
                namesFor = lang;
            }
            // 2 Content and 3 Place work on a selected sign: greyed and closed without one (0.20.73, the author: until a sign
            // is picked, the 2 Content and 3 Place tabs are grey and disabled; they cannot even be looked at)
            if (usable == null) usable = new bool[tabs.Length];
            for (int i = 0; i < tabs.Length; i++) usable[i] = Session.HasTarget || !NeedsSign(tabs[i]);
            if (Event.current.type == EventType.Layout && !usable[tab])
            {
                tab = Array.FindIndex(tabs, x => x is SignTab);             // the sign went (unloaded, deleted): back to 1 Sign
                kit.Scroll = Vector2.zero;
                Widgets.DropFocus();                                        // a field no longer drawn keeps no keyboard (0.20.130)
                SettingsTab.StopCapture();
            }
            int picked = Widgets.Tabs(tab, names, usable, Theme.Tab, Widgets.H(30));
            if (picked != tab) Later(() => { tab = picked; kit.Scroll = Vector2.zero; status = ""; showNearby = false; closeAfterPut = false; Widgets.DropFocus(); SettingsTab.StopCapture(); });   // messages belong to their tab; the list closes
            Widgets.Space(4);                                               // a rule under the tabs as over them (0.20.78, the author:
            Widgets.Rule();                                                 // a separator line under the tabs too)
            Widgets.Space(2);
            try { Session.Refresh(Time.unscaledTime); }                    // Layout events only: the state the tabs draw from
            catch (Exception e) { Failed(e); }
            if (Event.current.type == EventType.Layout)
            {
                content.Follow(Session);                                    // the player's own source back, whatever tab shows
                if (!(tabs[tab] is ContentTab) && !(tabs[tab] is PlaceTab))  // those tick it themselves: on the other tabs the
                {                                                           // frame still needs the pick's size and place
                    try { content.Current.Tick(Session, Time.unscaledTime); }
                    catch (Exception e) { Failed(e); }
                }
                // the board's frame red while nothing would hold the sign where the steps or the pose to come put it (1.0.2)
                try
                {
                    ContentTab.PoseProblem(Session);
                    ghost.Update(Session, content.Current, ContentTab.PoseUnheld);
                }
                catch (Exception e) { Failed(e); }
                try { Plugin.Preview.Follow(Session, content.Current, Time.unscaledTime, Busy); }   // the result, before the button (0.20.129)
                catch (Exception e) { Failed(e); }
                try { veil.Follow(Session, Busy, Plugin.Preview.Showing); }   // the sign's own hides under it, or under a new pick
                catch (Exception e) { Failed(e); }
                try { boardDouble.Follow(Session, Busy); }                // the board in the pose still to come
                catch (Exception e) { Failed(e); }
                if (ListHovering) ShowHover(listHover);                     // the row of Signs nearby under the mouse: its frame in the world
            }
            Tab t = tabs[tab];

            kit.BeginTop();                                                 // the parts above the body (steady while held)
            if (t.UsesTarget)
            {
                try
                {
                    // the sign picker on 1 Sign only (0.20.73, the author: from now on the sign picker shows only
                    // on the 1 Sign tab); 2 Content and 3 Place open only with a sign
                    bool picker = t is SignTab;
                    if (picker) TargetBar();
                    if (Session.HasTarget && Session.Reason != null) GUILayout.Label(Session.Reason, Theme.Warn);
                    if (Session.HasTarget && Session.Missing != null) Widgets.Hint(Session.Missing);
                    // a selected sign without any frame says why (both switches off: each is a part of its own since 1.1.0)
                    if (Session.HasTarget && !PluginConfig.Highlight.Value && !PluginConfig.HighlightContent.Value)
                        GUILayout.Label(Lang.T("Highlight off (Settings)"), Theme.Notice);
                    if (picker && showNearby) NearbyPanel();
                }
                catch (Exception e) when (!(e is ExitGUIException))
                {
                    Failed(e);
                }
            }
            try { t.Pinned(); }                                             // in view whatever the body's scroll
            catch (Exception e) when (!(e is ExitGUIException))
            {
                Failed(e);
            }
            kit.EndTop();
            Widgets.Space(4);
            kit.Body(drawBody ?? (drawBody = DrawBody));                   // made once: no delegate an event (0.20.132)
            // as tall as it was for this tab and choice; no box for a tab with no footer (the status line alone)
            kit.Footer(tab * 1000003 + choices, drawFooter ?? (drawFooter = DrawFooter), t.HasFooter);
            if (kit.CloseRow(CloseKeys(), drawShowAll ?? (drawShowAll = ShowAllBox))) Close();   // Show all at its start (1.1.0)
            kit.EndDraw();
        }

        Action drawBody, drawFooter, drawShowAll;

        /// <summary>The tab's body, inside the kit's scroll.</summary>
        void DrawBody()
        {
            try { tabs[tab].Body(); }
            catch (Exception e) when (!(e is ExitGUIException))
            {
                Failed(e);
            }
        }

        /// <summary>The tab's footer and the status line (Done: / Could not:), inside the kit's footer section.</summary>
        void DrawFooter()
        {
            try { tabs[tab].Footer(); }
            catch (Exception e) when (!(e is ExitGUIException))
            {
                Failed(e);
            }
            // decided at the Layout: the time runs on between it and the Repaint, and a line that came or went in
            // between would throw the layout
            if (Event.current.type == EventType.Layout) statusFresh = status.Length > 0 && Time.unscaledTime - statusAt < StatusSeconds;
            bool fresh = statusFresh;
            bool failed = status.StartsWith("error", StringComparison.Ordinal);
            // in a footer box only while it says something (1.1.0, the author: an empty grey line stayed under the
            // button); alone, on a tab with no footer, it keeps its line so the Close row stays put
            if (!fresh && tabs[tab].HasFooter) return;
            GUILayout.Label(!fresh ? " " : failed ? Lang.F("Could not: {0}", status.Substring(Math.Min(status.Length, 7))) : Lang.F("Done: {0}", status),
                            failed ? Theme.Warn : Theme.Good);
        }

        bool statusFresh;                                                   // DrawFooter's status line, decided at the Layout
        string lastFailure;

        /// <summary>A tab threw while drawing: the status line says so, the log has the details (once per message), and
        /// the rest of the window draws enabled.</summary>
        void Failed(Exception e)
        {
            GUI.enabled = true;
            Later(() => Say("error: " + e.Message));
            if (e.Message == lastFailure) return;
            lastFailure = e.Message;
            Plugin.Log.LogWarning($"window: {e}");
        }

        public bool HasTarget => Session.HasTarget;

        /// <summary>Text sizes (Settings). 2 went and 0.6 and 0.7 came (0.20.120, the author: text size 1.5 and 2 look the
        /// same to me; take 2 out and add one even smaller than 0.8): the size times the screen's height over
        /// 1080 stops at 3 (Layout), so on a 4K screen 1.5 is 3 already and 2 was the same.</summary>
        public static readonly float[] Scales = { 0.6f, 0.7f, 0.8f, 0.9f, 1f, 1.15f, 1.3f, 1.5f };
    }
}
