using System;
using System.Collections.Generic;
using UnityEngine;

namespace PetyusPolisz.Kit
{
    /// <summary>What a window reads from its mod's settings (its config file, or a preview while a slider is held), and
    /// where it reports a resize. Shares of the screen: width 0 is automatic (WindowSizing).</summary>
    internal interface IWindowLook
    {
        /// <summary>The text scale the player chose (1: as designed at 1080p).</summary>
        float TextScale { get; }
        float WidthShare { get; }
        float HeightShare { get; }
        /// <summary>How solid the window is, 0.5 .. 1 (the text stays opaque).</summary>
        float Opacity { get; }
        /// <summary>The look: Theme.ClassicName or Theme.NordicName (1.1.0).</summary>
        string ThemeName { get; }
        /// <summary>Where the window was dragged to, screen pixels; negative: the default place.</summary>
        float X { get; set; }
        float Y { get; set; }
        /// <summary>An edge is dragged: the new size, shown at once (not kept yet).</summary>
        void Resizing(float widthShare, float heightShare);
        /// <summary>The edge was let go: the size is kept.</summary>
        void Resized();
    }

    /// <summary>The frame of a mod's window (IMGUI, GUILayout.Window), whatever it shows: its size from the settings and the
    /// screen (WindowSizing), the text scale and the look (Theme.Ensure), the place it was dragged to (kept), every edge
    /// and corner resizing it, the title rule, the body's scroll and the parts that stay as tall as they were while a
    /// control is held, the close row with its keys, and clicks' work run at a later Layout (IDeferred). The mod's window
    /// (a MonoBehaviour) owns one, calls Layout and Show from OnGUI, and draws its own rows between BeginDraw and
    /// EndDraw. Lifted from SignLink's window (1.0.2) as it was; its whys are SignLink's.</summary>
    internal sealed class KitWindow : IDeferred
    {
        readonly int id;
        readonly IWindowLook look;
        readonly Action<string> logInfo, logWarning;
        Rect rect = new Rect(60, 60, WindowSizing.MinWidth, 640);         // screen pixels

        public KitWindow(int id, IWindowLook look, Action<string> logInfo, Action<string> logWarning)
        {
            this.id = id;
            this.look = look;
            this.logInfo = logInfo ?? (_ => { });
            this.logWarning = logWarning ?? (_ => { });
        }

        /// <summary>The window's size on screen now, in pixels.</summary>
        public Vector2 Size => rect.size;

        /// <summary>The window's width now, in pixels (a title laid out across it).</summary>
        public float Width => rect.width;

        // ---------------------------------------------------------------- clicks' work at a later Layout

        /// <summary>State changes from clicks wait for the first Layout event of a later frame: IMGUI draws every event
        /// twice (Layout, then the event), and controls that appear or vanish in between log "Getting control's position"
        /// errors; and the frame the click was in still draws.</summary>
        readonly List<Action> later = new List<Action>();
        readonly List<int> laterFrames = new List<int>();

        public void Later(Action a)
        {
            later.Add(a);
            laterFrames.Add(Time.frameCount);
        }

        /// <summary>Is anything queued and not run yet?</summary>
        public bool Waiting => later.Count > 0;

        /// <summary>Runs what was queued in earlier frames (Layout events), or all of it (closing: a click in the same frame
        /// as Esc runs now). failed: what one of them threw.</summary>
        public void RunLater(bool all, Action<Exception> failed)
        {
            if (later.Count == 0) return;                                   // nothing queued: no list made (0.20.132, review)
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
                catch (Exception e) { failed?.Invoke(e); }
            }
        }

        /// <summary>Everything queued runs, also what the queued work queues (0.20.130), then the queue is empty.</summary>
        public void RunAll(Action<Exception> failed)
        {
            for (int round = 0; round < 8 && later.Count > 0; round++) RunLater(true, failed);
            later.Clear();
            laterFrames.Clear();
        }

        // ---------------------------------------------------------------- size, place, look

