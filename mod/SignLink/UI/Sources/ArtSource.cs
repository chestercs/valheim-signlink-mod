using System;
using System.Collections.Generic;
using SignLink.Animation;
using SignLink.Pictures;
using SignLink.Presets;
using SignLink.Signs;
using UnityEngine;
using PetyusPolisz.Kit;

namespace SignLink.UI.Sources
{
    /// <summary>Art: the pieces that come with SignLink (ArtLibrary): glyph art made of letters and symbols, and pixel
    /// art with animations. Every player has them. A list of them with headings, the picked one's quick view, light and
    /// size (SizeRows), and putting it on the sign (SignFit.Put): sized, moved and turned as 3 Place says, lit as chosen.
    /// Pixel pictures are drawn from their own text (animations play); glyph art shows its render. A piece is read once,
    /// on a worker (a big one is megabytes), and the texts read for the quick view are the ones put on.</summary>
    internal sealed class ArtSource : Source
    {
        public override string Title => "Art";                          // lang:keys (the source strip shows it through T)
        public override SignRecipe.Kinds Kind => SignRecipe.Kinds.Art;

        static List<Preset> Items => ArtLibrary.Items;

        public int Selected = -1;
        Vector2 scroll;
        /// <summary>The list open (0.20.58, the author: the list should close for Art too, as for Pictures): a drop-down
        /// like the Picture source's, open while nothing is picked. A click on a piece closes it, as on Picture (1.1.0, the
        /// author; it stayed open since 0.20.142); the arrows step the pieces with it shut. Selecting a sign that holds a
        /// piece leaves it as the player left it (0.20.143).</summary>
        bool browsing = true;
        Action showList, hideList;                                          // the list button's work, made once: a value, not a flip
                                                                            // (two events of one click must not undo each other)
        readonly Thumb thumb = new Thumb();
        readonly Job<Quick> quickJob = new Job<Quick>();
        Quick shown;                                                        // the picked piece as read (null while reading, or failed)
        string shownError, shownKey;
        Quick snapFor;                                                      // what Scale snapped last, and for which size
        double snapped = 1, snapWant;
        Choice shownFor;                                                    // the choice slot the list shows (Session.Chosen)
        bool shownOwn = true;                                               // ... and whether it is the player's own
        /// <summary>The player's own pick while a sign's own is loaded: back when the own slot is.</summary>
        string ownPath;
        bool ownKept;

        public Preset Current => Selected >= 0 && Selected < Items.Count ? Items[Selected] : null;

        int reveal = -1;                                                    // the list line to scroll into view
        /// <summary>The group heading the arrows are on (0.20.171, the author: the selector cannot go onto the groups), or
        /// null: they are on the picked piece.</summary>
        string focusGroup;

        /// <summary>The line of a group's heading, or -1.</summary>
        int Heading(string group)
        {
            if (lineItem == null || lineGroup == null) return -1;
            for (int k = 0; k < lineItem.Length; k++)
                if (lineItem[k] < 0 && lineGroup[k] == group) return k;
            return -1;
        }

        /// <summary>Right opens the group the cursor is on (its heading, or the picked piece's group), left shuts it, as a click
        /// on its heading does; shutting from a piece moves the cursor to the heading.</summary>
        public override bool Fold(Session s, bool openIt)
        {
            string g = focusGroup ?? Current?.Category;
            if (string.IsNullOrEmpty(g)) return false;
            bool changed = openIt ? open.Add(g) : open.Remove(g);
            if (!openIt) focusGroup = g;
            if (changed) openVersion++;
            return changed;
        }

        /// <summary>The previous or next piece as the list shows them (open groups only), picked as a click picks it
        /// (Moving on); from nothing picked, the first.</summary>
        public override bool Step(Session s, int dir)
        {
            Lines(Items);                                                   // the list shut (a click closes it): Draw made none
            int[] li = lineItem;                                            // since a group opened or shut (Update, not OnGUI)
            if (li == null || li.Length == 0) return false;
            int j = focusGroup != null ? Heading(focusGroup) : Selected >= 0 ? Array.IndexOf(li, Selected) : -1;
            if (j < 0 && Current != null) j = Heading(Current.Category);    // its group shut: on from its heading
            int k = j < 0 ? (dir > 0 ? 0 : li.Length - 1) : j + dir;      // the next line, whatever it is
            if (k < 0 || k >= li.Length) return false;
            if (li[k] < 0)                                                  // a group's heading: the cursor rests there
            {
                focusGroup = lineGroup[k];
                reveal = k;
                return true;
            }
            if (li[k] != Selected) s.Chosen.Still = false;                  // every piece picked starts moving (0.20.167)
            Selected = li[k];
            focusGroup = null;
            reveal = k;
            return true;
        }

