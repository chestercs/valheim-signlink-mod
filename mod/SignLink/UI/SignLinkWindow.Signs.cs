using System;
using System.Collections.Generic;
using BepInEx.Configuration;
using SignLink.Pictures;
using SignLink.Signs;
using UnityEngine;
using PetyusPolisz.Kit;

namespace SignLink.UI
{
    /// <summary>The sign bar on 1 Sign (the selected sign, Pick in view, Signs nearby, Show all) and the nearby list.</summary>
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
                        GUILayout.Label((barName.Length > 0 ? barName : "A sign") + $" · {SignWords.Distance(Target)}" + (barCount > 1 ? $" · {barCount} signs" : "") + barPose,
                                        Theme.Header, Widgets.W(nameW));
                    else GUILayout.Label(ShowAll.On ? "All shown" : "No sign", Theme.Warn, Widgets.W(nameW));
                }
            }
            // every SignLink sign framed in the world, the window open or shut, until switched off (0.20.87)
            bool show = Widgets.OnOff("Show all SignLink signs", ShowAll.On);
            if (show != ShowAll.On) Later(() => SetShowAll(show));
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
                    Widgets.Hint(listMode == 1 ? $"None within {SaveCost.AreaRadiusM} m" : $"None within {NearbyRadius:0} m");
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
                    Widgets.Hint("Too far:");
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
    }
}
