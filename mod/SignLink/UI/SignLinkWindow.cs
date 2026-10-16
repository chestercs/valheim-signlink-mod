using System;
using System.Collections.Generic;
using BepInEx.Configuration;
using SignLink.Pictures;
using SignLink.Signs;
using UnityEngine;

namespace SignLink.UI
{
    /// <summary>The SignLink window (PageUp): a shell around the tabs in UI/Tabs (1 Sign, 2 Content, 3 Place, Playing,
    /// Settings). Above the tabs' bodies sits the target sign bar (the sign you looked at when opening it, or picked
    /// later); below them each tab's footer (its main button, always in view) and the one status line. IMGUI with its own
    /// styles (Theme, at the player's Opacity): no assets, no dependencies; sizes are logical units drawn through Widgets.U at the text scale.
    /// While it is open the game takes no input (InputBlockPatch, ScrollBlockPatch, InventoryBlockPatch); Pick in view
    /// hides it until a click on a sign.</summary>
    internal sealed class SignLinkWindow : MonoBehaviour
    {
        public static bool IsOpen { get; private set; }

        /// <summary>Pick in view: the window is hidden and the next click selects the sign under the cursor (the game's
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
                return new Rect(w.rect.x, w.rect.y, Mathf.Max(w.rect.width, w.drawnWidth), Mathf.Max(w.rect.height, w.drawnHeight)).Contains(at);
            }
        }

        const int WindowId = 0x51C9;
        Rect rect = new Rect(60, 60, WindowSizing.MinWidth, 640);         // screen pixels
        Vector2 scroll;
        Tab[] tabs;
        ContentTab content;
        int tab;
        string status = "";
        /// <summary>State changes from clicks wait for the first Layout event of a later frame: IMGUI draws every event
        /// twice (Layout, then the event), and controls that appear or vanish in between log "Getting control's position"
        /// errors; and the frame the click was in still draws (its button says "Working ..." for an Act).</summary>
        readonly List<Action> later = new List<Action>();
        readonly List<int> laterFrames = new List<int>();

        /// <summary>The window's shared state: the selected sign, its stack, what it holds, what the tabs chose.</summary>
        public readonly Session Session = new Session();

        /// <summary>The frame in the world where the chosen content goes (Settings › Sign options › Highlight signs).</summary>
        readonly Ghost ghost = new Ghost();
        /// <summary>The frame around the sign under the cursor (Pick in view, a row of Signs nearby): the content green
        /// and red as the selection's, the board cool white instead of neon lime.</summary>
        readonly Ghost hover = new Ghost(Ghost.Fill, Ghost.Line, Ghost.HoverBoard);
        bool hovering;
        /// <summary>The selected sign shows nothing on this computer while the live preview shows, or a new picture or
        /// piece is picked over it.</summary>
        readonly HeldVeil veil = new HeldVeil();
        // the row of Signs nearby the mouse is over (set while the row repaints; stale after a couple of frames)
        Sign listHover;
        int listHoverAt = -10;

        bool ListHovering => IsOpen && showNearby && listHover && Time.frameCount - listHoverAt <= 2;

        /// <summary>The sign the tabs work on: for a stack, its rearmost sign (SignFinder.Hovered).</summary>
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
            Instance = this;                                                // useGUILayout stays on: shut, OnGUI draws only the badge and the editor's button
            Application.quitting += Quitting;
            Session.Changed += () =>
            {
                barAt = -1f;                                                // the bar reads the sign again at once
                LookAgain();                                                // the hover frame looks again (Pick in view, the list)
                Plugin.Restore.Soon();                                      // a moving GIF's screen comes back at once after a change
            };
            try
            {
                content = new ContentTab(this);
                tabs = new Tab[] { new SignTab(this, content), content, new PlaceTab(this, content), new PlayingTab(this), new SettingsTab(this) };
                if (!Plugin.Patched(typeof(Patches.InputBlockPatch)))
                    Broken = "the input blocker did not apply (a game update?)";   // a window the game plays through is worse than none
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

        public void Later(Action a)
        {
            later.Add(a);
            laterFrames.Add(Time.frameCount);
        }

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
            if (later.Count == 0)                                           // nothing queued: no list made (0.20.132, review)
            {
                Busy = false;
                return;
            }
            int frame = Time.frameCount;
            var due = new List<Action>();
            for (int i = 0; i < later.Count;)
            {
                if (all || laterFrames[i] < frame)
                {
                    due.Add(later[i]);
                    later.RemoveAt(i);
                    laterFrames.RemoveAt(i);
                }
                else i++;
            }
            foreach (Action a in due)
            {
                try { a(); }
                catch (Exception e) { Say("error: " + e.Message); }
            }
            if (later.Count == 0) Busy = false;
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
            try { MessageHud.instance?.ShowMessage(MessageHud.MessageType.TopLeft, "SignLink: " + said); }
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
        float lastUpdateWarning = -100f;
        float pickingAt;

        /// <summary>Guarded: a game update that breaks one of the UI checks (CanOpen) must not throw once a frame for
        /// 30 s; three failures in a row mark the window Broken, which the command and the main menu say.</summary>
        void Update()
        {
            if (Broken != null) return;
            try
            {
                if (IsOpen && !Player.m_localPlayer) Close();              // died, logged out, lost the connection
                showAll.Step(Time.unscaledTime);                            // Show all SignLink signs: open or shut
                // every sign's board in faint grey while a click could select one (0.20.172, the author)
                boards.Step(Time.unscaledTime, IsOpen && (Picking || !HasTarget) && !ShowAll.On);
                // a click on a sign selects it, without Pick in view (0.20.172, the author: whenever I click a sign, select
                // it, without Pick in view); a click on the window, or off any sign, does nothing here
                if (IsOpen && !Picking && !ShowAll.On && Input.GetMouseButtonDown(0) && !PointerOver && GUIUtility.hotControl == 0)
                    ClickAt(Input.mousePosition);
                if (IsOpen && Picking)
                {
                    if (Input.GetMouseButtonDown(0) && Time.unscaledTime - pickingAt > 0.2f) PickAt(Input.mousePosition);
                    else if (Input.GetMouseButtonDown(1)) Picking = false;
                    else HoverAt(Input.mousePosition);
                }
                else if (IsOpen && tabs[tab] is PlaceTab placeTab) placeTab.Keys();   // arrow keys move the sign
                else if (IsOpen && tabs[tab] is ContentTab contentTab) contentTab.Keys(Session);   // up and down step the list
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
                    if (IsOpen) Close();
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
                if (++updateFailures >= 3) Broken = $"{e.GetType().Name}: {e.Message}";
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
            logOpen = true;
            IsOpen = true;
            ShowTab(OpeningTab(Session.HasTarget ? Target : null));        // clears the status line too
        }

        /// <summary>The sign whose board a ray hits (its stack's first sign), else the flat sign whose spot it meets; the
        /// stack found on the way, or null (ShowHover finds it then).</summary>
        static Sign AlongRay(Vector3 origin, Vector3 direction, out List<Sign> stack)
        {
            stack = null;
            if (Physics.Raycast(origin, direction, out RaycastHit hit, 60f, SignPose.Mask, QueryTriggerInteraction.Ignore))
            {
                Sign s = hit.collider ? hit.collider.GetComponentInParent<Sign>() : null;
                if (s)
                {
                    stack = SignFinder.Stack(s);                            // its first sign, as SignFinder.Root finds it
                    return stack.Count > 0 ? stack[0] : s;
                }
            }
            return SignPose.Looked(origin, direction);
        }

        /// <summary>The tab the window opens on for this picked sign: 3 Place for content SignLink made, an edit (0.20.110,
        /// the author: with a sign already selected, open straight in 3 Place); 2 Content for an empty sign or one the
        /// game's editor wrote, which wants content first (0.20.118, the author: only when we open a sign made with
        /// SignLink that already has content); 1 Sign with none picked.</summary>
        static string OpeningTab(Sign picked)
        {
            if (!picked) return "1 Sign";
            List<Sign> stack = SignFinder.Stack(picked);
            Sign root = stack.Count > 0 ? stack[0] : picked;
            bool edit = SignMarks.IsSignLinks(stack.Count > 0 ? stack : new List<Sign> { root }) && SignText.SavedText(root).Length > 0;
            return edit ? "3 Place" : "2 Content";
        }

        /// <summary>The body's scroll back to the top (a tab's content changed under it).</summary>
        public void ScrollToTop() => scroll = Vector2.zero;

        /// <summary>Shows the tab with this title ("2 Content").</summary>
        public void ShowTab(string title)
        {
            for (int i = 0; i < tabs.Length; i++)
                if (tabs[i].Title == title)
                {
                    tab = i;
                    scroll = Vector2.zero;
                    status = "";
                    Widgets.DropFocus();
                    SettingsTab.StopCapture();
                    return;
                }
        }

        public void Close()
        {
            closeAfterPut = false;
            for (int round = 0; round < 8 && later.Count > 0; round++) RunLater(all: true);   // queued by queued ones too (0.20.130)
            later.Clear();
            laterFrames.Clear();
            Busy = false;
            IsOpen = false;
            Picking = false;
            openAt = -1f;
            openForSign = null;
            SettingsTab.StopCapture();
            StopResize();
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
            try { veil.End(); }                                             // the sign as it is, the window shut
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
                Player.m_localPlayer?.Message(MessageHud.MessageType.Center, $"SignLink {Plugin.Version} could not load: see BepInEx/LogOutput.log");
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
            Say(stack.Count > 1 ? $"opened for this picture ({stack.Count} signs); the game's editor would cut its text to 50 characters."
                                : "opened for this sign; the game's editor would cut its text to 50 characters.");
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
        /// player with none selected, against SaveCost.AreaLimit; nothing in single player. Read twice a second. The ways
        /// to say it, longest first: the title takes the longest its room holds (TitleBar).</summary>
        string[] AreaTitle()
        {
            if (Time.unscaledTime - areaTitleAt < 0.5f) return areaTitle;
            areaTitleAt = Time.unscaledTime;
            if (!SignText.Shared()) return areaTitle = new string[0];
            long used;
            if (Session.HasTarget) used = Session.AreaNow;                 // SignLink's signs only, everyone's (0.20.67)
            else used = Player.m_localPlayer ? SignArea.Others(Player.m_localPlayer.transform.position, null, out _) : 0;
            areaOver = used > SaveCost.AreaLimit;
            string u = SaveCost.Label(used), l = SaveCost.Label(SaveCost.AreaLimit);
            return areaTitle = new[]
            {
                $"{u} / {l} multiplayer area limit" + (areaOver ? " (over: only lighter content)" : ""),   // 0.20.108, the author
                $"{u} / {l} area limit" + (areaOver ? " (over)" : ""),
                $"{u} / {l}",
            };
        }

        /// <summary>The title (0.20.51, the author: the version written smaller, with room; one centred line ran
        /// out of a narrow window at both ends): the name on the left with the version smaller beside it, the area on the
        /// right with room between, its longest wording that fits, else the shortest drawn smaller (Theme.Fitted).</summary>
        void TitleBar()
        {
            float x = Widgets.U(12), top = Widgets.U(6), h = Widgets.U(30), right = rect.width - Widgets.U(12);
            var name = new GUIContent(Plugin.Name);
            var version = new GUIContent(Plugin.Version);
            float nw = Theme.TitleName.CalcSize(name).x, vw = Theme.TitleVersion.CalcSize(version).x;
            GUI.Label(new Rect(x, top, nw, h), name, Theme.TitleName);
            GUI.Label(new Rect(x + nw + Widgets.U(7), top, vw, h - Widgets.U(5)), version, Theme.TitleVersion);   // on the name's baseline
            string[] area = AreaTitle();
            float left = x + nw + Widgets.U(7) + vw + Widgets.U(28), room = right - left;
            if (area.Length == 0 || room < Widgets.U(40)) return;
            GUIStyle style = areaOver ? Theme.TitleAreaOver : Theme.TitleArea;
            var said = new GUIContent(area[area.Length - 1]);
            foreach (string a in area)
            {
                var c = new GUIContent(a);
                if (style.CalcSize(c).x <= room)
                {
                    said = c;
                    break;
                }
            }
            GUI.Label(new Rect(left, top, room, h), said, Theme.Fitted(style, said, room));
        }

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

        void StartPicking()
        {
            Picking = true;
            pickingAt = Time.unscaledTime;
            showNearby = false;
            LookAgain();
            Widgets.DropFocus();
        }

        // what the hover frame was last worked out for (0.20.132, review: two raycasts and two or three passes over every
        // loaded sign each frame while picking, the mouse and the camera still): the mouse and the camera's pose for
        // HoverAt, the stack's first sign for ShowHover, and when; looked at again at least every LookEvery seconds (a sign
        // that appeared or changed under a still cursor), and at once when the window's state changes (LookAgain)
        Vector3 hoverMouse, hoverCamPos;
        Quaternion hoverCamRot;
        float hoverLookAt = -100f, hoverForAt = -100f;
        Sign hoverFor, hoverFirst, hoverHost;
        List<Sign> hoverStack;
        const float LookEvery = 0.5f;

        /// <summary>The hover frame is worked out again at the next look (another sign selected, picking started, the
        /// frame hidden).</summary>
        void LookAgain()
        {
            hoverLookAt = hoverForAt = -100f;
            hoverFor = null;
        }

        /// <summary>While picking, every frame: the sign the click would select gets the hover frame (its content and
        /// its board, in cool white), so the target shows before the click; nothing under the cursor hides it. With the
        /// mouse and the camera still, the last look stands for LookEvery seconds.</summary>
        void HoverAt(Vector3 screen)
        {
            Camera cam = Camera.main;
            float now = Time.unscaledTime;
            Vector3 camPos = cam ? cam.transform.position : Vector3.zero;
            Quaternion camRot = cam ? cam.transform.rotation : Quaternion.identity;
            if (now - hoverLookAt < LookEvery && screen.Equals(hoverMouse) && camPos.Equals(hoverCamPos) && camRot.Equals(hoverCamRot)) return;
            hoverLookAt = now;
            hoverMouse = screen;
            hoverCamPos = camPos;
            hoverCamRot = camRot;
            Sign root = null;
            List<Sign> stack = null;
            if (cam)
            {
                Ray ray = cam.ScreenPointToRay(screen);
                root = AlongRay(ray.origin, ray.direction, out stack);
            }
            if (!root)
            {
                if (hovering) hover.Hide();
                hovering = false;
                hoverFor = null;
                return;
            }
            ShowHover(root, stack);
        }

        /// <summary>The hover frame on this sign's stack (its first sign; the stack when the caller found it already); none
        /// on the selected stack while it wears the selection's frame (0.20.92, the author: the already selected sign has a
        /// layer ... there are 2 on the sign). The same first sign within LookEvery seconds keeps the frame as it is.</summary>
        void ShowHover(Sign root, List<Sign> stack = null)
        {
            float now = Time.unscaledTime;
            if (ReferenceEquals(root, hoverFor) && now - hoverForAt < LookEvery)
            {
                // the stack as found: the frame still follows the camera (its side; ShowStack is cheap when nothing changed)
                if (hovering && hoverFirst)
                {
                    try { hover.ShowStack(hoverFirst, hoverHost, hoverStack); }
                    catch (Exception e) { Failed(e); }
                }
                return;
            }
            hoverFor = root;
            hoverForAt = now;
            if (stack == null) stack = SignFinder.Stack(root);
            Sign first = stack.Count > 0 ? stack[0] : root;
            if (ghost.Frames(first))
            {
                if (hovering) hover.Hide();
                hovering = false;
                return;
            }
            Sign host = stack.Count > 0 ? stack[stack.Count - 1] : root;
            hoverFirst = first;
            hoverHost = host;
            hoverStack = stack.Count > 0 ? stack : new List<Sign> { root };
            try { hover.ShowStack(first, host, hoverStack); }
            catch (Exception e) { Failed(e); }
            hovering = true;
        }

        /// <summary>The click while picking: the sign under the cursor by a ray from the camera (its board, or where a
        /// lying sign stood); the window shows again either way.</summary>
        void PickAt(Vector3 screen)
        {
            Picking = false;
            Camera cam = Camera.main;
            Sign s = null;
            if (cam)
            {
                Ray ray = cam.ScreenPointToRay(screen);
                s = AlongRay(ray.origin, ray.direction, out _);
            }
            if (s)
            {
                KeepOpen();                                                 // the player's own pick: no put closes it now
                SelectSign(s);
                Say($"selected {Called(s)}.");
            }
            else
            {
                Session.Touch();
                Say("error: no sign there. Click a sign's board, or where a lying sign stood.");
            }
        }

        /// <summary>A click in the world while the window is open: the sign under it is selected, as Pick in view selects it;
        /// off any sign nothing happens (no message: a click in the world is not always meant for a sign).</summary>
        void ClickAt(Vector3 screen)
        {
            Camera cam = Camera.main;
            if (!cam) return;
            Ray ray = cam.ScreenPointToRay(screen);
            Sign s = AlongRay(ray.origin, ray.direction, out _);
            if (!s || Target && ReferenceEquals(SignFinder.Root(s), SignFinder.Root(Target))) return;   // already the selected one
            KeepOpen();
            SelectSign(s);
            Say($"selected {Called(s)}.");
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
            // kept on the screen as it was drawn: a row wider than the window widens it (GUILayout), and clamped by its own
            // width it stood past the screen's edge (0.20.98, the author: it always lets it be pushed out to the right by
            // exactly the same amount)
            rect.x = Mathf.Clamp(rect.x, 0, Mathf.Max(0, Screen.width - Mathf.Max(rect.width, drawnWidth)));
            rect.y = Mathf.Clamp(rect.y, 0, Mathf.Max(0, Screen.height - Mathf.Max(rect.height, drawnHeight)));
            float wantWidth = rect.width;
            rect = GUILayout.Window(WindowId, rect, Draw, "", Theme.Window,             // the title: TitleBar
                                    GUILayout.Width(rect.width), GUILayout.Height(rect.height));
            if (Event.current.type == EventType.Repaint)
            {
                drawnWidth = rect.width;
                drawnHeight = rect.height;
                if (rect.width > wantWidth + 1f && !overSaid)               // once a window: which tab has the wide row
                {
                    overSaid = true;
                    Plugin.Log.LogWarning($"window: drawn {rect.width:0} px wide, {rect.width - wantWidth:0} px over its width " +
                                          $"(a row on {tabs[tab].Title} is wider than the window)");
                }
            }
            if (Event.current.type == EventType.Repaint && GUIUtility.hotControl == 0 &&
                (Mathf.Abs(rect.x - savedX) > 0.5f || Mathf.Abs(rect.y - savedY) > 0.5f))
                SavePlace();                                                // a drag ended: the place is kept
        }

        /// <summary>While picking: one line in the upper middle of the screen, the window hidden.</summary>
        void PickHint()
        {
            const string text = "Click a sign  ·  green: its content, white box: the sign's board  ·  Esc cancels";
            Vector2 size = Theme.Header.CalcSize(new GUIContent(text));
            float pad = Widgets.U(24);
            var box = new Rect((Screen.width - size.x - 2 * pad) / 2f, Screen.height * 0.12f, size.x + 2 * pad, size.y + pad);
            GUI.Box(box, GUIContent.none, Theme.Section);
            GUI.Label(new Rect(box.x + pad, box.y + pad / 2, size.x, size.y), text, Theme.Header);
        }

        /// <summary>A button above the game's own sign editor while it shows for a sign: "Open in SignLink" hides the
        /// editor (nothing written) and opens the window on that sign a frame later (the editor counts as visible until
        /// then). The game's editor cuts a text to 50 characters; SignLink takes any length, with templates and a
        /// preview. Placed over the panel's top edge in screen pixels (its canvas may be an overlay or a camera's).</summary>
        void EditorButton()
        {
            if (Broken != null) return;
            TextInput ti = TextInput.instance;
            if (!ti || !ti.m_panel || !ti.m_panel.activeSelf) return;
            Sign sign = ti.m_queuedSign as Sign;
            if (!sign) return;
            Layout();                                                       // the styles at the window's text scale
            float top = Screen.height * 0.4f, centreX = Screen.width / 2f;
            if (ti.m_panel.transform is RectTransform rt)
            {
                Canvas canvas = ti.m_panel.GetComponentInParent<Canvas>();
                Camera cam = canvas && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
                var corners = new Vector3[4];
                rt.GetWorldCorners(corners);                                 // bottom-left, top-left, top-right, bottom-right
                Vector2 bl = RectTransformUtility.WorldToScreenPoint(cam, corners[0]);
                Vector2 tr = RectTransformUtility.WorldToScreenPoint(cam, corners[2]);
                top = Screen.height - Mathf.Max(bl.y, tr.y);                 // the panel's top edge; IMGUI counts from the top
                centreX = (bl.x + tr.x) / 2f;
            }
            const string label = "Open in SignLink";
            Vector2 size = Theme.Button.CalcSize(new GUIContent(label));
            float bh = size.y + Widgets.U(8);
            var box = new Rect(centreX - size.x / 2f - Widgets.U(8), Mathf.Max(0f, top - Widgets.U(10) - bh), size.x + Widgets.U(16), bh);   // above the panel
            if (GUI.Button(box, label, Theme.Button))
            {
                ti.m_queuedSign = null;                                     // the editor writes nothing when it goes
                ti.Hide();
                OpenSoonFor(sign);
            }
        }

        /// <summary>Only when the window failed to load: a line in the main menu's bottom right corner that says so (the
        /// version is in the chat: /signlink version).</summary>
        void Badge()
        {
            if (Broken == null || !FejdStartup.instance || Player.m_localPlayer) return;
            if (badge == null)
                badge = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.LowerRight, fontSize = Mathf.Max(12, Screen.height / 75) };
            badge.normal.textColor = new Color(1f, 0.45f, 0.35f);
            GUI.Label(new Rect(0, 0, Screen.width - 12, Screen.height - 8),
                      $"SignLink {Plugin.Version} could not load: see BepInEx/LogOutput.log", badge);
        }

        bool placed, logOpen, overSaid;
        float drawnWidth, drawnHeight;                                      // as GUILayout drew it last (a wide row widens it)
        float savedX, savedY;

        /// <summary>The text scale and the window's size, in screen pixels: the window takes its share of the screen
        /// (Width and Height, on Settings or by its edges and corners: WindowSizing), up to the whole screen, the text its
        /// own scale (Text size); a window too narrow for the rows
        /// at that text size grows, and on a screen too small for both the text shrinks. The styles are built for the scale, and rows and lists follow
        /// (Widgets.Scale, ContentWidth, HeightFactor). The place it was dragged to is kept in the config.</summary>
        void Layout()
        {
            float scale = Mathf.Clamp(Screen.height / 1080f * PluginConfig.MenuScale.Value, 0.5f, 3f);
            WindowSizing.Size(Screen.width, Screen.height, SettingsTab.PreviewWidth ?? PluginConfig.MenuWidth.Value,
                              SettingsTab.PreviewHeight ?? PluginConfig.MenuHeight.Value, scale, out float w, out float h, out float s);
            rect.width = w;
            rect.height = h;
            if (resizing) rect.position = dragPlace;                        // a left or top edge held: the place follows it
            Widgets.Scale = s;
            Widgets.ContentWidth = w / s - 64f;
            Widgets.HeightFactor = Mathf.Clamp(h / s / 760f, 1f, 1.6f);
            Theme.Ensure(s, SettingsTab.PreviewOpacity ?? PluginConfig.MenuOpacity.Value);   // a dragged Opacity slider shows at once
            if (!placed)
            {
                placed = true;
                if (PluginConfig.WindowX.Value >= 0f) rect.x = PluginConfig.WindowX.Value;
                if (PluginConfig.WindowY.Value >= 0f) rect.y = PluginConfig.WindowY.Value;
                savedX = rect.x;
                savedY = rect.y;
            }
            if (logOpen)
            {
                logOpen = false;                                            // one line per open: what the window was built for (a retest reads it)
                overSaid = false;
                drawnWidth = drawnHeight = 0f;
                Plugin.Log.LogInfo($"window: open at text scale {s:0.00}, {w:0} x {h:0} px at {rect.x:0},{rect.y:0} on {Screen.width} x {Screen.height}");
            }
        }

        /// <summary>The window's size on screen now, in pixels (the Settings rows say it).</summary>
        public Vector2 Size => rect.size;

        int gripId;
        bool resizing;
        int dragEdges;                                                      // WindowSizing.Left | Right | Top | Bottom
        Rect dragFrom;
        Vector2 gripFrom, dragPlace;

        /// <summary>The mouse on screen, from the top left like IMGUI (Input counts from the bottom).</summary>
        static Vector2 Mouse => new Vector2(Input.mousePosition.x, Screen.height - Input.mousePosition.y);

        /// <summary>Which edges of the window lie under a point of it (its own frame): a band along each edge in its
        /// margins, clear of the rows, and a square at each corner (two edges).</summary>
        int EdgesAt(Vector2 p)
        {
            if (p.x < 0 || p.y < 0 || p.x > rect.width || p.y > rect.height) return 0;
            float band = Widgets.U(7), corner = Widgets.U(16);
            bool cl = p.x <= corner, cr = p.x >= rect.width - corner, ct = p.y <= corner, cb = p.y >= rect.height - corner;
            if ((cl || cr) && (ct || cb))
                return (cl ? WindowSizing.Left : WindowSizing.Right) | (ct ? WindowSizing.Top : WindowSizing.Bottom);
            return (p.x <= band ? WindowSizing.Left : 0) | (p.x >= rect.width - band ? WindowSizing.Right : 0) |
                   (p.y <= band ? WindowSizing.Top : 0) | (p.y >= rect.height - band ? WindowSizing.Bottom : 0);
        }

        /// <summary>Every edge and corner of the window resizes it (0.20.46 the bottom-right corner; 0.20.50 all of them,
        /// the author: at its corners and edges too, of course so that it cannot be dragged off-screen): the opposite edge
        /// stays, never past the screen nor below what the rows need (WindowSizing.Drag). The size goes to Width and
        /// Height as the Settings rows set it (shown at once, written once let go), the place to WindowX and WindowY (Layout puts it before the window
        /// is drawn: the window's own rect is its return value). First in Draw, so a press on an edge is the drag's before
        /// a row takes it; while held, the mouse is read on every event, since drag events stop reaching the window once
        /// the mouse leaves it. EdgeMarks paints the edges last.</summary>
        void ResizeEdges()
        {
            Event e = Event.current;
            gripId = GUIUtility.GetControlID(FocusType.Passive);
            if (!resizing)
            {
                if (e.type != EventType.MouseDown || e.button != 0) return;
                int at = EdgesAt(e.mousePosition);
                if (at == 0) return;
                resizing = true;
                dragEdges = at;
                GUIUtility.hotControl = gripId;                             // no row reacts meanwhile, nothing is written
                gripFrom = Mouse;
                dragFrom = rect;
                dragPlace = rect.position;
                e.Use();
                return;
            }
            if (!Input.GetMouseButton(0))
            {
                StopResize();
                if (e.type == EventType.MouseUp) e.Use();
                return;
            }
            Vector2 d = Mouse - gripFrom;
            float minW = Mathf.Max(WindowSizing.MinWidth * Widgets.Scale, WindowSizing.WidthLeast * Screen.width);
            float minH = Mathf.Max(WindowSizing.MinHeight * Widgets.Scale, WindowSizing.HeightLeast * Screen.height);
            WindowSizing.Drag(dragFrom.x, dragFrom.y, dragFrom.width, dragFrom.height, d.x, d.y, dragEdges, minW, minH,
                              Screen.width, Screen.height, out float nx, out float ny, out float nw, out float nh);
            if (e.type == EventType.Repaint)                                // the frame's last event: the next frame's Layout lays
            {                                                               // out at the new size, the same for all its events
                dragPlace = new Vector2(nx, ny);                            // (0.20.130, review: rows that wrap by the width
                SettingsTab.Resize(nw / Screen.width, nh / Screen.height);  // changed between Layout and MouseDrag)
            }
            if (e.type == EventType.MouseDrag || e.type == EventType.MouseUp) e.Use();
        }

        /// <summary>The edge let go (or the window closed while held): the size is kept, and the place (SavePlace,
        /// once nothing is held).</summary>
        void StopResize()
        {
            if (!resizing) return;
            resizing = false;
            if (GUIUtility.hotControl == gripId) GUIUtility.hotControl = 0;
            SettingsTab.KeepSize();
        }

        /// <summary>The edges' marks: six dots in the bottom-right corner, and an amber line along the edge (or the two
        /// of a corner) under the mouse or held, so it shows where the window can be taken.</summary>
        void EdgeMarks()
        {
            if (Event.current.type != EventType.Repaint) return;
            Color was = GUI.color;
            float d = Mathf.Max(2f, Widgets.U(2.5f)), g = Widgets.U(16);
            GUI.color = new Color(1f, 1f, 1f, resizing ? 0.9f : 0.45f);
            for (int i = 0; i < 3; i++)
                for (int j = 0; j <= i; j++)
                    GUI.DrawTexture(new Rect(rect.width - d * 2f * (j + 1), rect.height - d * 2f * (i - j + 1), d, d), Texture2D.whiteTexture);
            int lit = resizing ? dragEdges : GUIUtility.hotControl == 0 ? EdgesAt(Event.current.mousePosition) : 0;
            if (lit != 0)
            {
                float t = Mathf.Max(2f, Widgets.U(3));
                GUI.color = new Color(0.98f, 0.76f, 0.36f, resizing ? 0.95f : 0.7f);
                if ((lit & WindowSizing.Left) != 0) GUI.DrawTexture(new Rect(0, 0, t, rect.height), Texture2D.whiteTexture);
                if ((lit & WindowSizing.Right) != 0) GUI.DrawTexture(new Rect(rect.width - t, 0, t, rect.height), Texture2D.whiteTexture);
                if ((lit & WindowSizing.Top) != 0) GUI.DrawTexture(new Rect(0, 0, rect.width, t), Texture2D.whiteTexture);
                if ((lit & WindowSizing.Bottom) != 0) GUI.DrawTexture(new Rect(0, rect.height - t, rect.width, t), Texture2D.whiteTexture);
            }
            // the title's own line: a little room under it, then a thin rule (0.20.50, the author: a little separation under it)
            GUI.color = new Color(0.98f, 0.76f, 0.36f, 0.45f);
            GUI.DrawTexture(new Rect(Widgets.U(12), Widgets.U(TitleRule), rect.width - Widgets.U(24), Mathf.Max(1f, Widgets.U(1.5f))), Texture2D.whiteTexture);
            GUI.color = was;
        }

        /// <summary>Where the rule under the title runs (logical units from the window's top; Theme.Window's padding
        /// starts the rows a little under it).</summary>
        const float TitleRule = 37f;

        void SavePlace()
        {
            savedX = rect.x;
            savedY = rect.y;
            PluginConfig.WindowX.Value = Mathf.Round(rect.x);
            PluginConfig.WindowY.Value = Mathf.Round(rect.y);
        }

        string[] names;
        bool[] usable;

        readonly ShowAll showAll = new ShowAll();
        readonly AllBoards boards = new AllBoards();
        static readonly string[] OnOff = { "On", "Off" };

        /// <summary>Show all SignLink signs on or off (1 Sign): on drops the pick, and picking waits until it is off.</summary>
        void SetShowAll(bool on)
        {
            ShowAll.Set(on);
            if (!on) return;
            Picking = false;
            showNearby = false;
            if (Session.HasTarget) SelectSign(null);
        }

        // the window's parts as last drawn (screen pixels), kept as their least height while a control is held (steady)
        float topMin, bodyMin;
        bool steady;

        // the footer only grows while the same tab shows the same chosen sign in a window of the same width (0.20.84,
        // the author: as I drag the Right slider in 3 Place the window jumps ... no window jumping): a hint
        // under its button came with "As it is" and went with every change and live refit, and the buttons jumped
        float footKeep = -1f, footWidth = -1f, footScale = -1f;
        int footTab = -1, footChoice = -1, choices;

        /// <summary>The footer's least height: the most it has had since the tab, the sign or the window's width changed.</summary>
        GUILayoutOption[] FooterLeast()
        {
            if (Event.current.type == EventType.Layout && (tab != footTab || choices != footChoice ||
                                                           Mathf.Abs(rect.width - footWidth) > 0.5f || Widgets.Scale != footScale))
            {
                footTab = tab;
                footChoice = choices;
                footWidth = rect.width;
                footScale = Widgets.Scale;
                footKeep = 0f;
            }
            return footKeep > 0f ? new[] { GUILayout.MinHeight(footKeep) } : NoOptions;
        }
        static readonly GUILayoutOption[] NoOptions = new GUILayoutOption[0];

        /// <summary>A part's least height while steady: as tall as it was when the hold began, or taller.</summary>
        GUILayoutOption[] Steady(float least) => steady && least > 0f ? new[] { GUILayout.MinHeight(least) } : NoOptions;

        float viewHeight;

        /// <summary>The body's least height: as tall as it was when a hold began (Steady), and never shorter than what the
        /// scroll shows now (0.20.84): content that got shorter while scrolled to its end pulled the scroll back and moved
        /// everything (a hint at the bottom losing a line after a live refit); scrolled up, the room goes again. Never
        /// taller than the body was (WindowSizing.BodyLeast, 0.20.111: scrolling made room on and on).</summary>
        GUILayoutOption[] BodyLeast()
        {
            float least = WindowSizing.BodyLeast(scroll.y, viewHeight, bodyMin, steady);
            return least > 0f ? new[] { GUILayout.MinHeight(least) } : NoOptions;
        }

        /// <summary>The group just closed, measured at Repaint: its own height, or while steady the most it had.</summary>
        void Measure(ref float least)
        {
            if (Event.current.type != EventType.Repaint) return;
            float h = GUILayoutUtility.GetLastRect().height;
            least = steady ? Mathf.Max(least, h) : h;
        }

        string footNote, footNoteFor;

        /// <summary>The footer's line, made again only when the hotkey changes (0.20.91, review: every event).</summary>
        string FootNote()
        {
            string key = PluginConfig.MenuKeyText();
            if (footNote != null && ReferenceEquals(key, footNoteFor)) return footNote;
            footNoteFor = key;
            return footNote = (key.Length == 0 ? "Esc closes" : key + " or Esc closes") +
                              " · drag the title to move, an edge or a corner to resize";
        }

        /// <summary>A tab that shows nothing without a selected sign (2 Content, 3 Place).</summary>
        static bool NeedsSign(Tab t) => t is ContentTab || t is PlaceTab;

        void Draw(int id)
        {
            ResizeEdges();
            TitleBar();
            if (names == null)
            {
                names = new string[tabs.Length];
                for (int i = 0; i < tabs.Length; i++) names[i] = tabs[i].Title;
            }
            // 2 Content and 3 Place work on a selected sign: greyed and closed without one (0.20.73, the author: until a sign
            // is picked, the 2 Content and 3 Place tabs are grey and disabled; they cannot even be looked at)
            if (usable == null) usable = new bool[tabs.Length];
            for (int i = 0; i < tabs.Length; i++) usable[i] = Session.HasTarget || !NeedsSign(tabs[i]);
            if (Event.current.type == EventType.Layout && !usable[tab])
            {
                tab = Array.FindIndex(tabs, x => x is SignTab);             // the sign went (unloaded, deleted): back to 1 Sign
                scroll = Vector2.zero;
                Widgets.DropFocus();                                        // a field no longer drawn keeps no keyboard (0.20.130)
                SettingsTab.StopCapture();
            }
            int picked = Widgets.Tabs(tab, names, usable, Theme.Tab, Widgets.H(30));
            if (picked != tab) Later(() => { tab = picked; scroll = Vector2.zero; status = ""; showNearby = false; closeAfterPut = false; Widgets.DropFocus(); SettingsTab.StopCapture(); });   // messages belong to their tab; the list closes
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
                try { ghost.Update(Session, content.Current); }
                catch (Exception e) { Failed(e); }
                try { Plugin.Preview.Follow(Session, content.Current, Time.unscaledTime, Busy); }   // the result, before the button (0.20.129)
                catch (Exception e) { Failed(e); }
                try { veil.Follow(Session, Busy, Plugin.Preview.Showing); }   // the sign's own hides under it, or under a new pick
                catch (Exception e) { Failed(e); }
                if (ListHovering) ShowHover(listHover);                     // the row of Signs nearby under the mouse: its frame in the world
            }
            Tab t = tabs[tab];

            // while a control is held or typed in, the window's parts do not get shorter (0.20.75, the author: when I
            // tilt the picture and drag the slider the window jumps ... fix it everywhere): a shorter top moved the
            // body under the mouse, a shorter footer or body pulled the scroll back; let go, they take their own height
            if (Event.current.type == EventType.Layout) steady = GUIUtility.hotControl != 0 || GUIUtility.keyboardControl != 0;
            Widgets.Scope top = Widgets.Vertical(Steady(topMin));          // the parts above the body
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
                    if (Session.HasTarget && !PluginConfig.Highlight.Value)   // a selected sign without its frame says why
                        GUILayout.Label("Highlighting is disabled: Settings › Sign options › Highlight signs.", Theme.Notice);
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
            top.Dispose();
            Measure(ref topMin);
            Widgets.Space(4);
            scroll = GUILayout.BeginScrollView(scroll, false, false, GUIStyle.none, GUI.skin.verticalScrollbar, GUILayout.ExpandHeight(true));
            using (new Widgets.Scope(2))
            {
                using (Widgets.Vertical(BodyLeast()))
                {
                    Widgets.BodyView = viewHeight > 0 ? new Rect(-10000, scroll.y, 20000, viewHeight) : (Rect?)null;   // what shows of it
                    try { t.Body(); }
                    catch (Exception e) when (!(e is ExitGUIException))
                    {
                        Failed(e);
                    }
                    finally { Widgets.BodyView = null; }
                }
                Measure(ref bodyMin);
            }
            if (Event.current.type == EventType.Repaint) viewHeight = GUILayoutUtility.GetLastRect().height;   // the body's view

            using (Widgets.Vertical(Theme.Section, FooterLeast()))
            {
                try { t.Footer(); }
                catch (Exception e) when (!(e is ExitGUIException))
                {
                    Failed(e);
                }
                bool fresh = status.Length > 0 && Time.unscaledTime - statusAt < StatusSeconds;
                bool failed = status.StartsWith("error", StringComparison.Ordinal);
                GUILayout.Label(!fresh ? " " : failed ? "Could not: " + status.Substring(Math.Min(status.Length, 7)) : "Done: " + status,
                                failed ? Theme.Warn : Theme.Good);
            }
            if (Event.current.type == EventType.Repaint) footKeep = Mathf.Max(footKeep, GUILayoutUtility.GetLastRect().height);
            using (Widgets.Horizontal())
            {
                GUILayout.Label(FootNote(), Theme.Small);
                if (Widgets.Button("Close", Theme.Button, Widgets.W(90))) Close();
            }
            EdgeMarks();
            GUI.DragWindow(new Rect(0, 0, 10000, Widgets.U(TitleRule)));
            // a click on anything but the focused field (a button, a slider, the title, empty space) takes the keyboard
            // focus away: at a later Layout, once the mouse button is up (never during the click: Widgets.DropFocus)
            if (Event.current.rawType == EventType.MouseDown && GUIUtility.keyboardControl != 0 && GUIUtility.hotControl != GUIUtility.keyboardControl)
                Widgets.DropFocus();
        }

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

        void TargetBar()
        {
            using (Widgets.Horizontal(Theme.Section))
            {
                bool has = Session.HasTarget;
                if (has && Event.current.type == EventType.Layout && Time.unscaledTime - barAt > 0.5f)    // OnGUI runs several times a frame
                {
                    barAt = Time.unscaledTime;
                    barCount = Session.Stack.Count;
                    barPose = Mathf.Abs(Session.SignTilt) > 0.05f || Mathf.Abs(Session.SignTurn) > 0.05f
                        ? " · " + SignPose.Describe(Session.SignTilt, Session.SignTurn) : "";
                    barName = Session.Name;
                }
                bool view = has && Session.View.Has;                        // changes between frames only (Session)
                if (view)
                {
                    Session.View.DrawSmall(72, 40);
                    Widgets.Space(12);                                      // air between the picture and the rest
                }
                using (Widgets.Vertical())
                {
                    // the buttons first, the name under them (0.20.177, the author: the picture's name under the buttons,
                    // since a long one does not look nice): a long name wraps on a row of its own
                    using (Widgets.Horizontal())
                    {
                        // picking switches Show all off (0.20.88, the author: the pick buttons should not be grey, they
                        // switch Show all SignLink off by themselves)
                        if (Widgets.Button("Pick in view", Theme.Button, Widgets.W(110)))
                            Later(() =>
                            {
                                if (ShowAll.On) SetShowAll(false);
                                StartPicking();
                            });
                        // the selection dropped (0.20.173, the author: a Clear selection button beside Pick in view too):
                        // the sign as it is, steps not applied dropped as picking another sign drops them
                        bool was = GUI.enabled;
                        GUI.enabled = was && has;
                        if (Widgets.Button("Clear selection", Theme.Button, Widgets.W(140)))
                            Later(() =>
                            {
                                SelectSign(null);
                                Say("no sign selected.");
                            });
                        GUI.enabled = was;
                        bool list = Widgets.Toggle(showNearby, showNearby ? "Hide list" : "Signs nearby", Theme.Tab, Widgets.W(140));   // lit while the list shows; bold needs the room
                        if (list != showNearby)
                            Later(() =>
                            {
                                if (list && ShowAll.On) SetShowAll(false);
                                showNearby = list;
                                nearbyAt = -1f;
                            });
                        GUILayout.FlexibleSpace();
                    }
                    float nameW = Widgets.ContentWidth - 40 - (view ? 92 : 0);
                    if (has)
                        GUILayout.Label((barName.Length > 0 ? barName : "A sign") + $" · {Widgets.Distance(Target)}" + (barCount > 1 ? $" · {barCount} signs" : "") + barPose,
                                        Theme.Header, Widgets.W(nameW));
                    else GUILayout.Label(ShowAll.On ? "Every SignLink sign is shown. Picking one switches that off." :
                                         "No sign selected. Click a sign, or choose one from Signs nearby.", Theme.Warn, Widgets.W(nameW));
                }
            }
            // every SignLink sign framed in the world, the window open or shut, until switched off (0.20.87)
            using (Widgets.Horizontal())
            {
                GUILayout.Label("Show all SignLink signs", Theme.Label, Widgets.W(Widgets.LabelW));
                int at = ShowAll.On ? 0 : 1;
                int pick = Widgets.Toolbar(at, OnOff, Theme.Chip, Widgets.W(Mathf.Min(200f, Widgets.RestW - 20f)));
                GUILayout.FlexibleSpace();
                if (pick != at) Later(() => SetShowAll(pick == 0));
            }
            Widgets.Hint(ShowAll.On ? "Every SignLink sign around wears its frame, the window open or shut. Picking a sign switches it off."
                                    : "On: every SignLink sign around wears its frame, the window open or shut; no sign stays picked meanwhile.");
        }

        // the sign finder: every sign within reach, or the ones SignLink made (wherever this game knows them); stacks
        // as one row, with where they are from your view. It picks signs you cannot look at (built into a wall, behind
        // others, lying under a floor).
        bool showNearby;
        int listMode;
        // SignLink's signs within the multiplayer area's reach (0.20.103, the author: look for SignLink signs in the same
        // range our 600kb limit checker uses): the loaded ones anywhere and every unloaded one in the
        // world before
        static readonly string[] ListModes = { "Every sign within 30 m", $"SignLink signs within {SaveCost.AreaRadiusM} m" };
        float nearbyAt = -1f, farAt = -100f;
        Vector2 nearbyScroll;

        sealed class NearbyRow
        {
            public string Label, SelectedLabel;
            public Sign Sign;
            public int Order;                                               // SignLink's stacks, then the rest, empty signs last
            public float Distance;
        }

        readonly List<NearbyRow> nearby = new List<NearbyRow>();
        readonly List<string> far = new List<string>();
        const float NearbyRadius = 30f;

        /// <summary>The rows: stacks grouped in one pass (signs bucketed by the metre they stand in: a stack's signs lie
        /// within a few centimetres of each other, so a sign's stack-mates are in its own or a neighbouring cell; a base
        /// of 400 signs is not 400 x 400 distance tests).</summary>
        void RefreshNearby()
        {
            nearbyAt = Time.unscaledTime;
            nearby.Clear();
            Player p = Player.m_localPlayer;
            if (!p) return;
            bool mine = listMode == 1;
            float radius = mine ? SaveCost.AreaRadiusM : NearbyRadius;
            Vector3 me = p.transform.position;
            Transform eye = GameCamera.instance ? GameCamera.instance.transform : p.transform;
            Vector3 fwd = Vector3.ProjectOnPlane(eye.forward, Vector3.up).normalized, right = Vector3.Cross(Vector3.up, fwd);
            var all = new List<Sign>();
            foreach (Sign s in SignFinder.Loaded())                       // once per refresh: the expensive part
                if (s && SignFinder.Zdo(s) != null && Vector3.Distance(s.transform.position, me) <= radius + 1f) all.Add(s);
            all.Sort((a, b) => Vector3.Distance(a.transform.position, me).CompareTo(Vector3.Distance(b.transform.position, me)));
            foreach (List<Sign> stack in SignFinder.Stacks(all))           // stacks in one pass (SignFinder.Stacks)
            {
                Sign first = stack[0];
                float dist = Vector3.Distance(first.transform.position, me);
                if (dist > radius) continue;
                bool ours = SignMarks.IsSignLinks(stack);
                if (mine && !ours) continue;
                // from the character, as the bar's "5 m away" (0.20.193, test: the rows measured from the camera), the
                // way as the view looks; up and down from the eyes
                Vector3 d = first.transform.position - me;
                float ahead = Vector3.Dot(d, fwd), side = Vector3.Dot(d, right), up = first.transform.position.y - eye.position.y;
                int bytes = 0;
                foreach (Sign o in stack) bytes += SignText.TextBytes(o);
                // what the world save holds, as 1 Sign counts it: each sign's own share and a saved animation too (0.20.191,
                // test: a moving cat read 2.9 KB here, 4.2 KB on 1 Sign)
                byte[] blob = Animation.StoredAnims.Blob(first);
                long save = bytes == 0 ? 0 : bytes + (long)SaveCost.PerSign * stack.Count + (blob != null && blob.Length > 0 ? blob.Length + 8 : 0);
                string where = (ahead >= 0 ? $"{ahead:0.0} m ahead" : $"{-ahead:0.0} m behind") + " · " +
                               (side >= 0 ? $"{side:0.0} m right" : $"{-side:0.0} m left") + " · " +
                               (up >= 0 ? $"{up:0.0} m up" : $"{-up:0.0} m down");
                nearby.Add(new NearbyRow
                {
                    Label = $"{What(stack, first, bytes, save)}   <size={Theme.SmallSize}><color=#b3ab99>{where}</color></size>",
                    // on the selected row's amber a dark brown, not the grey (0.20.191, test: almost unreadable)
                    SelectedLabel = $"{What(stack, first, bytes, save)}   <size={Theme.SmallSize}><color=#4a3c22>{where}</color></size>",
                    Sign = first, Order = ours ? 0 : bytes > 0 ? 1 : 2, Distance = dist,
                });
            }
            nearby.Sort((a, b) => a.Order != b.Order ? a.Order.CompareTo(b.Order) : a.Distance.CompareTo(b.Distance));
        }

        /// <summary>A row's description, leading with what it is: "starry_night.jpg · 7 signs · 2.3 MB · lying face up";
        /// a plain sign's first words (not when the game hides its text: a blocked or muted author, user content off;
        /// 0.20.132, review), or "empty".</summary>
        static string What(List<Sign> stack, Sign first, int bytes, long save)
        {
            string name = Session.ShownName(first);
            bool plain = name.Length == 0 && stack.Count == 1 && bytes > 0 && bytes < 400;
            string words = plain && first.m_isViewable ? SignRecipe.Clean(TextReadback.Words(new[] { SignText.SavedText(first) }, 28)) : "";
            var parts = new List<string>();
            if (bytes == 0) parts.Add("empty");
            else
            {
                parts.Add(name.Length > 0 ? name : plain && !first.m_isViewable ? Session.HiddenWords : words.Length > 0 ? $"\"{words}\"" :
                          stack.Count > 1 ? "a picture" : "a sign");
                if (stack.Count > 1) parts.Add($"{stack.Count} signs");
                parts.Add(SaveCost.Label(save));
            }
            SignPose.Read(first, out float tilt, out float turn);
            if (Mathf.Abs(tilt) > 0.05f || Mathf.Abs(turn) > 0.05f) parts.Add(SignPose.Describe(tilt, turn));
            return string.Join(" · ", parts.ToArray());
        }

        // the far list reads the zones within the area's reach only (0.20.132, review: it walked the whole world's sign ZDOs,
        // up to 401 sectors a Layout for 10-20 frames every 10 s, and the game's walk reads the sector it pauses at twice,
        // ZDOMan.GetAllZDOsWithPrefabIterative stopping before its index moves on, so that sector's signs showed as "2 signs")
        static readonly int SignPrefab = "sign".GetStableHashCode();
        readonly List<ZDO> farZone = new List<ZDO>(), farSigns = new List<ZDO>();

        /// <summary>SignLink's signs this game knows but has not loaded within SaveCost.AreaRadiusM (in single player and
        /// on the host: all of them), copies of a stack as one, nearest first; they can be selected once you are near. The
        /// zones around the player's own, as many on every side as the reach needs from anywhere in it (64 m zones: two for
        /// 100 m), every 10 s while the list shows; the rows carry their indent.</summary>
        void WalkFar(Vector3 me)
        {
            if (ZDOMan.instance == null || !ZNetScene.instance || Time.unscaledTime - farAt < 10f) return;
            farAt = Time.unscaledTime;
            far.Clear();
            farSigns.Clear();
            Vector2s home = ZoneSystem.GetZone(me);
            int around = Mathf.FloorToInt(SaveCost.AreaRadiusM / 64f) + 1;   // a zone's half (32 m) on both sides of the player
            for (int dx = -around; dx <= around; dx++)
                for (int dy = -around; dy <= around; dy++)
                {
                    farZone.Clear();
                    ZDOMan.instance.FindSectorObjects(new Vector2s(home.x + dx, home.y + dy), new SimulationDistance(0, 0), farZone);
                    foreach (ZDO z in farZone)
                        if (z.GetPrefab() == SignPrefab) farSigns.Add(z);
                }
            farZone.Clear();
            var groups = new List<KeyValuePair<ZDO, int>>();
            var cells = new Dictionary<Vector3Int, List<int>>();            // 30 cm cells: a stack is looked for next door only
            foreach (ZDO z in farSigns)
            {
                if (ZNetScene.instance.FindInstance(z) != null || !SignMarks.IsSignLinks(z)) continue;
                Vector3 p = z.GetPosition();
                if ((p - me).sqrMagnitude > SaveCost.AreaRadiusM * (float)SaveCost.AreaRadiusM) continue;   // the area's reach only
                Vector3Int cell = Vector3Int.FloorToInt(p / 0.3f);
                int g = -1;
                for (int i = 0; i < 27 && g < 0; i++)
                {
                    if (!cells.TryGetValue(cell + new Vector3Int(i % 3 - 1, i / 3 % 3 - 1, i / 9 - 1), out List<int> near)) continue;
                    foreach (int at in near)
                        if (Vector3.Distance(groups[at].Key.GetPosition(), p) < 0.3f &&
                            Quaternion.Angle(groups[at].Key.GetRotation(), z.GetRotation()) < 1f) { g = at; break; }
                }
                if (g < 0)
                {
                    groups.Add(new KeyValuePair<ZDO, int>(z, 1));
                    if (!cells.TryGetValue(cell, out List<int> here)) cells[cell] = here = new List<int>();
                    here.Add(groups.Count - 1);
                }
                else
                {
                    ZDO keep = groups[g].Key.GetString(SignMarks.NameKey, "").Length > 0 ? groups[g].Key : z;
                    groups[g] = new KeyValuePair<ZDO, int>(keep, groups[g].Value + 1);
                }
            }
            farSigns.Clear();
            groups.Sort((a, b) => Vector3.Distance(a.Key.GetPosition(), me).CompareTo(Vector3.Distance(b.Key.GetPosition(), me)));
            foreach (KeyValuePair<ZDO, int> e in groups)
            {
                if (far.Count >= 60) break;
                Vector3 d = e.Key.GetPosition() - me;
                string name = SignRecipe.Clean(e.Key.GetString(SignMarks.NameKey, ""));
                far.Add($"   {(name.Length > 0 ? name : "a sign")} · {(e.Value > 1 ? $"{e.Value} signs" : "1 sign")} · " +
                        $"{new Vector2(d.x, d.z).magnitude:0} m {Compass(d)}");
            }
        }

        static string Compass(Vector3 d)
        {
            string[] names = { "north", "north-east", "east", "south-east", "south", "south-west", "west", "north-west" };
            float a = Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg;                // the map's up is +z
            return names[(Mathf.RoundToInt(a / 45f) % 8 + 8) % 8];
        }

        void NearbyPanel()
        {
            int mode = Widgets.Toolbar(listMode, ListModes, Theme.Tab, Widgets.W(Widgets.ContentWidth - 30));
            if (mode != listMode) Later(() => { listMode = mode; nearbyAt = -1f; farAt = -100f; });
            if (Event.current.type == EventType.Layout)                     // rows change only between frames
            {
                if (Time.unscaledTime - nearbyAt > 1f) RefreshNearby();
                if (listMode == 1 && Player.m_localPlayer) WalkFar(Player.m_localPlayer.transform.position);
            }
            using (Widgets.Scroll(ref nearbyScroll, 170))
            {
                if (nearby.Count == 0)
                    Widgets.Hint(listMode == 1 ? $"No sign SignLink made within {SaveCost.AreaRadiusM} m." : $"No signs within {NearbyRadius:0} m: build one with the hammer, then Pick in view.");
                GUILayoutOption[] row = { Widgets.W(Widgets.ContentWidth - 30) };   // one for every row (0.20.132, review)
                float shownTo = nearbyScroll.y + Widgets.U(Widgets.Tall(170));
                foreach (NearbyRow e in nearby)
                {
                    bool selected = ReferenceEquals(e.Sign, Target);
                    if (GUILayout.Toggle(selected, selected ? e.SelectedLabel : e.Label, Theme.ItemRich, row) != selected)
                    {
                        Sign s = e.Sign;
                        Later(() =>
                        {
                            if (!ReferenceEquals(s, Target))
                            {
                                KeepOpen();                                 // the player's own pick: no put closes it now
                                SelectSign(s);
                                Say($"selected {Called(s)}.");
                            }
                            showNearby = false;                             // chosen (or chosen again): the list has done its job
                        });
                    }
                    Vector2 mouse = Event.current.mousePosition;              // a row scrolled out of the box is not under it
                    if (Event.current.type == EventType.Repaint && e.Sign && GUILayoutUtility.GetLastRect().Contains(mouse) &&
                        mouse.y >= nearbyScroll.y && mouse.y <= shownTo)
                    {
                        listHover = e.Sign;                                 // its frame shows in the world (Draw, next Layout)
                        listHoverAt = Time.frameCount;
                    }
                }
                if (listMode == 1 && far.Count > 0)                         // read at once (WalkFar): no "looking" line
                {
                    Widgets.Hint("Not loaded yet (walk closer to select them):");
                    foreach (string f in far) Widgets.Hint(f);              // indented as made
                }
            }
        }

        float barAt = -1f;
        int barCount;
        string barPose = "", barName = "";

        /// <summary>What SignLink put on a sign, or "the sign" (players do not know ZDO ids).</summary>
        static string Called(Sign s)
        {
            string name = Session.ShownName(s);
            return name.Length > 0 ? $"the sign with {name}" : "the sign";
        }

        public bool HasTarget => Session.HasTarget;

        /// <summary>Text sizes (Settings). 2 went and 0.6 and 0.7 came (0.20.120, the author: text size 1.5 and 2 look the
        /// same to me; take 2 out and add one even smaller than 0.8): the size times the screen's height over
        /// 1080 stops at 3 (Layout), so on a 4K screen 1.5 is 3 already and 2 was the same.</summary>
        public static readonly float[] Scales = { 0.6f, 0.7f, 0.8f, 0.9f, 1f, 1.15f, 1.3f, 1.5f };
    }
}