        /// <summary>What the quick view, the size and the put need from a piece (read on any thread).</summary>
        sealed class Quick
        {
            public string Path;
            public string[] Texts;
            public float Fps;
            public string Why;                                              // what keeps it from being resized, or null
            /// <summary>The quick view's line, worded on the main thread (Lang is not for workers): the facts the worker found.</summary>
            public Func<string> Info = () => "";
            public List<double> Heights;
            public long Save, SaveStill, Largest;                           // SaveStill: an animation as its first frame
            public int W, H;
            public List<byte[]> Rows;
            public byte[] Picture;
            public Vector2 NaturalM;
        }

        static Quick Read(Preset p)
        {
            var q = new Quick { Path = p.Path };
            string name = p.Name;
            try
            {
                Preset.Content c = p.Load();
                q.Texts = c.Texts;
                q.Fps = c.Fps;
                TextTransform.Supports(c.Texts, out q.Why);
                if (q.Why == null) q.Heights = TextTransform.Heights(c.Texts);
                var input = new List<IList<string>>();
                if (p.Kind == Preset.Kinds.Anim)
                    foreach (string f in c.Texts) input.Add(new[] { f });           // one sign per frame
                else input.Add(c.Texts);                                          // one picture over the stack
                bool stack = p.Kind == Preset.Kinds.Stack, anim = p.Kind == Preset.Kinds.Anim;
                int stacked = c.Texts.Length;
                float fps = c.Fps;
                long save = p.Kind == Preset.Kinds.Anim ? SaveCost.Bytes(new[] { c.Texts[0] }, StoredAnims.Pack(c.Texts, c.Fps).Length)
                                                        : SaveCost.Bytes(c.Texts);
                q.Save = save;
                q.SaveStill = p.Kind == Preset.Kinds.Anim ? SaveCost.Bytes(new[] { c.Texts[0] }) : save;
                foreach (string t in c.Texts) q.Largest = Math.Max(q.Largest, System.Text.Encoding.UTF8.GetByteCount(t));
                if (p.Size(out double sw, out double sh)) q.NaturalM = new Vector2((float)(sw / 40), (float)(sh / 40));
                if (TextReadback.ReadFrames(input, out q.W, out q.H, out List<int[]> grids, out double pitch, asSeen: true))
                {
                    if (q.NaturalM.x <= 0 && pitch > 0) q.NaturalM = new Vector2((float)(q.W * pitch / 40), (float)(q.H * pitch / 40));
                    q.Rows = grids.ConvertAll(g => Thumb.Rows(g, q.W, q.H));
                    int w = q.W, h = q.H, frames = grids.Count;
                    q.Info = () => anim ? Lang.F("{0}: {1} x {2} pixels, {3}, {4} frames at {5:0.#} fps", name, w, h, SaveCost.Label(save), frames, fps)
                                 : stack ? Lang.F("{0}: {1} x {2} pixels on {3} stacked signs, {4}", name, w, h, stacked, SaveCost.Label(save))
                                 : Lang.F("{0}: {1} x {2} pixels, {3}", name, w, h, SaveCost.Label(save));
                }
                else
                {
                    q.Picture = p.ReadPicture?.Invoke();
                    bool glyph = q.Picture != null;
                    q.Info = () => glyph ? (stack ? Lang.F("{0}: glyph art on {1} stacked signs, {2}", name, stacked, SaveCost.Label(save))
                                                  : Lang.F("{0}: glyph art, {1}", name, SaveCost.Label(save)))
                                         : (stack ? Lang.F("{0}: no quick view on {1} stacked signs, {2}", name, stacked, SaveCost.Label(save))
                                                  : Lang.F("{0}: no quick view, {1}", name, SaveCost.Label(save)));
                }
            }
            catch (Exception e)
            {
                q.Texts = null;
                string why = e.Message;
                q.Info = () => Lang.F("{0}: cannot read it: {1}", name, why);
            }
            return q;
        }

