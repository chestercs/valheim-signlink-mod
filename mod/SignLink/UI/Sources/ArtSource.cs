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
        public override string Title => "Art";
        public override SignRecipe.Kinds Kind => SignRecipe.Kinds.Art;

        static List<Preset> Items => ArtLibrary.Items;

        public int Selected = -1;
        Vector2 scroll;
        /// <summary>The list open (0.20.58, the author: the list should close for Art too, as for Pictures): a drop-down
        /// like the Picture source's, open while nothing is picked. A pick leaves it open (0.20.142, the author: for the art
        /// pieces I would like the list to stay open after a pick; pictures still close), and so does selecting a sign
        /// that holds a piece (0.20.143, the author: keep it open when editing too): it stays as the player left it.</summary>
        bool browsing = true;
        readonly Thumb thumb = new Thumb();
        readonly Job<Quick> quickJob = new Job<Quick>();
        Quick shown;                                                        // the picked piece as read (null while reading, or failed)
        string shownError, shownKey, snapKey;
        double snapped = 1;
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
            if (lineItem == null || lineCategory == null || lineGroup == null) return -1;
            for (int k = 0; k < lineItem.Length; k++)
                if (lineItem[k] < 0 && lineCategory[k] && lineGroup[k] == group) return k;
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
            int[] li = lineItem;
            if (li == null || li.Length == 0 || lineCategory == null) return false;
            int j = focusGroup != null ? Heading(focusGroup) : Selected >= 0 ? Array.IndexOf(li, Selected) : -1;
            if (j < 0 && Current != null) j = Heading(Current.Category);    // its group shut: on from its heading
            for (int k = j < 0 ? (dir > 0 ? 0 : li.Length - 1) : j + dir; k >= 0 && k < li.Length; k += dir)
            {
                if (li[k] < 0 && lineCategory[k])                           // a group's heading: the cursor rests there
                {
                    focusGroup = lineGroup[k];
                    reveal = k;
                    return true;
                }
                if (li[k] < 0) continue;                                    // a variants' label
                if (li[k] != Selected) s.Chosen.Still = false;              // every piece picked starts moving (0.20.167)
                Selected = li[k];
                focusGroup = null;
                reveal = k;
                return true;
            }
            return false;
        }

        /// <summary>What the quick view, the size and the put need from a piece (read on any thread).</summary>
        sealed class Quick
        {
            public string Path;
            public string[] Texts;
            public float Fps;
            public string Why, Info = "";                                   // Why: what keeps it from being resized, or null
            public List<double> Heights;
            public long Save, SaveStill, Largest;                           // SaveStill: an animation as its first frame
            public double Load;
            public int W, H;
            public List<byte[]> Rows;
            public byte[] Picture;
            public Vector2 NaturalM;
        }

        static Quick Read(Preset p)
        {
            var q = new Quick { Path = p.Path };
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
                string layers = p.Kind == Preset.Kinds.Stack ? $" on {c.Texts.Length} stacked signs" : "";
                long save = p.Kind == Preset.Kinds.Anim ? SaveCost.Bytes(new[] { c.Texts[0] }, StoredAnims.Pack(c.Texts, c.Fps).Length)
                                                        : SaveCost.Bytes(c.Texts);
                layers += $", {SaveCost.Label(save)} in the world save";
                q.Save = save;
                q.SaveStill = p.Kind == Preset.Kinds.Anim ? SaveCost.Bytes(new[] { c.Texts[0] }) : save;
                foreach (string t in c.Texts)
                {
                    q.Largest = Math.Max(q.Largest, System.Text.Encoding.UTF8.GetByteCount(t));
                    if (p.Kind == Preset.Kinds.Anim)                                  // one frame shows at a time
                        q.Load = Math.Max(q.Load, SaveCost.LoadSeconds(SaveCost.Glyphs(new[] { t })));
                }
                if (p.Kind != Preset.Kinds.Anim) q.Load = SaveCost.LoadSeconds(SaveCost.Glyphs(c.Texts));
                double wait = SaveCost.WaitSeconds(q.Load, q.Save);         // download and build, for a player who arrives
                bool heavy = SaveCost.Heavy(q.Load, q.Save, q.Largest);
                if (heavy || SaveCost.Level(wait) > 0)
                    layers += $"; {(heavy ? "very large, " : "")}{wait:0.#} s to arrive";
                if (p.Size(out double sw, out double sh)) q.NaturalM = new Vector2((float)(sw / 40), (float)(sh / 40));
                if (TextReadback.ReadFrames(input, out q.W, out q.H, out List<int[]> grids, out double pitch, asSeen: true))
                {
                    if (q.NaturalM.x <= 0 && pitch > 0) q.NaturalM = new Vector2((float)(q.W * pitch / 40), (float)(q.H * pitch / 40));
                    q.Rows = grids.ConvertAll(g => Thumb.Rows(g, q.W, q.H));
                    q.Info = $"{p.Name}: {q.W} x {q.H} pixels{layers}" + (p.Kind == Preset.Kinds.Anim ? $", {grids.Count} frames at {c.Fps:0.#} fps" : "");
                }
                else
                {
                    q.Picture = p.ReadPicture?.Invoke();
                    q.Info = q.Picture != null ? $"{p.Name}: glyph art{layers}" : $"{p.Name}: no quick view{layers}";
                }
            }
            catch (Exception e)
            {
                q.Texts = null;
                q.Info = $"{p.Name}: cannot read it: {e.Message}";
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
                snapKey = null;
                quickJob.Forget();                                          // a piece picked again is read again
                thumb.Clear();
                thumb.Info = p == null ? "" : $"{p.Name}: reading ...";
            }
            quickJob.Want(key, now, 0.15f,
                () => { Preset pp = p; return () => Read(pp); },
                q =>
                {
                    shown = q.Texts != null ? q : null;
                    shownError = q.Texts == null ? q.Info : null;
                    if (q.Rows != null) thumb.Set(q.W, q.H, q.Rows, q.Fps);
                    else if (q.Picture != null)
                    {
                        Texture2D tex = PictureImporter.LoadTexture(q.Picture);
                        if (tex) thumb.SetTexture(tex);
                    }
                    thumb.Info = q.Info;
                },
                e =>
                {
                    shownError = $"{p.Name}: cannot read it: {e.Message}";
                    thumb.Info = shownError;
                });
            s.Chosen.Recipe = p != null ? SignRecipe.ForPiece(Kind, s.Chosen.Light, p.Path, Still(s, p)) : SignRecipe.None;
            thumb.Paused = p != null && Still(s, p);
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
            using (Widgets.Horizontal())
            {
                bool own = now0 != null && s.Editing && s.Held.Recipe.Kind == Kind && Preset.Find(items, s.Held.Recipe.Path) == Selected;
                GUILayout.Label(now0 == null ? "Nothing picked yet" : $"{now0.Name}   ({now0.KindLabel}, {now0.SizeLabel})" +
                                (own ? "   · on the sign" : ""), Theme.Label, Widgets.W(Widgets.ContentWidth - 30 - 250));
                Widgets.Keys("↑ ↓", "← →");                                 // ContentTab steps the list, folds the group (1.0.2: keys shown)
                GUI.enabled = now0 != null || !browsing;                    // nothing picked yet: the list is all there is (0.20.69)
                bool open = Widgets.Toggle(browsing, browsing ? "Hide list" : "Browse pieces", Theme.Tab, Widgets.W(170));   // lit while it shows
                GUI.enabled = true;
                if (open != browsing) w.Later(() => browsing = open);
            }
            if (browsing)
            using (Widgets.Scroll(ref scroll, 260))
            {
                if (items.Count == 0) Widgets.Hint("   nothing here yet");
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
                    if (i < 0 && lineCategory[j])                           // a group's heading: opens and closes it
                    {
                        string g = lineGroup[j];
                        if (Widgets.Button(lineWords[j], g == focusGroup ? Theme.FoldOn : Theme.Fold))
                            w.Later(() =>
                            {
                                if (!open.Remove(g)) open.Add(g);
                                openVersion++;
                                focusGroup = g;                             // the arrows go on from here
                            });
                    }
                    else if (i < 0) GUILayout.Label(lineWords[j], Theme.Label);   // a piece's variants under one name
                    else
                    {
                        Preset p = items[i];
                        Texture2D t = layout ? Thumbs.Get(p.ThumbKey, () => Thumbs.FromPiece(p), now) : Thumbs.Peek(p.ThumbKey);   // in view: used
                        int pick = i;
                        if (Widgets.PictureItem(i == Selected && focusGroup == null, t, lineWords[j], Widgets.ContentWidth - 30))
                            w.Later(() =>                           // the list stays open: the next piece is a click away (0.20.142)
                            {
                                if (pick != Selected) s.Chosen.Still = false;   // every piece picked starts moving (0.20.167)
                                Selected = pick;
                                focusGroup = null;
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
            // above the quick view (0.20.167, the author: this button is hidden away ... put it above the quick view; it
            // was under the light, the last row)
            if (Current != null && Current.Kind == Preset.Kinds.Anim)        // with the piece it changes (0.20.144)
            {
                GUI.enabled = can;
                bool moving = Widgets.Check(!s.Chosen.Still, "Moving");
                if (moving == s.Chosen.Still) w.Later(() => s.Chosen.Still = !moving);
                GUI.enabled = true;
            }
            // the quick view folds like 3 Place's groups (0.20.171, the author: the quick view should be a group
            // everywhere too, one that opens and closes), open at first and kept as left
            if (Widgets.Foldout("art.view", "Quick view")) thumb.Draw(Widgets.ContentWidth - 30, 200);
            if (!can) Widgets.Hint(Source.NotReady(s));
            GUI.enabled = can;                                              // the last group (0.20.69)
            LightRow.Draw(s, w, LightRow.PieceLights, LightRow.PieceNames);
            GUI.enabled = true;
            Arriving(s);
        }

        /// <summary>What the piece costs a server, as the Picture source shows it (0.20.168, the author: we could show the
        /// same indicator there as for pictures): the wait of a player who arrives (download and build), whether it fits
        /// the multiplayer limits, and what it adds to the world save; a moving piece as it goes on (Moving off: its still).</summary>
        void Arriving(Session s)
        {
            Preset p = Current;
            Quick q = p != null && shown != null && shown.Path == p.Path && shown.Texts != null ? shown : null;
            if (q == null) return;
            long bytes = FirstFrameOnly(s, p) ? q.SaveStill : q.Save;
            double down = SaveCost.DownloadSeconds(bytes), wait = q.Load + down;
            int level = SaveCost.Level(wait);
            string limit = Limit(s);
            Widgets.Field("Arriving", $"{wait:0.#} s ({down:0.#} s download, {q.Load:0.#} s build)",
                          level == 2 ? Theme.Warn : level == 1 ? Theme.Notice : Theme.Good);
            if (limit != null) Widgets.Field("Multiplayer", limit, Theme.Warn);
            else if (SignText.Shared()) Widgets.Field("Multiplayer", "fits the limits", Theme.Good);
            else Widgets.Field("Multiplayer", "no limit here", Theme.Small);
            Widgets.Field("World save", SaveCost.Label(bytes));
        }

        // the list's lines (0.20.132, review: every line's words were made, and every piece's thumbnail marked used, on every
        // event): a category, a group or a piece, with its words, made once for the library; drawn only while in view
        readonly ListRows lines = new ListRows();
        List<Preset> linesFor;
        int linesCount = -1, linesVersion = -1;
        int[] lineItem;                                                     // the piece's index, -1 for a heading
        string[] lineWords, lineGroup;
        bool[] lineCategory;
        /// <summary>The groups open (0.20.141, the author: put them into the list in groups ... that open and close):
        /// all closed at first, the picked piece's group opened with it; remembered while the game runs.</summary>
        static readonly HashSet<string> open = new HashSet<string>();
        static int openVersion;
        string openedFor;

        /// <summary>The list's lines as the loop made them: a category's heading where it starts (a fold: ▸ closed, ▾ open,
        /// with how many pieces it holds), and while it is open a group's where a run of its variants starts, then the piece
        /// (a variant indented under its group).</summary>
        void Lines(List<Preset> items)
        {
            if (ReferenceEquals(linesFor, items) && linesCount == items.Count && linesVersion == openVersion) return;
            linesFor = items;
            linesCount = items.Count;
            linesVersion = openVersion;
            var item = new List<int>();
            var words = new List<string>();
            var groupOf = new List<string>();
            var category = new List<bool>();
            var count = new Dictionary<string, int>();
            foreach (Preset p in items) count[p.Category] = count.TryGetValue(p.Category, out int n) ? n + 1 : 1;
            for (int i = 0; i < items.Count; i++)
            {
                Preset p = items[i];
                bool shown = open.Contains(p.Category);
                if (i == 0 || items[i - 1].Category != p.Category)
                {
                    item.Add(-1);
                    words.Add($"{(shown ? "▾" : "▸")}  {p.Category}   ({count[p.Category]})");
                    groupOf.Add(p.Category);
                    category.Add(true);
                }
                if (!shown) continue;
                bool grouped = (i > 0 && items[i - 1].Group == p.Group && items[i - 1].Category == p.Category) ||
                               (i + 1 < items.Count && items[i + 1].Group == p.Group && items[i + 1].Category == p.Category);
                if (grouped && (i == 0 || items[i - 1].Group != p.Group))
                {
                    item.Add(-1);
                    words.Add("   " + p.Group);
                    groupOf.Add(p.Category);
                    category.Add(false);
                }
                item.Add(i);
                words.Add(grouped ? $"        {(p.Variant.Length > 0 ? p.Variant : p.Pixel ? "pixel art" : "glyph art")}   ({p.KindLabel}, {p.SizeLabel})" : $"{p.Name}   ({p.KindLabel}, {p.SizeLabel})");
                groupOf.Add(p.Category);
                category.Add(false);
            }
            lineItem = item.ToArray();
            lineWords = words.ToArray();
            lineGroup = groupOf.ToArray();
            lineCategory = category.ToArray();
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

        public override string Problem(Session s) =>
            Current == null ? "Pick a piece above" : shown == null ? (shownError ?? "Reading the piece ...") : null;

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

        public override bool Heavy(Session s, out double loadSeconds, out long saveBytes, out long largest)
        {
            loadSeconds = shown?.Load ?? 0;
            saveBytes = shown != null ? (Current != null && FirstFrameOnly(s, Current) ? shown.SaveStill : shown.Save) : -1;
            largest = shown?.Largest ?? 0;
            return shown != null && SaveCost.Heavy(loadSeconds, saveBytes, largest);
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
            string key = shown.Path + "|" + want;
            if (key != snapKey)
            {
                snapKey = key;
                snapped = shown.Heights != null ? TextTransform.Snap(shown.Heights, want) : TextTransform.Snap(shown.Texts, want);
                if (snapped <= 0) snapped = 1;
            }
            return snapped;
        }

        public override string FitHint(Session s)
        {
            if (Current == null) return null;
            if (shown == null) return shownError ?? "Reading ...";
            if (shown.Why != null) return "Fixed size";
            double k = Scale(s);
            // one line either way: a hint that grew a sentence while Width moved made the rows below jump (0.20.161)
            if (Math.Abs(k - s.Chosen.Fit.Scale) > 0.0005)
                return shown.NaturalM.x > 0 ? $"Goes on {k * shown.NaturalM.x:0.##} m wide" : $"Goes on at {k * 100:0.#} %";
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
                if (keep) return $"'{p.Name}' is on the sign{how}, not moving.";
                if (anim) return $"'{p.Name}' is on the sign{how}, moving.";
                if (still) return $"'{p.Name}' is on the sign{how}, still.";
                string save = SaveCost.Label(SaveCost.Bytes(texts));
                return signs.Count > 1 ? $"'{p.Name}' is on the sign{how} ({signs.Count} signs, {save})."
                                       : $"'{p.Name}' is on the sign{how} ({save}).";
            });
        }
    }
}