        bool placed, logOpen, overSaid;
        float drawnWidth, drawnHeight;                                      // as GUILayout drew it last (a wide row widens it)
        float savedX, savedY;

        /// <summary>The window opens: Layout logs once what it was built for, and the drawn size starts again.</summary>
        public void Opened() => logOpen = true;

        /// <summary>The text scale and the window's size, in screen pixels: the window takes its share of the screen (its
        /// width and height, set on the mod's settings or by its edges and corners: WindowSizing), up to the whole screen,
        /// the text its own scale; a window too narrow for the rows at that text size grows, and on a screen too small for
        /// both the text shrinks. The styles are built for the scale, and rows and lists follow (Widgets.Scale,
        /// ContentWidth, HeightFactor). The place it was dragged to comes from the settings once.</summary>
        public void Layout()
        {
            float scale = Mathf.Clamp(Screen.height / 1080f * look.TextScale, 0.5f, 3f);
            WindowSizing.Size(Screen.width, Screen.height, look.WidthShare, look.HeightShare, scale, out float w, out float h, out float s);
            rect.width = w;
            rect.height = h;
            if (resizing) rect.position = dragPlace;                        // a left or top edge held: the place follows it
            Widgets.Scale = s;
            Widgets.ContentWidth = w / s - 64f;
            Widgets.HeightFactor = Mathf.Clamp(h / s / 760f, 1f, 1.6f);
            Theme.Ensure(s, look.Opacity, look.ThemeName);
            if (!placed)
            {
                placed = true;
                if (look.X >= 0f) rect.x = look.X;
                if (look.Y >= 0f) rect.y = look.Y;
                savedX = rect.x;
                savedY = rect.y;
            }
            if (logOpen)
            {
                logOpen = false;                                            // one line per open: what the window was built for
                overSaid = false;
                drawnWidth = drawnHeight = 0f;
                logInfo($"window: open at text scale {s:0.00}, {w:0} x {h:0} px at {rect.x:0},{rect.y:0} on {Screen.width} x {Screen.height}, {look.ThemeName} look, fonts: {Theme.Faces}" +
                        (Theme.LookError != null ? $", flat: the look failed ({Theme.LookError})" : ""));
            }
        }

        /// <summary>Draws the window (GUILayout.Window) kept on the screen as it was drawn: a row wider than the window widens
        /// it (GUILayout), and clamped by its own width it stood past the screen's edge (0.20.98). wideWhere: what to name
        /// in the log when a row is wider than the window (once an open). A drag that ended is kept (the settings' X, Y).</summary>
        public void Show(GUI.WindowFunction draw, Func<string> wideWhere = null)
        {
            rect.x = Mathf.Clamp(rect.x, 0, Mathf.Max(0, Screen.width - Mathf.Max(rect.width, drawnWidth)));
            rect.y = Mathf.Clamp(rect.y, 0, Mathf.Max(0, Screen.height - Mathf.Max(rect.height, drawnHeight)));
            float wantWidth = rect.width;
            // the window function draws with the skin current here (the look's own scrollbar, Theme.Skin): set for this call only
            GUISkin was = GUI.skin;
            if (Theme.Skin) GUI.skin = Theme.Skin;
            try { rect = GUILayout.Window(id, rect, draw, "", Theme.Window, GUILayout.Width(rect.width), GUILayout.Height(rect.height)); }
            finally { GUI.skin = was; }
            if (Event.current.type == EventType.Repaint)
            {
                drawnWidth = rect.width;
                drawnHeight = rect.height;
                if (rect.width > wantWidth + 1f && !overSaid)               // once a window: which part has the wide row
                {
                    overSaid = true;
                    logWarning($"window: drawn {rect.width:0} px wide, {rect.width - wantWidth:0} px over its width " +
                               $"(a row on {wideWhere?.Invoke() ?? "it"} is wider than the window)");
                }
            }
            if (Event.current.type == EventType.Repaint && GUIUtility.hotControl == 0 &&
                (Mathf.Abs(rect.x - savedX) > 0.5f || Mathf.Abs(rect.y - savedY) > 0.5f))
                SavePlace();                                                // a drag ended: the place is kept
        }