        public override bool Load(SignRecipe r, Choice into, Session s)
        {
            int i = Preset.Find(Items, r.Path);
            if (i < 0) return false;
            into.Recipe = SignRecipe.ForPiece(Kind, r.Light, Items[i].Path, r.Still);
            into.Light = r.Light;
            into.Still = r.Still || Items[i].Kind != Preset.Kinds.Anim;     // a sign's own moving piece stays moving
            if (ReferenceEquals(into, s.Chosen)) shownFor = null;           // into the slot showing (the text editor's Reset): taken again
            return true;
        }

        /// <summary>The list takes the choice slot's pick when the session switched slots (a sign holding a piece was
        /// selected, or the player's own choice is back: its own pick with it).</summary>
        void Take(Session s)
        {
            if (shownOwn && s.Editing)                                      // leaving the player's own choice: kept
            {
                ownPath = Current?.Path;
                ownKept = true;
            }
            shownFor = s.Chosen;
            shownOwn = !s.Editing;
            if (!s.Editing && ownKept)                                      // back to it: as it was
            {
                ownKept = false;
                Selected = ownPath == null ? -1 : Items.FindIndex(p => p.Path == ownPath);
            }
            SignRecipe r = s.Chosen.Recipe;
            if (r.Kind != Kind)
            {
                if (s.Editing) Selected = -1;                               // a sign holding something else: no pick of
                return;                                                     // before on it (0.20.89); the own one is kept
            }
            Selected = Preset.Find(Items, r.Path);
        }

        public override void Tick(Session s, float now)
        {
            if (!ReferenceEquals(s.Chosen, shownFor)) Take(s);
            Preset p = Current;
            string key = p == null ? null : p.Path + "|" + p.Bytes;
            if (key != shownKey)
            {
                shownKey = key;
                shown = null;
                shownError = null;
                snapFor = null;
                quickJob.Forget();                                          // a piece picked again is read again
                thumb.Clear();
                thumb.Info = p == null ? "" : Lang.F("{0}: reading ...", p.Name);
            }
            if (quickJob.Wants(key, now, 0.15f)) StartRead(p, key, now);   // the delegates only when work starts
            s.Chosen.Recipe = p != null ? SignRecipe.ForPiece(Kind, s.Chosen.Light, p.Path, Still(s, p)) : SignRecipe.None;
            thumb.Paused = p != null && Still(s, p);
        }

        void StartRead(Preset p, string key, float now)
        {
            quickJob.Want(key, now, 0.15f,
                () => () => Read(p),
                q =>
                {
                    string info = q.Info();                                 // worded here, on the main thread
                    shown = q.Texts != null ? q : null;
                    shownError = q.Texts == null ? info : null;
                    if (q.Rows != null) thumb.Set(q.W, q.H, q.Rows, q.Fps);
                    else if (q.Picture != null)
                    {
                        Texture2D tex = PictureImporter.LoadTexture(q.Picture);
                        if (tex) thumb.SetTexture(tex);
                    }
                    thumb.Info = info;
                },
                e =>
                {
                    shownError = Lang.F("{0}: cannot read it: {1}", p.Name, e.Message);
                    thumb.Info = shownError;
                });
        }

