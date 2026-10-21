using System;
using System.Collections.Generic;
using BepInEx.Configuration;
using SignLink.Pictures;
using SignLink.Signs;
using UnityEngine;
using PetyusPolisz.Kit;

namespace SignLink.UI
{
    /// <summary>The sign bar on 1 Sign (the selected sign, Select sign, Signs nearby, Clear selection) and the nearby list.</summary>
    internal partial class SignLinkWindow
    {
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
                    // the whole line once here, not on every event (1.1.0, review); its words whole for the translations
                    string name = barName.Length > 0 ? barName : Lang.T("A sign");
                    barLine = (barCount > 1 ? Lang.F("{0} · {1} · {2} signs", name, SignWords.Distance(Target), barCount)
                                            : Lang.F("{0} · {1}", name, SignWords.Distance(Target))) + barPose;
                }
                // the small view's place is kept while the view loads, so the buttons beside it stay put (1.1.0, the author:
                // nothing should jump when it goes and comes back)
                if (has)
                {
                    if (Session.View.Has) Session.View.DrawSmall(72, 40);   // changes between frames only (Session)
                    else Thumb.DrawEmpty("", 72, 40);
                    Widgets.Space(12);                                      // air between the picture and the rest
                }
                using (Widgets.Vertical())
                {
                    // the buttons first, the name under them (0.20.177, the author: the picture's name under the buttons,
                    // since a long one does not look nice): a long name wraps on a row of its own
                    using (Widgets.Horizontal())
                    {
                        // picking switches Show all off (0.20.88, the author: the pick buttons should not be grey, they
                        // switch Show all SignLink off by themselves); "Select sign" (1.1.0, the author: was Pick in view)
                        if (Widgets.Button(Lang.T("Select sign"), Theme.Button, Widgets.W(120)))
                            Later(() =>
                            {
                                if (ShowAll.On) SetShowAll(false);
                                StartPicking();
                            });
                        // the same grey button as Select sign (1.1.0, the author), its words saying which way it goes (a button, not a
                        // toggle: the look gives a button no "on" face, and the skin's bright one showed)
                        bool list = showNearby;
                        if (Widgets.Button(showNearby ? Lang.T("Hide list") : Lang.T("Signs nearby"), Theme.Button, Widgets.W(140))) list = !showNearby;
                        if (list != showNearby)
                            Later(() =>
                            {
                                if (list && ShowAll.On) SetShowAll(false);
                                showNearby = list;
                                nearbyAt = -1f;
                            });
                        if (showNearby) Widgets.KeysHere(Theme.Button, UpDownKeys);             // the arrows step the list (1.1.0): shown while it is open
                        GUILayout.FlexibleSpace();
                        // the selection dropped (0.20.173, the author: a Clear selection button too), at the row's right end
                        // in the tabs' look (1.1.0, the author): the sign as it is, steps not applied dropped as picking another
                        // sign drops them
                        bool was = GUI.enabled;
                        GUI.enabled = was && has;
                        if (Widgets.TabButton(Lang.T("Clear selection"), Widgets.W(150)))
                            Later(() =>
                            {
                                SelectSign(null);
                                Say(Lang.T("no sign selected."));
                            });
                        GUI.enabled = was;
                    }
                    float nameW = Widgets.ContentWidth - 40 - (has ? 92 : 0);
                    if (has)
                        GUILayout.Label(barLine,
                                        Theme.Lead, Widgets.W(nameW));
                    // asks for a sign, not an error (1.1.0, the author: "No sign" → ask the player to choose the sign to work on)
                    else GUILayout.Label(Lang.T("Select a sign to work on"), Theme.Notice, Widgets.W(nameW));
                }
            }
        }

        /// <summary>Show all SignLink signs, a check box in the Close row on every tab (1.1.0, the author: a check box by
        /// Close, not an On / Off row under the sign bar): every SignLink sign framed in the world, the window open or shut,
        /// until switched off (0.20.87).</summary>
        void ShowAllBox()
        {
            // nothing to frame with: grey and off while both highlights are off (1.1.0, the author), and switched off if it was on
            bool can = PluginConfig.Highlight.Value || PluginConfig.HighlightContent.Value, was = GUI.enabled;
            if (!can && ShowAll.On && Event.current.type == EventType.Layout) Later(() => SetShowAll(false));
            GUI.enabled = was && can;
            bool show = Widgets.Check(can && ShowAll.On, Lang.T("Show all SignLink signs"));
            GUI.enabled = was;
            if (can && show != ShowAll.On) Later(() => SetShowAll(show));
        }

        // the sign finder: every sign within reach, or the ones SignLink made (wherever this game knows them); stacks
        // as one row, with where they are from your view. It picks signs you cannot look at (built into a wall, behind
        // others, lying under a floor).
        bool showNearby;
        int listMode;
        // SignLink's signs within the multiplayer area's reach (0.20.103, the author: look for SignLink signs in the same
        // range our 600kb limit checker uses): the loaded ones anywhere and every unloaded one in the
        // world before
        static string[] listModes;
        static string listModesFor;

        /// <summary>The list's two modes in the language in use, made again when it changes.</summary>
        static string[] ListModes
        {
            get
            {
                string lang = Lang.Current;
                if (listModes == null || listModesFor != lang)
                {
                    listModes = new[] { Lang.T("Every sign within 30 m"), Lang.F("SignLink signs within {0} m", SaveCost.AreaRadiusM) };
                    listModesFor = lang;
                }
                return listModes;
            }
        }
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
                string where = (ahead >= 0 ? Lang.F("{0:0.0} m ahead", ahead) : Lang.F("{0:0.0} m behind", -ahead)) + " · " +
                               (side >= 0 ? Lang.F("{0:0.0} m right", side) : Lang.F("{0:0.0} m left", -side)) + " · " +
                               (up >= 0 ? Lang.F("{0:0.0} m up", up) : Lang.F("{0:0.0} m down", -up));
                nearby.Add(new NearbyRow
                {
                    Label = $"{What(stack, first, bytes, save)}   <size={Theme.SmallSize}><color={Theme.DimHex}>{where}</color></size>",
                    // on the selected row a colour of its own (Classic: a dark brown on the amber, not the grey: 0.20.191, test: almost
                    // unreadable; Nordic: a light grey on the slate)
                    SelectedLabel = $"{What(stack, first, bytes, save)}   <size={Theme.SmallSize}><color={Theme.DimOnHex}>{where}</color></size>",
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
            if (bytes == 0) parts.Add(Lang.T("empty"));
            else
            {
                parts.Add(name.Length > 0 ? name : plain && !first.m_isViewable ? Session.HiddenWords : words.Length > 0 ? $"\"{words}\"" :
                          stack.Count > 1 ? Lang.T("a picture") : Lang.T("a sign"));
                if (stack.Count > 1) parts.Add(Lang.F("{0} signs", stack.Count));
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
                far.Add($"   {(name.Length > 0 ? name : Lang.T("a sign"))} · {(e.Value > 1 ? Lang.F("{0} signs", e.Value) : Lang.T("1 sign"))} · " +
                        Compass(d));
            }
        }

        /// <summary>How far and which way d lies on the ground, a whole phrase each way ("12 m north"), for the translations
        /// (each key written out: signart/lang_keys.py finds literal keys only).</summary>
        static string Compass(Vector3 d)
        {
            float m = new Vector2(d.x, d.z).magnitude;
            float a = Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg;                // the map's up is +z
            switch ((Mathf.RoundToInt(a / 45f) % 8 + 8) % 8)
            {
                case 0: return Lang.F("{0:0} m north", m);
                case 1: return Lang.F("{0:0} m north-east", m);
                case 2: return Lang.F("{0:0} m east", m);
                case 3: return Lang.F("{0:0} m south-east", m);
                case 4: return Lang.F("{0:0} m south", m);
                case 5: return Lang.F("{0:0} m south-west", m);
                case 6: return Lang.F("{0:0} m west", m);
                default: return Lang.F("{0:0} m north-west", m);
            }
        }

        void NearbyPanel()
        {
            int mode = Widgets.Toolbar(listMode, ListModes, Theme.Tab, Widgets.W(Widgets.ContentWidth - 30));
            if (mode != listMode) Later(() => { listMode = mode; nearbyAt = -1f; farAt = -100f; });
            if (Event.current.type == EventType.Layout)                     // rows change only between frames
            {
                if (Time.unscaledTime - nearbyAt > 1f && nearbyReveal < 0) RefreshNearby();   // not under an arrow's reveal: its row
                                                                            // is where the last Repaint saw it (1.1.0, review)
                if (listMode == 1 && Player.m_localPlayer) WalkFar(Player.m_localPlayer.transform.position);
            }
            float viewH = Widgets.U(Widgets.Tall(170));
            if (Event.current.type == EventType.Layout && nearbyReveal >= 0 && nearbyReveal < nearbyTops.Count)
            {                                                               // an arrow's pick: scrolled into view
                float top = nearbyTops[nearbyReveal], bottom = nearbyBottoms[nearbyReveal];
                if (top < nearbyScroll.y) nearbyScroll.y = top;
                else if (bottom > nearbyScroll.y + viewH) nearbyScroll.y = bottom - viewH;
                nearbyReveal = -1;
            }
            using (Widgets.Scroll(ref nearbyScroll, 170))
            {
                if (nearby.Count == 0)
                    Widgets.Hint(Lang.F("None within {0} m", listMode == 1 ? SaveCost.AreaRadiusM : (int)NearbyRadius));
                GUILayoutOption[] row = { Widgets.W(Widgets.ContentWidth - 30) };   // one for every row (0.20.132, review)
                float shownTo = nearbyScroll.y + viewH;
                bool repaint = Event.current.type == EventType.Repaint;
                if (repaint)
                {
                    nearbyTops.Clear();
                    nearbyBottoms.Clear();
                }
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
                                Say(Selected(s));
                            }
                            showNearby = false;                             // chosen (or chosen again): the list has done its job
                        });
                    }
                    Vector2 mouse = Event.current.mousePosition;              // a row scrolled out of the box is not under it
                    if (repaint)
                    {
                        Rect r = GUILayoutUtility.GetLastRect();
                        nearbyTops.Add(r.y);                                // where it lies, for the arrows (NearbyKeys)
                        nearbyBottoms.Add(r.yMax);
                        if (e.Sign && r.Contains(mouse) && mouse.y >= nearbyScroll.y && mouse.y <= shownTo)
                        {
                            listHover = e.Sign;                             // its frame shows in the world (Draw, next Layout)
                            listHoverAt = Time.frameCount;
                        }
                    }
                }
                if (listMode == 1 && far.Count > 0)                         // read at once (WalkFar): no "looking" line
                {
                    Widgets.Hint(Lang.T("Too far:"));
                    foreach (string f in far) Widgets.Hint(f);              // indented as made
                }
            }
        }

        // the arrows on the list (1.1.0, the author: Signs nearby is a list too, the up and down arrows should work there as
        // on Art's and Picture's): where each row lay at the last Repaint, the row to scroll into view, the held arrow
        readonly List<float> nearbyTops = new List<float>(), nearbyBottoms = new List<float>();
        int nearbyReveal = -1;
        KeyCode nearbyHeld;
        float nearbyRepeatAt;

        /// <summary>While Signs nearby shows (1 Sign): up and down select the row above or below, the list stays open, the
        /// row scrolls into view; a held arrow repeats as on 2 Content's lists. Not while a field, the chat or the console
        /// takes the keys.</summary>
        void NearbyKeys()
        {
            if (GUIUtility.keyboardControl != 0 || Console.IsVisible() || (Chat.instance && Chat.instance.HasFocus()) || nearby.Count == 0)
            {
                nearbyHeld = KeyCode.None;
                return;
            }
            KeyCode k = Input.GetKeyDown(KeyCode.UpArrow) ? KeyCode.UpArrow : Input.GetKeyDown(KeyCode.DownArrow) ? KeyCode.DownArrow : KeyCode.None;
            if (k != KeyCode.None)
            {
                nearbyHeld = k;
                nearbyRepeatAt = Time.unscaledTime + 0.4f;
            }
            else if (nearbyHeld != KeyCode.None && Input.GetKey(nearbyHeld) && Time.unscaledTime >= nearbyRepeatAt)
            {
                k = nearbyHeld;
                nearbyRepeatAt = Time.unscaledTime + 0.12f;
            }
            else if (nearbyHeld != KeyCode.None && !Input.GetKey(nearbyHeld)) nearbyHeld = KeyCode.None;
            if (k != KeyCode.None) NearbyStep(k == KeyCode.UpArrow ? -1 : 1);
        }

        /// <summary>The row `step` rows below (above: negative) the selected one is selected; none selected: the first row
        /// (down) or the last (up).</summary>
        void NearbyStep(int step)
        {
            if (nearby.Count == 0) return;
            int at = nearby.FindIndex(e => ReferenceEquals(e.Sign, Target));
            int next = at < 0 ? (step > 0 ? 0 : nearby.Count - 1) : Mathf.Clamp(at + step, 0, nearby.Count - 1);
            Sign s = nearby[next].Sign;
            if (next == at || !s) return;
            Later(() =>
            {
                KeepOpen();                                                 // the player's own pick: no put closes it now
                SelectSign(s);
                showNearby = true;                                          // a pick by the arrows: the list stays for the next one
                nearbyReveal = next;
                Say(Selected(s));
            });
        }

        float barAt = -1f;
        int barCount;
        string barPose = "", barName = "", barLine = "";
        static readonly string[] UpDownKeys = { "↑ ↓" };                   // Signs nearby's keys, one array for every event

        /// <summary>The status line after a pick, naming what SignLink put on the sign, or "the sign" (players do not know
        /// ZDO ids); a whole sentence each way, for the translations.</summary>
        static string Selected(Sign s)
        {
            string name = Session.ShownName(s);
            return name.Length > 0 ? Lang.F("selected the sign with {0}.", name) : Lang.T("selected the sign.");
        }
    }
}