        /// <summary>Is this screen point (from the top left) over the window as it was drawn last (its rows may widen it)?</summary>
        public bool Covers(Vector2 at) =>
            new Rect(rect.x, rect.y, Mathf.Max(rect.width, drawnWidth), Mathf.Max(rect.height, drawnHeight)).Contains(at);

        void SavePlace()
        {
            savedX = rect.x;
            savedY = rect.y;
            look.X = Mathf.Round(rect.x);
            look.Y = Mathf.Round(rect.y);
        }

        // ---------------------------------------------------------------- inside the window function

        /// <summary>First in the window function: an open drop-down list takes its clicks, then a press on an edge is the
        /// resize's before a row takes it.</summary>
        public void BeginDraw()
        {
            Widgets.DropInput();                                            // an open drop-down list takes its clicks first
            ResizeEdges();
        }

        /// <summary>Last in the window function: the edges' marks, the title as the handle that moves it, and a click on
        /// anything but the focused field takes the keyboard focus away (at a later Layout: Widgets.DropFocus).</summary>
        public void EndDraw()
        {
            EdgeMarks();
            Widgets.DropList(rect.height);                                  // an open drop-down list, over everything
            GUI.DragWindow(new Rect(0, 0, 10000, Widgets.U(TitleRule)));
            if (Event.current.rawType == EventType.MouseDown && GUIUtility.keyboardControl != 0 && GUIUtility.hotControl != GUIUtility.keyboardControl)
                Widgets.DropFocus();
        }

        /// <summary>Where the rule under the title runs (logical units from the window's top; Theme.Window's padding starts
        /// the rows a little under it).</summary>
        public const float TitleRule = 37f;