        public override void Draw(Session s, SignLinkWindow w, float now)
        {
            bool can = Source.Ready(s);                                     // the light and the size are set for a sign
            List<Preset> items = Items;
            bool layout = Event.current.type == EventType.Layout;
            Preset now0 = Current;
            if (now0 == null && !browsing && layout) browsing = true;      // nothing to show: the list
            if (layout && now0 != null && now0.Path != openedFor)           // the picked piece's group opens with it
            {
                openedFor = now0.Path;
                if (open.Add(now0.Category)) openVersion++;
            }
            // the same parts in the same places as the Picture source (1.1.0, the author: unify Art and Picture): a title row
            // with the keys and the list's button, the pick in one line under it while the list is shut
            using (Widgets.Horizontal())
            {
                Widgets.Header(Lang.T("Art"));
                GUILayout.FlexibleSpace();
                Widgets.KeysHere(Theme.Tab, AllArrows);                     // ContentTab steps the list, folds the group (1.0.2: keys
                                                                            // shown); level with the list button (1.1.0)
                if (ListButton(browsing, now0 != null, Lang.T("Browse pieces")))
                    w.Later(browsing ? hideList ?? (hideList = () => browsing = false) : showList ?? (showList = () => browsing = true));
            }
            if (now0 != null && !browsing)                                  // the pick in one line; the quick view shows it
            {
                bool own = s.Editing && s.Held.Recipe.Kind == Kind && Preset.Find(items, s.Held.Recipe.Path) == Selected;
                GUILayout.Label(own ? Lang.F("{0}   · on the sign", now0.Name) : now0.Name, Theme.Label);
            }
            if (browsing)
            using (Widgets.Scroll(ref scroll, 260))
            {
                if (items.Count == 0) Widgets.Hint("   " + Lang.T("nothing here yet"));
                bool repaint = Event.current.type == EventType.Repaint;
                if (layout)                                                 // the lines in view only, their words made once (0.20.132)
                {
                    Lines(items);
                    if (reveal >= 0)                                        // an arrow's pick: scrolled into view
                    {
                        lines.Reveal(reveal, ref scroll.y, Widgets.U(Widgets.Tall(260)));
                        reveal = -1;
                    }
                    lines.Plan(lineItem.Length, scroll.y, Widgets.U(Widgets.Tall(260)), lineItem, Widgets.ContentWidth, Widgets.U(1f));
                }
                if (lines.Above > 0f) GUILayout.Space(lines.Above);
                for (int j = lines.First; j <= lines.Last && lineItem != null && j < lineItem.Length; j++)
                {
                    int i = lineItem[j];
                    if (i < 0)                                              // a group's heading: opens and closes it
                    {
                        string g = lineGroup[j];
                        if (Widgets.FoldHead(lineKey[j], lineWords[j], open.Contains(g), g == focusGroup ? Theme.FoldOn : Theme.Fold))   // its arrow turns (1.1.0)
                            w.Later(() =>
                            {
                                if (!open.Remove(g)) open.Add(g);
                                openVersion++;
                                focusGroup = g;                             // the arrows go on from here
                            });
                    }
                    else
                    {
                        Preset p = items[i];
                        Texture2D t = layout ? Thumbs.Get(p.ThumbKey, () => Thumbs.FromPiece(p), now) : Thumbs.Peek(p.ThumbKey);   // in view: used
                        int pick = i;
                        if (Widgets.PictureItem(i == Selected && focusGroup == null, t, lineWords[j], Widgets.ContentWidth - 30))
                            w.Later(() =>                           // a click closes the list, as on Picture (1.1.0, the author;
                            {                                       // it stayed open for the next piece since 0.20.142); the
                                if (pick != Selected) s.Chosen.Still = false;   // arrows step it open
                                Selected = pick;                    // every piece picked starts moving (0.20.167)
                                focusGroup = null;
                                browsing = false;                   // chosen: the list has done its job
                            });
                    }
                    if (repaint)
                    {
                        Rect r = GUILayoutUtility.GetLastRect();
                        lines.Saw(j, r.y, r.yMax);
                    }
                }
                if (lines.Below > 0f) GUILayout.Space(lines.Below);
            }
            // the quick view folds like 3 Place's groups (0.20.171, the author: the quick view should be a group
            // everywhere too, one that opens and closes), open at first and kept as left; closed while nothing is picked,
            // open again once a piece is (1.1.0, the author: closed while it has no content); no line under it: the
            // Summary says what it is (1.1.0, the author: as Picture)
            if (Widgets.Foldout("art.view", Lang.T("Quick view"), usable: Current != null)) thumb.Draw(Widgets.ContentWidth - 30, 220);   // as tall as Picture's
            if (!can) Widgets.Hint(Source.NotReady(s));
            GUI.enabled = can;
            LightRow.Draw(s, w, LightRow.PieceLights, LightRow.PieceNames);
            // under the light, as a GIF's on Picture (1.1.0, the author: the same things in the same places; above the quick
            // view since 0.20.167)
            if (Current != null && Current.Kind == Preset.Kinds.Anim)        // with the piece it changes (0.20.144)
            {
                bool moving = Widgets.Check(!s.Chosen.Still, Lang.T("Moving"));
                if (moving == s.Chosen.Still) w.Later(() => s.Chosen.Still = !moving);
            }
            GUI.enabled = true;
            Summary(s);
        }