        /// <summary>The title line: the name on the left with the version smaller beside it (on the name's baseline), and on
        /// the right, with room between, the longest of these wordings that fits (else the shortest drawn smaller,
        /// Theme.Fitted); warn: in the warning colour.</summary>
        public void TitleBar(string name, string version, string[] right, bool warn)
        {
            float x = Widgets.U(12), top = Widgets.U(6), h = Widgets.U(30), edge = rect.width - Widgets.U(12);
            var n = new GUIContent(name);
            var v = new GUIContent(version);
            float nw = Theme.TitleName.CalcSize(n).x, vw = Theme.TitleVersion.CalcSize(v).x;
            float o = Mathf.Max(1f, Mathf.Round(Widgets.U(1)));
            if (Theme.TitleShadow != null) GUI.Label(new Rect(x + o, top + o, nw, h), n, Theme.TitleShadow);
            GUI.Label(new Rect(x, top, nw, h), n, Theme.TitleName);
            GUI.Label(new Rect(x + nw + Widgets.U(7), top, vw, h - Widgets.U(5)), v, Theme.TitleVersion);
            float left = x + nw + Widgets.U(7) + vw + Widgets.U(28), room = edge - left;
            if (right == null || right.Length == 0 || room < Widgets.U(40)) return;
            GUIStyle style = warn ? Theme.TitleAreaOver : Theme.TitleArea;
            var said = new GUIContent(right[right.Length - 1]);
            foreach (string a in right)
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

        // ---------------------------------------------------------------- the parts: top, body, footer

        // the window's parts as last drawn (screen pixels), kept as their least height while a control is held (steady):
        // while a control is held or typed in, they do not get shorter (0.20.75: a shorter top moved the body under the
        // mouse, a shorter footer or body pulled the scroll back); let go, they take their own height
        float topMin, bodyMin, viewHeight;
        bool steady;
        static readonly GUILayoutOption[] NoOptions = new GUILayoutOption[0];

        /// <summary>The body's scroll (the window's content under its top part).</summary>
        public Vector2 Scroll;

        Widgets.Scope topScope;

        /// <summary>The part above the body (tabs, pickers, what stays in view whatever the scroll): at least as tall as it
        /// was when a hold began. EndTop closes it.</summary>
        public void BeginTop()
        {
            if (Event.current.type == EventType.Layout)
            {
                steady = GUIUtility.hotControl != 0 || GUIUtility.keyboardControl != 0;
                // a wheel step or a key (Shift + wheel on a slider, the arrow keys moving a sign) is no held control, but
                // the rows change as much: the footer keeps its height a moment after one (1.1.0, review)
                if (steady || Input.anyKey || Input.mouseScrollDelta.y != 0f) inputAt = Time.unscaledTime;
            }
            topScope = Widgets.Vertical(steady && topMin > 0f ? new[] { GUILayout.MinHeight(topMin) } : NoOptions);
        }

        public void EndTop()
        {
            topScope.Dispose();
            Measure(ref topMin);
        }

        /// <summary>The body, a vertical scroll (never a horizontal one) that fills the rest; draw runs inside it with
        /// Widgets.BodyView set to the part in view. Never shorter than what its scroll shows (0.20.84: content that got
        /// shorter while scrolled to its end pulled the scroll back), never taller than it was (WindowSizing.BodyLeast).</summary>
        public void Body(Action draw)
        {
            Scroll = GUILayout.BeginScrollView(Scroll, false, false, GUIStyle.none, GUI.skin.verticalScrollbar, GUILayout.ExpandHeight(true));
            using (new Widgets.Scope(2))
            {
                float least = WindowSizing.BodyLeast(Scroll.y, viewHeight, bodyMin, steady);
                using (Widgets.Vertical(least > 0f ? new[] { GUILayout.MinHeight(least) } : NoOptions))
                {
                    Widgets.BodyView = viewHeight > 0 ? new Rect(-10000, Scroll.y, 20000, viewHeight) : (Rect?)null;
                    try { draw(); }
                    finally { Widgets.BodyView = null; }
                }
                Measure(ref bodyMin);
            }
            if (Event.current.type == EventType.Repaint) viewHeight = GUILayoutUtility.GetLastRect().height;
        }

        // the footer only grows while a control is held (or a wheel step or key came just now) and it shows the same thing
        // in a window of the same width (0.20.84: a hint under its button came and went with every change of a drag, and
        // the buttons jumped); let go, it is as tall as its rows (1.1.0, the author: an empty grey stretch stayed under the
        // button once a longer hint had gone)
        float footKeep = -1f, footWidth = -1f, footScale = -1f, inputAt = -100f;
        const float FootSettle = 0.75f;
        int footFor = int.MinValue;

        /// <summary>The footer under the body (a section; boxed false: no box, for a part whose footer is only a status line,
        /// 1.1.0, the author: an empty bar showed): while a control is held its least height is the most it has had since what (the part shown,
        /// the thing it works on) or the window's width changed, else its rows' own. draw: its rows.</summary>
        public void Footer(int what, Action draw, bool boxed = true)
        {
            bool held = Time.unscaledTime - inputAt < FootSettle;               // a control held, or a wheel step or key just now
            if (Event.current.type == EventType.Layout && (!held || what != footFor || Mathf.Abs(rect.width - footWidth) > 0.5f || Widgets.Scale != footScale))
            {
                footFor = what;
                footWidth = rect.width;
                footScale = Widgets.Scale;
                footKeep = 0f;
            }
            GUILayoutOption[] least = footKeep > 0f ? new[] { GUILayout.MinHeight(footKeep) } : NoOptions;
            using (boxed ? Widgets.Vertical(Theme.Section, least) : Widgets.Vertical(least))
                draw();
            if (Event.current.type == EventType.Repaint) footKeep = Mathf.Max(footKeep, GUILayoutUtility.GetLastRect().height);
        }

        /// <summary>The last row: left (the window's own, at the row's start: SignLink's Show all, 1.1.0), the keys that close
        /// the window (dim frames) and Close; true when Close was clicked.</summary>
        public bool CloseRow(string[] keys, Action left = null)
        {
            using (Widgets.Horizontal())
            {
                left?.Invoke();
                Widgets.Keys(keys);
                return Widgets.Button(Lang.T("Close"), Theme.Button, Widgets.W(90));
            }
        }

        /// <summary>The group just closed, measured at Repaint: its own height, or while steady the most it had.</summary>
        void Measure(ref float least)
        {
            if (Event.current.type != EventType.Repaint) return;
            float h = GUILayoutUtility.GetLastRect().height;
            least = steady ? Mathf.Max(least, h) : h;
        }

        // ---------------------------------------------------------------- edges

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

        /// <summary>Every edge and corner of the window resizes it (0.20.50): the opposite edge stays, never past the screen
        /// nor below what the rows need (WindowSizing.Drag). The size goes to the settings (shown at once, kept once let
        /// go), the place with it. While held, the mouse is read on every event, since drag events stop reaching the
        /// window once the mouse leaves it. EdgeMarks paints the edges last.</summary>
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
                look.Resizing(nw / Screen.width, nh / Screen.height);      // changed between Layout and MouseDrag)
            }
            if (e.type == EventType.MouseDrag || e.type == EventType.MouseUp) e.Use();
        }

        /// <summary>The edge let go (or the window closed while held): the size is kept, and the place (once nothing is held).</summary>
        public void StopResize()
        {
            if (!resizing) return;
            resizing = false;
            if (GUIUtility.hotControl == gripId) GUIUtility.hotControl = 0;
            look.Resized();
        }

        /// <summary>The edges' marks: six dots in the bottom-right corner, and an amber line along the edge (or the two of a
        /// corner) under the mouse or held, so it shows where the window can be taken; and the thin rule under the title.</summary>
        void EdgeMarks()
        {
            if (Event.current.type != EventType.Repaint) return;
            Color was = GUI.color;
            float d = Mathf.Max(2f, Widgets.U(2.5f));
            Color grip = resizing ? Theme.GripHeld : Theme.GripColor;
            GUI.color = new Color(grip.r, grip.g, grip.b, resizing ? 0.9f : Theme.Painted ? 1f : 0.45f);
            for (int i = 0; i < 3; i++)
                for (int j = 0; j <= i; j++)
                    GUI.DrawTexture(new Rect(rect.width - d * 2f * (j + 1), rect.height - d * 2f * (i - j + 1), d, d), Texture2D.whiteTexture);
            int lit = resizing ? dragEdges : GUIUtility.hotControl == 0 ? EdgesAt(Event.current.mousePosition) : 0;
            if (lit != 0)
            {
                float t = Mathf.Max(2f, Widgets.U(Theme.EdgeThickness));
                Color edge = Theme.EdgeColor;
                GUI.color = new Color(edge.r, edge.g, edge.b, resizing ? 0.95f : 0.7f);
                if ((lit & WindowSizing.Left) != 0) GUI.DrawTexture(new Rect(0, 0, t, rect.height), Texture2D.whiteTexture);
                if ((lit & WindowSizing.Right) != 0) GUI.DrawTexture(new Rect(rect.width - t, 0, t, rect.height), Texture2D.whiteTexture);
                if ((lit & WindowSizing.Top) != 0) GUI.DrawTexture(new Rect(0, 0, rect.width, t), Texture2D.whiteTexture);
                if ((lit & WindowSizing.Bottom) != 0) GUI.DrawTexture(new Rect(0, rect.height - t, rect.width, t), Texture2D.whiteTexture);
            }
            GUI.color = Theme.TitleRuleColor;                               // the title's own line: a little room under it,
            float rule = Theme.Painted ? Mathf.Max(1f, Mathf.Round(Widgets.U(Theme.TitleRuleThickness))) : Mathf.Max(1f, Widgets.U(Theme.TitleRuleThickness));
            GUI.DrawTexture(new Rect(Widgets.U(12), Widgets.U(TitleRule), rect.width - Widgets.U(24), rule), Texture2D.whiteTexture);
            GUI.color = was;                                                // then a thin rule (0.20.50)
        }
    }
}