        /// <summary>What the piece makes, one fact a row under its own title, as the Picture source's Summary (1.1.0, the
        /// author: the same parts; the quick view's line said the pixels and the frames before): the pixels (or glyph art;
        /// a piece has no file behind it, so no native pixels), the signs, an animation's frames, its size on the sign,
        /// then in multiplayer what it costs a server (CostRows); a moving piece as it goes on, Moving off: its still. No
        /// Size (1.1.0, the author: 3 Place sets it and its slider shows it).</summary>
        void Summary(Session s)
        {
            Preset p = Current;
            Quick q = p != null && shown != null && shown.Path == p.Path && shown.Texts != null ? shown : null;
            if (q == null) return;
            Widgets.Header(Lang.T("Summary"));
            using (Widgets.Vertical(Theme.Section))
            {
                Row(Lang.T("Pixels"), q.Rows != null ? $"{q.W} x {q.H}" : Lang.T("glyph art"));
                int signs = p.Kind == Preset.Kinds.Stack ? q.Texts.Length : 1;
                Row(Lang.T("Signs"), signs == 1 ? "1" : Lang.F("{0}, stacked", signs));
                bool still = FirstFrameOnly(s, p);
                if (p.Kind == Preset.Kinds.Anim)
                    Row(Lang.T("Frames"), Lang.F(still ? "{0} at {1:0.#} fps, goes on still" : "{0} at {1:0.#} fps", q.Texts.Length, q.Fps));
                long bytes = still ? q.SaveStill : q.Save;
                Row(Lang.T("Content size"), SaveCost.Label(bytes));
                CostRows(s, bytes);
            }
        }

        // the list's lines (0.20.132, review: every line's words were made, and every piece's thumbnail marked used, on every
        // event): a group's heading or a piece, with its words, made once for the library; drawn only while in view
        readonly ListRows lines = new ListRows();
        List<Preset> linesFor;
        int linesCount = -1, linesVersion = -1;
        string linesLang;                                                   // the language the words were made in
        int[] lineItem;                                                     // the piece's index, -1 for a heading
        string[] lineWords, lineGroup, lineKey;                            // lineKey: a heading's FoldHead key, made once
        /// <summary>The groups open (0.20.141, the author: put them into the list in groups ... that open and close):
        /// all closed at first, the picked piece's group opened with it; remembered while the game runs.</summary>
        static readonly HashSet<string> open = new HashSet<string>();
        static int openVersion;
        string openedFor;

        /// <summary>The list's lines as the loop made them: a category's heading where it starts (a fold, its arrow drawn by
        /// Widgets.FoldHead, with how many pieces it holds), and while it is open its pieces, each by its own name (1.1.0).</summary>
        void Lines(List<Preset> items)
        {
            string lang = Lang.Current;                                     // the words follow a change of language
            if (ReferenceEquals(linesFor, items) && linesCount == items.Count && linesVersion == openVersion && lang == linesLang) return;
            linesFor = items;
            linesCount = items.Count;
            linesVersion = openVersion;
            linesLang = lang;
            var item = new List<int>();
            var words = new List<string>();
            var groupOf = new List<string>();
            var count = new Dictionary<string, int>();
            var named = new Dictionary<string, int>();
            foreach (Preset p in items)
            {
                count[p.Category] = count.TryGetValue(p.Category, out int n) ? n + 1 : 1;
                named[p.Name] = named.TryGetValue(p.Name, out int m) ? m + 1 : 1;
            }
            for (int i = 0; i < items.Count; i++)
            {
                Preset p = items[i];
                bool shown = open.Contains(p.Category);
                if (i == 0 || items[i - 1].Category != p.Category)
                {
                    item.Add(-1);
                    words.Add($"{p.Category}   ({count[p.Category]})");   // the arrow is FoldHead's (1.1.0)
                    groupOf.Add(p.Category);
                }
                if (!shown) continue;
                // every piece by its own name, no heading line over a name's variants (1.1.0, the author: no "Bears:" lines
                // among the pieces); two pieces of one name say which is which
                string name = named[p.Name] > 1 ? (p.Pixel ? Lang.F("{0} (pixel art)", p.Name) : Lang.F("{0} (glyph art)", p.Name)) : p.Name;
                // the kind in words here, whole phrases for a translator (Preset.KindLabel's words)
                string kind = p.Kind == Preset.Kinds.Anim ? Lang.T("animation") : p.Kind == Preset.Kinds.Stack ? Lang.T("several signs") : Lang.T("one sign");
                item.Add(i);
                words.Add($"{name}   ({kind}, {p.SizeLabel})");
                groupOf.Add(p.Category);
            }
            lineItem = item.ToArray();
            lineWords = words.ToArray();
            lineGroup = groupOf.ToArray();
            lineKey = Array.ConvertAll(lineGroup, g => "art." + g);
        }

        SizeRows size;

        public override void DrawSize(Session s, SignLinkWindow w)
        {
            bool was = GUI.enabled;
            GUI.enabled = was && Source.Ready(s);
            if (size == null) size = new SizeRows("size." + Kind);
            Quick q = shown;                                                // its exact sizes for Shift + wheel (0.20.98)
            size.Draw(s, shown != null ? shown.NaturalM : Vector2.zero, Current != null && Movable(s), Current != null ? FitHint(s) : null,
                      k => q?.Heights != null ? TextTransform.Snap(q.Heights, k) : k);
            GUI.enabled = was;
        }

        public override bool Reverts(Session s) =>                         // Moving too (0.20.148, review)
            s.HasTarget && Current != null && !(s.Held.Recipe.Kind == Kind && s.Held.Recipe.SameSettings(s.Chosen.Recipe));

        /// <summary>The sign's own piece picked again (Take does it from the recipe), or none when it holds another kind.</summary>
        public override void Revert(Session s)
        {
            if (s.Held.Recipe.Kind == Kind)
            {
                s.Chosen.Recipe = s.Held.Recipe.Copy();
                s.Chosen.Still = s.Held.Recipe.Still || Preset.Find(Items, s.Held.Recipe.Path) is int i && i >= 0 && Items[i].Kind != Preset.Kinds.Anim;
                shownFor = null;                                            // Take picks it from the recipe
            }
            else Selected = -1;
        }

        public override bool Size(Session s, out Vector2 metres)
        {
            metres = shown != null ? shown.NaturalM : Vector2.zero;
            return metres.x > 0 && metres.y > 0;
        }

        /// <summary>Where the piece's middle sits before Right and Up: the sign's middle for a piece that can move and
        /// knows its own middle (Act puts it centred, 0.20.38, the author: the glyph art pieces are not centred by default
        /// at 0-0-0 settings), else where it was made.</summary>
        public override bool Centre(Session s, out double x, out double y)
        {
            x = y = 0;
            if (Current == null || !Current.Centre(out x, out y)) return false;
            // as it is on the sign until something changes: an Art Refit puts the piece on again, centred (0.20.146), so a
            // piece put before 0.20.38 moves from the middle then (0.20.157, review: the frame stayed off by its old centre)
            if (s.Editing && s.Held.Recipe.SameItem(s.Chosen.Recipe) && s.CentreKnown && s.Verb == Verb.None)
            {
                x = s.C0x;                                                  // the piece on the sign moves from where it is: a piece
                y = s.C0y;                                                  // put before 0.20.38 keeps the centre it was made at
                return true;
            }
            if (shown != null && shown.Path == Current.Path && shown.Why == null) x = y = 0;   // put centred
            return true;
        }

        public override void Dispose() => thumb.Clear();

        /// <summary>The piece as read goes with the window: its texts, every frame's rows and the quick view's texture (1.1.0,
        /// review: an animation kept them all until the next pick); a read still running is dropped. Tick reads it again at
        /// the next open (Act copies what it uses first).</summary>
        public override void OnClose()
        {
            quickJob.Abandon();
            thumb.Clear();
            shown = null;
            shownError = null;
            shownKey = null;
        }

        /// <summary>Another source picked: the read stays, so coming back shows the piece at once (OnClose's default would
        /// drop it).</summary>
        public override void OnLeave() { }

        public override string Problem(Session s) =>
            Current == null ? Lang.T("Pick a piece above") : shown == null ? (shownError ?? Lang.T("Reading the piece ...")) : null;

        public override bool Calculating(Session s) => Current != null && shown == null && shownError == null;

        public override string What(Session s) => $"'{Current?.Name}'";

        /// <summary>An animated piece goes on as its first frame (Moving off).</summary>
        static bool Still(Session s, Preset p) => p.Kind == Preset.Kinds.Anim && s.Chosen.Still;

        /// <summary>The sign holds this piece's frames switched off on Playing: put on again (a new size, light or place) it
        /// keeps them, still for everyone, so Moving there starts it again (1.0.2, review: a resize dropped them).</summary>
        static bool KeepsFrames(Sign root, Preset p)
        {
            ZDO held = root ? SignFinder.Zdo(root) : null;
            return p != null && p.Kind == Preset.Kinds.Anim && StoredAnims.HasFrames(held) && StoredAnims.Still(held) &&
                   SignRecipe.Parse(SignMarks.Source(root)).Path == p.Path;
        }

        /// <summary>It goes on as its first frame only: Moving off, and no frames of it kept (KeepsFrames). What it costs
        /// follows (1.0.2, review: kept frames were counted as one).</summary>
        static bool FirstFrameOnly(Session s, Preset p) => Still(s, p) && !(s.HasTarget && KeepsFrames(s.First, p));

        public override bool Heavy(Session s, out long saveBytes, out long largest)
        {
            saveBytes = shown != null ? (Current != null && FirstFrameOnly(s, Current) ? shown.SaveStill : shown.Save) : -1;
            largest = shown?.Largest ?? 0;
            return shown != null && SaveCost.Heavy(saveBytes, largest);
        }

        /// <summary>Its texts can be resized and moved (TextTransform.Supports); yes while it is still being read.</summary>
        public override bool Movable(Session s) => shown == null || shown.Why == null;

        /// <summary>The size the put really gives (pixel pieces keep a line height TMP adds up exactly), 1 when the piece
        /// cannot be resized.</summary>
        public override double ShownScale(Session s) => Scale(s);

        double Scale(Session s)
        {
            if (shown == null || shown.Why != null) return 1;
            double want = s.Chosen.Fit.Scale;
            if (!ReferenceEquals(shown, snapFor) || want != snapWant)        // no key string on every event (1.1.0, review)
            {
                snapFor = shown;
                snapWant = want;
                snapped = shown.Heights != null ? TextTransform.Snap(shown.Heights, want) : TextTransform.Snap(shown.Texts, want);
                if (snapped <= 0) snapped = 1;
            }
            return snapped;
        }

        public override string FitHint(Session s)
        {
            if (Current == null) return null;
            if (shown == null) return shownError ?? Lang.T("Reading ...");
            if (shown.Why != null) return Lang.T("Fixed size");
            double k = Scale(s);
            // one line either way: a hint that grew a sentence while Width moved made the rows below jump (0.20.161)
            if (Math.Abs(k - s.Chosen.Fit.Scale) > 0.0005)
                return shown.NaturalM.x > 0 ? Lang.F("Goes on {0:0.##} m wide", k * shown.NaturalM.x) : Lang.F("Goes on at {0:0.#} %", k * 100);
            return " ";                                                     // the line stays: the rows below do not jump
        }

        string previewKey, previewWant;
        float previewWantAt;
        PreviewContent previewContent;

        /// <summary>The live preview (0.20.129): the piece as the put writes it (lit as chosen, SignGlow; sized and placed,
        /// FitTexts.ForPut from the sign's middle, as Act's SignFit.Put); an animation by its first frame. Worked out again
        /// only when the piece, the fit or the light changes.</summary>
        public override PreviewContent Preview(Session s)
        {
            Preset p = Current;
            Quick q = p != null && shown != null && shown.Path == p.Path ? shown : null;
            if (q == null || q.Texts == null || q.Texts.Length == 0) return null;
            Fit fit = s.Chosen.Fit;
            GlowMode light = s.Chosen.Light;
            // a moving piece moves on the sign too, as it will go on (0.20.165, the author); still: its first frame
            bool moving = p.Kind == Preset.Kinds.Anim && !s.Chosen.Still && q.Fps > 0f && q.Texts.Length > 1;
            string key = $"piece|{p.Path}|{ContentPreview.FitKey(fit)}|{light}|{(moving ? "moving" : "still")}";
            if (key == previewKey) return previewContent;
            float now = Time.unscaledTime;
            if (key != previewWant)
            {
                previewWant = key;
                previewWantAt = now;
            }
            // a big piece is worked out again once the rows stand still: its last texts meanwhile, in the pose to come
            if (previewContent != null && previewContent.Key.StartsWith($"piece|{p.Path}|") && q.Save > 64 * 1024 &&
                (now - previewWantAt < 0.3f || GUIUtility.hotControl != 0))
                return previewContent.Posed(fit);
            previewKey = key;
            previewContent = null;
            IList<string> texts = SignGlow.Apply(p.Kind == Preset.Kinds.Anim && !moving ? new[] { q.Texts[0] } : q.Texts, light);
            bool known = p.Centre(out double cx, out double cy);
            try
            {
                FitTexts.Result r = FitTexts.ForPut(texts, cx, cy, known, fit, centred: true);   // every frame alike
                previewContent = moving ? PreviewContent.Moving(r.Texts, r, key, q.Fps) : PreviewContent.Made(r.Texts, r, key);
            }
            catch (ArgumentException) { }                                  // too large or too far: the put refuses it
            return previewContent;
        }

        /// <summary>Puts the piece on the selected sign's stack (SignFit.Put): lit as chosen (SignGlow), sized, its middle put
        /// on the sign's middle and moved Right and Up from there, turned around it when it knows it (Preset.Centre), the sign in the chosen pose; texts and pictures
        /// on as many signs as they need (replacing what it held), an animation saved on the sign. The texts read for the
        /// quick view are the ones put on (no second read of a megabyte piece on the main thread).</summary>
        public override void Act(Session s, SignLinkWindow w, Verb v)
        {
            Preset p = Current;
            if (p == null || !s.HasTarget) return;
            Quick q = shown != null && shown.Path == p.Path ? shown : null;
            Fit fit = s.Chosen.Fit;
            GlowMode light = s.Chosen.Light;
            Sign target = s.Target;
            SignRecipe.Kinds kind = Kind;
            bool still = Still(s, p);
            w.Act(() =>
            {
                Preset.Content c = q != null ? new Preset.Content { Texts = q.Texts, Fps = q.Fps } : p.Load();
                bool keep = still && c.Texts.Length > 1 && KeepsFrames(SignFinder.Root(target), p);   // off on Playing: frames stay
                IList<string> texts = SignGlow.Apply(still && !keep ? new[] { c.Texts[0] } : c.Texts, light);   // a still: the first frame
                bool known = p.Centre(out double cx, out double cy);
                bool anim = p.Kind == Preset.Kinds.Anim && (!still || keep);
                List<Sign> signs = SignFit.Put(target, texts, cx, cy, known, fit, p.Name, anim ? Math.Max(0.1f, c.Fps) : 0f,
                                               centred: true);                // 0-0-0 is the sign's middle (0.20.38)
                SignMarks.SetSource(signs[0], SignRecipe.ForPiece(kind, light, p.Path, still).ToString());   // this source edits it from now on
                if (keep) Plugin.Stored.SetMoving(signs[0], false);
                Fit put = SignFit.Read(signs[0], out _, out _, out _);
                s.Chosen.Fit = put;                                         // as put (a snapped size): nothing left to apply (0.20.148)
                w.Select(signs[0]);
                string how = put.Describe(light);
                if (keep) return Lang.F("'{0}' is on the sign{1}, not moving.", p.Name, how);
                if (anim) return Lang.F("'{0}' is on the sign{1}, moving.", p.Name, how);
                if (still) return Lang.F("'{0}' is on the sign{1}, still.", p.Name, how);
                string save = SaveCost.Label(SaveCost.Bytes(texts));
                return signs.Count > 1 ? Lang.F("'{0}' is on the sign{1} ({2} signs, {3}).", p.Name, how, signs.Count, save)
                                       : Lang.F("'{0}' is on the sign{1} ({2}).", p.Name, how, save);
            });
        }
    }
}
