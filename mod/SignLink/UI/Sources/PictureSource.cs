using System;
using System.Collections.Generic;
using System.IO;
using SignLink.Pictures;
using SignLink.Signs;
using UnityEngine;
using PetyusPolisz.Kit;

namespace SignLink.UI.Sources
{
    /// <summary>Picture: a PNG, JPG or GIF from the images folder (PictureFolder) onto the selected sign as saved sign text
    /// (as many stacked copies as it needs) that vanilla players see; a moving GIF also plays over it for SignLink
    /// players. Its Resolution (pixels wide: the data), its width in metres (the pixels' size, free), seamless pixels and
    /// its light are chosen here; PNG transparency and moving GIFs once, on Settings (Pictures); where
    /// it sits on 3 Place. The quick view shows the exact pixel grid. The folder's sizes are read on a worker;
    /// the picked file is decoded once (kept while the window is open), its grid and its sign texts are made on workers
    /// as the settings settle, and Place puts those very texts on: a photo costs one decode and one encode. A sign whose
    /// file is not on this computer (another player's picture) is edited from the sign itself (0.20.49, SignPicture):
    /// its pixels read back stand in for the decoded file; Resolution goes down only, Width is free.</summary>
    internal sealed partial class PictureSource : Source
    {
        const string Unreadable = "cannot read: PNG, JPG or GIF only (convert HEIC or WebP first)";
        /// <summary>Above this the quick view says a large photo is being read before the game stops for its decode.</summary>
        const long LargePhoto = 12000000;

        /// <summary>The settings: Resolution by AutoDetail until the player sets it (in multiplayer one that fits the
        /// limits), 2 m wide. The pose and the place follow Session.Chosen.Fit (Tick).</summary>
        PictureOptions o = new PictureOptions { WidthM = 2f, Pitch = PictureOptions.Pitches[1], Place = Placement.Custom };   // Detail: AutoDetail
        readonly ImportJob job = new ImportJob();
        List<string> files = new List<string>();
        int selected = -1;
        Vector2 listScroll;
        int revealRow = -1;                                                 // the list row to scroll into view
        /// <summary>The folder of the images folder the list shows ("" the folder itself, as PictureImporter.Relative names
        /// it), its folders (full paths; 1.0.2, the owner: folders copied into the images folder, walked in the window), and
        /// the row the arrows stand on: an up row in a folder, then the folders, then the files, where the pick follows it.</summary>
        string inFolder = "";
        List<string> folders = new List<string>();
        int cursor = -1;
        int Up => inFolder.Length > 0 ? 1 : 0;                              // the ".." row
        int FileRow(int i) => Up + folders.Count + i;                      // the row of file i

        /// <summary>A file picked from the list (a click, or an arrow): a new one starts at the most that fits, as Max finds it.</summary>
        void Pick(Session s, int pick)
        {
            if (pick != selected)
            {
                job.Cancel();                                               // another file: a GIF still decoding stops
                // a new picture starts at the most that fits, as Max finds it (0.20.99, the author: in Content the
                // density slider should start at the Max setting by default): Light's grid first, then up as its bytes are counted
                maxTarget = MaxTarget(s);                                   // an area full already: Light, not 8 px
                maxSteps = maxTarget > 0 ? 6 : 0;
                maxLastW = -1;
                maxNothing = false;
                o.DetailAuto = true;
                o.Animate = PluginConfig.MovingGifs.Value;                  // a GIF starts as the last one went (0.20.160)
            }
            selected = pick;
            cursor = FileRow(pick);
        }

        /// <summary>The previous or next row of the list (the up row and the folders stand on it too, 1.0.2; a picture that
        /// cannot be read is skipped): a file is picked as a click picks it, a folder only waits for → (Fold); the list stays
        /// as it is (open or shut).</summary>
        public override bool Step(Session s, int dir)
        {
            int total = Up + folders.Count + files.Count;
            if (total == 0) return false;
            int from = cursor >= 0 && cursor < total ? cursor : selected >= 0 ? FileRow(selected) : -1;
            for (int k = from < 0 ? (dir > 0 ? 0 : total - 1) : from + dir; k >= 0 && k < total; k += dir)
            {
                int i = k - Up - folders.Count;                             // the file's index, or negative: up or a folder
                if (i >= 0 && rowBad != null && i < rowBad.Length && rowBad[i]) continue;
                cursor = k;
                if (i >= 0) Pick(s, i);
                revealRow = k;
                return true;
            }
            return false;
        }

        /// <summary>→ opens the folder the arrows stand on (the up row: the folder above), ← leaves the folder the list shows
        /// (1.0.2; the Art list's groups open and shut so).</summary>
        public override bool Fold(Session s, bool open)
        {
            if (!open) return inFolder.Length > 0 && Enter(PictureImporter.DirOf(inFolder));
            if (cursor < 0 || cursor >= Up + folders.Count) return false;
            return Enter(cursor < Up ? PictureImporter.DirOf(inFolder) : PictureImporter.Relative(folders[cursor - Up]));
        }

        /// <summary>The list shows this folder of the images folder: its folders and files, the pick kept when it is there (in
        /// another folder it is not: nothing picked, the quick view empty), the arrows on the folder just left when going up,
        /// else at the top; the list open.</summary>
        bool Enter(string folder)
        {
            string left = inFolder;
            inFolder = folder;
            Relist();
            cursor = -1;
            if (left.Length > folder.Length)                                // up: the arrows on the folder just left
                cursor = folders.FindIndex(f => string.Equals(PictureImporter.Relative(f), left, StringComparison.OrdinalIgnoreCase));
            if (cursor >= 0) cursor += Up;
            browsing = true;
            listScroll.y = 0f;
            revealRow = Math.Max(0, cursor);
            return true;
        }
        Choice shownFor;                                                // the choice slot the rows show (Session.Chosen)
        bool shownOwn = true;                                           // ... and whether it is the player's own
        /// <summary>The player's own settings and pick while a sign's own are loaded: back when the own slot is.</summary>
        PictureOptions ownOptions;
        string ownFile;

        /// <summary>What a file in the folder is: its size from its header, its length and time (a changed file is
        /// another picture), or why it cannot be read.</summary>
        sealed class Header
        {
            public int W, H;
            public string Stamp = "", Error;
        }

        readonly Dictionary<string, Header> headers = new Dictionary<string, Header>();
        readonly Job<Dictionary<string, Header>> headerJob = new Job<Dictionary<string, Header>>();
        string headerKey;

        /// <summary>The picked file decoded: RGBA rows top-down, halved as Grid would halve it first (RawPixels); SrcW x
        /// SrcH is the picture's own size, which sets the plan.</summary>
        sealed class Decoded
        {
            public string File, Stamp;
            public byte[] Rgba;
            public int W, H, SrcW, SrcH;
            /// <summary>A GIF of one frame: it does not move (the Moving row); a longer one's words come from RoomForGif, as its
            /// frames' stride follows the sign's size.</summary>
            public string Motion;
            /// <summary>A GIF: its size and frames, for what its screen takes at the sign's size (0: not known).</summary>
            public int MW, MH, Count;
        }

        readonly Job<Decoded> decodeJob = new Job<Decoded>();
        Decoded source;
        string readError, readErrorKey;

        /// <summary>The quick view's grid: the very pixels Place encodes, with their RGBA rows for the texture.</summary>
        sealed class Gridded
        {
            public int[] Px;
            public byte[] Rows;
            public PicturePlan Plan;
            public string Key;
        }

        readonly Job<Gridded> gridJob = new Job<Gridded>();
        readonly Thumb thumb = new Thumb();
        /// <summary>The file list open (0.20.48, a drop-down like Signs nearby; the author: the picture browser should be a
        /// drop-down too): open while nothing is picked, closed on a pick and while a sign's own picture is edited, so the quick
        /// view and the Size rows come first.</summary>
        bool browsing = true;
        int[] gridPx;
        string gridKey, thumbFile;

        /// <summary>The selected sign's own picture read back from its texts (0.20.49): its file is not here. SignFile
        /// names it where a file's path would (never a file: no file is read for it).</summary>
        const string SignFile = "<the sign>";
        bool fromSign;
        /// <summary>The sign's recipe as loaded: its file's name, see-through and moving as made, its columns.</summary>
        SignRecipe signHeld = SignRecipe.None;
        int signFileCols;

        sealed class FromSign
        {
            public SignPicture P;
            public double ShiftR, ShiftU;                               // ShiftX, ShiftY in the frame as built (wu)
            public SignRecipe Held;                                     // the sign's recipe when its texts were read
            public ulong Hash;                                          // its texts (SignPicture.TextsHash)
        }

        readonly Job<FromSign> signJob = new Job<FromSign>();
        FromSign signRead;
        /// <summary>The last read picture's hash, kept over a source round trip and a reopen (OnLeave and Reload drop
        /// signRead): the same picture read again keeps the player's Resolution and Width (0.20.148, review).</summary>
        ulong keptHash;
        bool keptHashSet;
        /// <summary>A recipe's Resolution was loaded: Tick checks it still makes the sign's grid (0.20.149).</summary>
        bool checkDetail;
        Header signHeader;
        string signError;
        float signNativeM;

        /// <summary>What the picture adds to the world save: its exact sign texts for that grid, kept for Place.</summary>
        sealed class Costed
        {
            public EncodedPicture Pic;
            public long Bytes;
            public int Largest, Glyphs;
            public string Stamp, File;
            public long Pixels;                                         // the plan's it was counted for (Estimated)
        }

        readonly Job<Costed> costJob = new Job<Costed>();
        Costed cost;
        string costFor, costError;

        // once per Layout event (Tick): the keys and numbers the rows, the footer and the Place tab read
        string file, name, decodeKey, wantKey, optionsKey, costKey;
        Header header;
        bool known, decoded, counted;
        PicturePlan plan;
        bool showCoarsened, showCapped, showCostError, fineLimited;
        int fineShift;

        public override string Title => "Picture";
        public override SignRecipe.Kinds Kind => SignRecipe.Kinds.Picture;
        public override bool Resizable => false;
        public override bool Busy => job.Busy || pendingAct != null;

        public override void OnOpen() => Reload();

        public override void Dispose()
        {
            OnClose();
            thumb.Clear();
        }

        /// <summary>A photo's buffer goes with the window (and when another source is picked); the next open decodes
        /// it again if it is still the pick. So do the grid and the counted texts (0.20.132, review: kept until the next
        /// open, which drops them): work still running for them stops.</summary>
        public override void OnClose()
        {
            OnLeave();
            job.Cancel();                                                   // the window shut: a GIF still decoding stops
            gridJob.Abandon();
            gridKey = null;
            gridPx = null;
            costJob.Abandon();
            costFor = null;
            cost = null;
        }

        /// <summary>Another source: the photo's buffer goes, a placement running goes on (its done handler copes with
        /// another selection).</summary>
        public override void OnLeave()
        {
            DropPending();
            source = null;
            decodeJob.Abandon();
            signJob.Abandon();                                              // the sign's picture is read again on return
            signRead = null;
            signHeader = null;
        }

        /// <summary>The folder's files again and the buffers dropped (the Reload button, the window opened again).</summary>
        void Reload()
        {
            DropPending();
            Relist();
            source = null;
            decodeJob.Abandon();
            signJob.Abandon();
            signRead = null;
            signHeader = null;
            gridJob.Forget();
            gridKey = null;
            gridPx = null;
            costJob.Forget();
            costFor = null;
            cost = null;
        }

        /// <summary>The folder's files again, the pick kept by name; the decoded picture, its grid and its cost stay
        /// (their keys hold the file's stamp: a file changed on disk is read again by itself).</summary>
        void Relist()
        {
            string keep = selected >= 0 && selected < files.Count ? files[selected] : null;
            files = PictureImporter.Files(inFolder, withUnreadable: true);
            folders = PictureImporter.Folders(inFolder);
            selected = keep == null ? -1 : files.IndexOf(keep);
            cursor = selected >= 0 ? FileRow(selected) : cursor >= Up + folders.Count + files.Count ? -1 : cursor;   // the highlight on the pick (review)
            long folder = 0;
            try { folder = Directory.GetLastWriteTimeUtc(PictureImporter.FolderPath(inFolder)).Ticks; }
            catch (Exception) { }
            headerKey = folder + "|" + inFolder + "|" + string.Join("\n", files.ToArray());
            headerJob.Forget();                                             // the same names may hold other bytes now
        }

        /// <summary>The list's index of a picture by the name a sign keeps (PictureImporter.Relative, its folders in it): the
        /// list moves to that folder first when it shows another and the folder is here (1.0.2); -1 when the file is not.</summary>
        int IndexOf(string fileName)
        {
            if (string.IsNullOrEmpty(fileName)) return -1;
            string want = fileName.Replace('\\', '/'), folder = PictureImporter.DirOf(want);
            if (!string.Equals(folder, inFolder, StringComparison.OrdinalIgnoreCase) && PictureImporter.PathFor(want) != null &&
                Directory.Exists(PictureImporter.FolderPath(folder)))
            {
                inFolder = folder;
                Relist();
                cursor = -1;
            }
            return files.FindIndex(f => string.Equals(PictureImporter.Relative(f), want, StringComparison.OrdinalIgnoreCase));
        }

        // the Moving row's figures, made at a Layout (1.0.2, review: a file stat and allocations on every event)
        long gifNeed, gifRoom, gifOwn;
        bool gifHeld;
        int gifStride, gifCount;
        string gifMotion;                                                   // "N frames, one in k moves": made when they change
        int roomFrame = -1;                                                 // the frame RoomForGif last ran in

        /// <summary>What the picked GIF's frames take at the size its sign shows (0: not a GIF, not known), whether frames
        /// of that size play or wait parked already, the room the screens leave, what the sign's own screen frees, and the
        /// Moving row's words (the stride follows the sign's size since 1.0.2: every frame while they fit the cap).</summary>
        void RoomForGif(Session s)
        {
            if (roomFrame == Time.frameCount) return;                       // once a frame, not once an event (review)
            roomFrame = Time.frameCount;
            int planW = plan.W;
            bool gif = source != null && source.File == file && source.Count > 1 && known;
            int kept = gif ? PictureImporter.ScreenKept(source.MW, source.MH, source.Count, planW) : 0;
            gifNeed = gif ? PictureImporter.ScreenBytes(source.MW, source.MH, kept, planW) : 0;
            int stride = gif ? GifDecoder.Stride(source.Count, PictureImporter.ScreenFrameBytes(source.MW, source.MH, planW), Animation.ScreenPlayer.FrameCap) : 0;
            if (stride != gifStride || (gif ? source.Count : 0) != gifCount)
            {
                gifStride = stride;
                gifCount = gif ? source.Count : 0;
                gifMotion = !gif ? null : stride > 1 ? $"{gifCount} frames, a long GIF: one frame in {stride} moves (the same speed, less smooth)."
                                                     : $"{gifCount} frames, all of them move.";
            }
            // playing now: nothing more to hold; parked only: starting them takes their room (review: parked frames were
            // taken as free); the screens idle far away or paused give theirs up for it (ScreenPlayer.MakeRoom, Parkable)
            var have = gifNeed > 0 ? Plugin.Screens.FramesFor(Stamp(file), (gw, gh) => PictureImporter.ScreenWidth(gw, gh, planW)) : null;
            gifHeld = have != null && Plugin.Screens.Playing(have.Key);
            gifRoom = Plugin.Screens.Room + (gifNeed > 0 && s.HasTarget ? Plugin.Screens.Parkable(s.Target) : 0);
            gifOwn = gifNeed > 0 ? OwnScreen(s) : 0;
        }

        /// <summary>The room the selected sign's own screen gives back when a new picture goes on it.</summary>
        static long OwnScreen(Session s)
        {
            long n = 0;
            if (s.HasTarget)
                foreach (Sign o in s.Stack.Count > 0 ? s.Stack : new List<Sign> { s.Target }) n += Plugin.Screens.Frees(o);
            return n;
        }

        /// <summary>Name, length and time: a file written again is another picture under the same name.</summary>
        static string Stamp(string file)
        {
            try
            {
                return PictureImporter.Stamp(file);
            }
            catch (Exception) { return null; }
        }

        public override bool Load(SignRecipe r, Choice into, Session s)
        {
            maxNothing = false;                                             // another sign's picture: Max said nothing for it
            PictureOptions loaded = o.Clone();                              // the rows take it from the slot (Take), so a
            r.Apply(loaded);                                                // load that fails leaves the player's settings
            into.Recipe = SignRecipe.ForPicture(loaded, r.File);
            into.Light = Change.LightOf(into.Recipe);
            if (ReferenceEquals(into, s.Chosen)) shownFor = null;           // into the slot showing (the text editor's Reset): taken again
            return true;                                                    // its file, or the sign itself (Take: 0.20.49)
        }

        public override void Tick(Session s, float now)
        {
            if (!ReferenceEquals(s.Chosen, shownFor)) Take(s);
            if (fromSign && selected >= 0) LeaveSign();                     // a file picked from the list: that one now
            ApplyFit(s.Chosen.Fit);
            // a picture it holds glows or is lit; nothing picked leaves the slot's light alone (0.20.91, review: it lit a
            // sign holding something else, and 3 Place rewrote that sign by itself)
            bool holds = fromSign || selected >= 0 && selected < files.Count;
            s.Chosen.Light = Change.PictureLight(s.Chosen.Light, holds, o.Glow);
            if (holds) o.Glow = s.Chosen.Light != GlowMode.Lit;
            o.Transparent = fromSign || PluginConfig.PngTransparency.Value;   // the sign's own: its empty pixels stay empty
            if (fromSign) o.Animate = signHeld.Animate;                     // the sign's own (no file here): as it was made
            if (headerJob.Wants(headerKey, now, 0f)) StartHeaders(now);      // the delegates only when work starts (0.20.132)

            if (fromSign)
            {
                ReadSign(s, now);
                file = SignFile;
                name = signHeld.File;
                header = signHeader;
            }
            else
            {
                file = selected >= 0 && selected < files.Count ? files[selected] : null;
                name = file != null ? PictureImporter.Relative(file) : "";   // with its folder: the sign says where the file is
                header = file != null && headers.TryGetValue(file, out Header h) ? h : null;
            }
            known = header != null && header.Error == null && header.W > 0 && header.H > 0;
            if (known)                                                      // the Detail model (0.20.35): the pixels across
            {
                if (o.DetailAuto) o.Detail = PictureOptions.AutoDetail(header.W, header.H, SignText.Shared());
                else if (o.Detail <= 0)                                     // a sign's recipe: the grid its width and pixel made
                {
                    PictureOptions made = o.Clone();
                    made.Detail = 0;
                    o.Detail = Math.Max(1, made.PlanFor(header.W, header.H).W);
                }
            }
            o.Fine = PluginConfig.PictureFineWidth.Value;                  // the size as asked, the Resolution kept (0.20.128)
            o.FineCap = FineCapFor(s);
            if (known && checkDetail)                                       // a recipe's Resolution: kept when it makes the sign's grid
            {                                                               // (else Fine Width counts otherwise now: the grid's pixels)
                checkDetail = false;
                PictureOptions made = o.Clone();
                made.Detail = 0;
                PicturePlan grid = made.PlanFor(header.W, header.H), asked = o.PlanFor(header.W, header.H);
                if (asked.W != grid.W || Math.Abs(asked.Pitch - grid.Pitch) > 1e-5f) o.Detail = Math.Max(1, grid.W);
            }
            Plan();                                                         // plan, fineShift, fineLimited: made again on change only
            decoded = known && source != null && source.File == file && source.Stamp == header.Stamp;
            Keys();                                                         // decodeKey, wantKey, optionsKey: the same
            costKey = wantKey != null && wantKey == gridKey ? optionsKey : null;
            counted = costFor != null && costFor == costKey;
            if (file != thumbFile)
            {
                thumbFile = file;
                thumb.Clear();                                              // the old picture goes at once
                thumb.Info = file == null ? "" : fromSign ? "reading the picture from the sign ..." : $"Loading {name} ...";
            }
            if (!fromSign) Decode(now);                                     // the sign's own: ReadSign is its decode
            MakeGrid(now);
            Count(now);
            if (decoded && !fromSign) Thumbs.Put(decodeKey, source.Rgba, source.W, source.H, now);   // a big photo's row gets its thumbnail now
            showCoarsened = known && plan.Coarsened;                         // dragging Detail changes these:
            showCapped = known && plan.Capped;                               // the lines come and go at the next Layout
            showCostError = counted && costError != null;
            if (!StepMax() && maxSteps <= 0) Pending();                     // a press waits for Max's last count (0.20.130, review)
            // the recipe as the plan makes it (the very one Place writes on the sign): it reads back as exactly its grid
            if (fromSign) s.Chosen.Recipe = !known || AsOnSign() ? signHeld.Copy() : SignRecipeFor(plan);
            else s.Chosen.Recipe = selected < 0 ? SignRecipe.None : known ? SignRecipe.ForPicture(o, plan, name) : SignRecipe.ForPicture(o, name);
        }

        /// <summary>The folder's headers read on a worker (Tick, once headerJob wants to start).</summary>
        void StartHeaders(float now) =>
            headerJob.Want(headerKey, now, 0f,
                () => { string[] list = files.ToArray(); return () => ReadHeaders(list); },
                got =>
                {
                    headers.Clear();
                    foreach (KeyValuePair<string, Header> e in got) headers[e.Key] = e.Value;
                    headersChanged++;
                },
                e => { });

        // the list's rows (0.20.132, review: every row's words were made on every event, for every row): made when the
        // files or their headers change, drawn only while in view (ListRows)
        readonly ListRows rows = new ListRows();
        List<string> rowFiles;                                              // the files the words were made for
        int rowHeaders = -1, headersChanged;                                // headersChanged: counts changes of headers
        string[] rowWords, rowKey, rowBox;
        bool[] rowBad;

        public override void Draw(Session s, SignLinkWindow w, float now)
        {
            bool can = Source.Ready(s);                                     // the light and the size are set for a sign
            bool picked = fromSign || selected >= 0 && selected < files.Count;
            if (!picked && !browsing && Event.current.type == EventType.Layout) browsing = true;   // nothing to show: the list
            using (Widgets.Horizontal())
            {
                Widgets.Header("Picture");
                GUILayout.FlexibleSpace();
                GUILayout.Label(Up > 0 || folders.Count > 0 ? "↑ ↓ ← →" : "↑ ↓", Theme.KeyHint, GUILayout.ExpandWidth(false));   // ContentTab steps the list (1.0.2: keys shown); ← → the folders
                // nothing picked yet: the list is all there is, Hide list grey (0.20.69, the author: until I have chosen a
                // picture, the Hide list button should be grey)
                GUI.enabled = picked || !browsing;
                bool open = Widgets.Toggle(browsing, browsing ? "Hide list" : "Browse pictures", Theme.Tab, Widgets.W(170));   // lit while it shows
                GUI.enabled = true;
                if (open != browsing) w.Later(() => browsing = open);
                if (browsing)                                               // with the list only (0.20.89)
                {
                    if (Widgets.Button("Reload", Theme.Button, Widgets.W(80))) w.Later(Reload);
                    if (Widgets.Button("Open folder", Theme.Button, Widgets.W(110))) PictureImporter.OpenFolder(inFolder);
                }
            }
            if (fromSign && !browsing)                                      // the sign's own picture, its file not here
            {
                GUILayout.Label($"{SignRecipe.Clean(signHeld.File)}{(known ? $"   {header.W} x {header.H}" : "")}   · on the sign", Theme.Label);
                Widgets.Hint(signError != null ? "No file here, unreadable: " + signError : "No file here: read from the sign");
            }
            else if (picked && !browsing)                                   // the pick in one line; the quick view shows it
            {
                string f = files[selected];
                Header ph = headers.TryGetValue(f, out Header got) ? got : null;
                bool own = s.Editing && s.Chosen.Recipe.Kind == SignRecipe.Kinds.Picture;
                GUILayout.Label($"{PictureImporter.Relative(f)}{(ph != null && ph.Error == null ? $"   {ph.W} x {ph.H}" : "")}" +
                                (own ? "   · on the sign" : ""), Theme.Label);
            }
            bool layout = Event.current.type == EventType.Layout, repaint = Event.current.type == EventType.Repaint;
            if (browsing)
            using (Widgets.Scroll(ref listScroll, 160))
            {
                if (files.Count == 0 && folders.Count == 0) Widgets.Hint(Up > 0 ? "No pictures in this folder" : "Empty: Open folder");
                int head = Up + folders.Count;                              // the up row and the folders come before the files (1.0.2)
                int at = cursor >= 0 ? cursor : selected >= 0 ? FileRow(selected) : -1;   // the one row highlighted: where the arrows
                                                                            // stand, else the pick (the owner: the highlight stayed on
                                                                            // the last picture while the arrows were on a folder)
                if (layout)                                                 // the rows in view only, their words made once (0.20.132)
                {
                    RowWords();
                    if (revealRow >= 0)                                     // an arrow's pick: scrolled into view
                    {
                        rows.Reveal(revealRow, ref listScroll.y, Widgets.U(Widgets.Tall(160)));
                        revealRow = -1;
                    }
                    rows.Plan(head + rowFiles.Count, listScroll.y, Widgets.U(Widgets.Tall(160)), rowFiles, Widgets.ContentWidth, Widgets.U(1f));
                }
                if (rows.Above > 0f) GUILayout.Space(rows.Above);
                for (int i = rows.First; i <= rows.Last && rowFiles != null && i < head + rowFiles.Count; i++)
                {
                    if (i < head)                                           // up, or a folder: a click or → opens it (Fold)
                    {
                        bool up = i < Up;
                        int row = i;
                        string into = up ? PictureImporter.DirOf(inFolder) : PictureImporter.Relative(folders[i - Up]);
                        string box = i == at ? (up ? "←" : "→") : up ? "up" : "folder";   // the key that opens it
                        if (Widgets.PictureItem(i == at, null, up ? ".." : Path.GetFileName(folders[i - Up]) + "/", Widgets.ContentWidth - 30, box, openWhenSelected: true))
                            w.Later(() =>
                            {
                                cursor = row;
                                Enter(into);
                            });
                    }
                    else
                    {
                        int pick = i - head;
                        string f = rowFiles[pick], key = rowKey[pick];
                        bool bad = rowBad[pick];
                        Texture2D t = layout ? Thumbs.Get(key, () => Thumbs.FromFile(f), now) : Thumbs.Peek(key);   // in view: marked used
                        GUI.enabled = !bad;
                        bool hit = Widgets.PictureItem(i == at, t, rowWords[pick], Widgets.ContentWidth - 30, t == null ? rowBox[pick] : "");
                        if (hit)
                            w.Later(() =>
                            {
                                Pick(s, pick);
                                browsing = false;                           // chosen: the list has done its job
                            });
                        GUI.enabled = true;
                    }
                    if (repaint)
                    {
                        Rect r = GUILayoutUtility.GetLastRect();
                        rows.Saw(i, r.y, r.yMax);
                    }
                }
                if (rows.Below > 0f) GUILayout.Space(rows.Below);
            }
            if (Widgets.Foldout("picture.view", "Quick view")) thumb.Draw(Widgets.ContentWidth - 30, 220);   // folds (0.20.171)
            GUI.enabled = can;

            Widgets.Header("Resolution", Widgets.WheelKey);                                   // its size in metres: 3 Place › Content size (0.20.59)
            using (Widgets.Vertical(Theme.Section))
            {
                // the Detail model (0.20.35, the author: only the density should affect the amount of data):
                // Detail, the pixels across, alone sets the data; Width and Height only size the pixels, for free
                int most = known ? MostDetail(header) : 2000;
                // the pixels the picture takes at this size (0.20.185, the author: the Resolution slider does not show that it
                // updates; the player's own Resolution 0.20.128-0.20.184): the one set stays in o.Detail and comes back with a
                // size that takes it; the Summary says it
                int detailNow = known ? Math.Max(1, Math.Min(plan.W > 0 ? plan.W : o.Detail, most)) : Math.Max(8, o.Detail);
                using (Widgets.Horizontal())                                // above the Resolution row (author, 2026-10-11)
                {
                    GUILayout.Label("", Theme.Label, Widgets.W(Widgets.LabelW));
                    // two buttons (0.20.162, the author: 2 buttons are enough, Native and Max (multiplayer); Light went): the most
                    // that fits a server's 300 KB (and the area's room), where a new picture starts, and every pixel
                    float bw = Widgets.RestW / 2f - 4f;
                    long target = MaxTarget(s);
                    GUI.enabled = can && known && target > 0;
                    if (Widgets.Button($"Max (multiplayer): {SaveCost.Label(target)}", Theme.SmallButton, Widgets.W(bw)))
                        w.Later(() =>
                        {
                            maxTarget = target;
                            maxSteps = 6;                                   // two or three counts get there; six at most
                            maxLastW = -1;
                            maxPressed = true;
                            maxNothing = false;
                            o.DetailAuto = false;
                        });
                    GUI.enabled = can && known && Math.Min(o.Detail, most) < most;   // the Resolution set (0.20.189, review)
                    if (Widgets.Button(!known || most >= header.W ? $"Native: {most} px" : $"Most: {most} px", Theme.SmallButton, Widgets.W(bw)))   // a million pixels at most (0.20.149)   // every pixel the file has
                        w.Later(() =>
                        {
                            o.Detail = most;
                            o.DetailAuto = false;
                            maxSteps = 0;
                        });
                    GUI.enabled = can;
                }
                // only where the density moves (0.20.133, the author: the slider should skip the dead stretches where the density
                // does not change, and round a number typed into the field): the slider, a typed number
                // and Shift + wheel go to the pixels a Resolution really makes at this size (PictureOptions.Achievable)
                if (snapDetail == null) snapDetail = v => known ? o.Achievable(Mathf.RoundToInt(v), header.W, header.H) : v;
                GUI.enabled = can && known;                                 // nothing picked: no Resolution to set (0.20.193, test)
                float d = Widgets.Number("pic.detail", "Resolution", detailNow, 8f, Math.Max(8, most), 4f, "px wide", "0", snap: snapDetail);   // the author: "Detail (pixel across)" ... I do not understand it
                GUI.enabled = can;
                if (maxNothing && known && plan.W == maxNothingW)
                    Widgets.Hint($"Max: {plan.W} px");
                int want = Mathf.Clamp(Mathf.RoundToInt(d), 1, Math.Max(1, most));
                if (want != detailNow && known)
                {
                    // the snap may land where the row stands or against the drag (0.20.149, review: from 200 at 2 m, a drag
                    // to 196 snapped to 213 and the thumb jumped right): then the next size that moves the drag's way
                    // a drag goes the mouse's way, and to the next size only once the mouse is past halfway to it (0.20.175,
                    // the author: a slow drag flickered: the next size stood ahead of the mouse, the next move read as one
                    // back and took the size before)
                    int slid = Widgets.Slid;
                    int dir = slid != 0 ? slid : Math.Sign(want - detailNow), here = o.Achievable(detailNow, header.W, header.H);
                    int got = o.Achievable(want, header.W, header.H);
                    // none bigger at this size (a small Width takes fewer pixels, 0.20.183): the row stays, and no search
                    // through every Resolution up to the most (0.20.189, review: each drag event stalled on it)
                    if (Math.Sign(got - here) != dir && dir > 0 && o.Achievable(Math.Max(1, most), header.W, header.H) <= here)
                        got = detailNow;
                    else if (Math.Sign(got - here) != dir)
                    {
                        int next = Mathf.RoundToInt(FineStep.Notch(here, -dir, 1f, "0", 1f, Math.Max(1, most), snapDetail));
                        got = slid != 0 && (want - (here + next) / 2f) * dir < 0 ? detailNow : next;
                    }
                    want = Mathf.Clamp(got, 1, Math.Max(1, most));
                }
                if (want != detailNow)
                {
                    maxNothing = false;
                    o.Detail = want;
                    o.DetailAuto = false;
                    maxSteps = 0;                                           // the player's own: Max stops
                }
                // cover 2: every pixel's glyph is drawn two pixels wide, so no gaps show between pixels from a distance
                o.Cover = Widgets.Check(o.Cover >= 2f, "Seamless pixels") ? 2f : 0f;
            }
            GUI.enabled = true;
            if (!can) Widgets.Hint(Source.NotReady(s));
            GUI.enabled = can && picked;                                    // before Summary (0.20.85, the author: swap the light
            LightRow.Draw(s, w, LightRow.PictureLights, LightRow.PictureNames);   // and the summary); a moving GIF's
                                                                            // screen follows it (0.20.3)
            // a GIF moves or goes on still, the picture's own choice as an art piece's (0.20.160, from Settings › Playback; the
            // author: okay, let it be so); the sign's own picture whose file is not here stays as it was made
            if (!fromSign && file != null && file.EndsWith(".gif", StringComparison.OrdinalIgnoreCase))
            {
                bool moving = Widgets.Check(o.Animate, "Moving");
                if (moving != o.Animate)
                    w.Later(() =>
                    {
                        o.Animate = moving;
                        PluginConfig.MovingGifs.Value = moving;             // the next GIF starts so
                    });
                // the other GIFs playing here share one budget (1.0.2, a friend's 35 MB GIF stayed still beside another GIF
                // and the row had said it moves): said before the put; frames already playing cost nothing more. Worked out
                // at the Layout before any row reads it (review: the hint below came at the Repaint alone, and the layout
                // threw on every GIF pick)
                if (Event.current.type == EventType.Layout) RoomForGif(s);   // once a frame: a file stat, the screens walked
                string motion = source != null && source.File == file ? source.Motion ?? gifMotion : null;   // read: what moving does (gifMotion: RoomForGif)
                if (o.Animate && motion != null) Widgets.Hint(motion);
                long budget = SignLink.Animation.ScreenPlayer.Budget;
                if (o.Animate && gifNeed > budget)
                    GUILayout.Label($"Too big to move: {gifNeed >> 20} MB of frames, at most {budget >> 20} MB; a lower Resolution helps", Theme.Warn);
                else if (o.Animate && gifNeed > 0 && !gifHeld && gifNeed > gifRoom + gifOwn)
                    GUILayout.Label($"Still for now: other GIFs here hold {(budget - gifRoom - gifOwn) >> 20} MB " +
                                    $"of {budget >> 20} MB; it needs {gifNeed >> 20} MB", Theme.Warn);
                if (o.Animate && gifNeed > 0)                               // the moving GIFs' memory with this one (1.0.2)
                    AreaMeter.Gifs(gifHeld ? 0 : Math.Max(0, gifNeed - gifOwn));
            }
            GUI.enabled = true;
            if (known)
            {
                // what it makes, one fact a row (author, 2026-10-11: break it into rows or a table ... unreadable),
                // under a title of its own (0.20.48)
                Widgets.Header("Summary");
                using (Widgets.Vertical(Theme.Section))
                {
                    float fw = Widgets.ContentWidth - 150;
                    double pxMm = plan.Pitch * 25;
                    bool guess = !Have && Estimated;                        // the last count's, scaled (0.20.125)
                    Widgets.Field("Signs", !Have && !guess ? "counting ..."                     // first (author, 2026-10-11)
                                         : cost.Pic.Bands.Count == 1 ? "1" : $"{cost.Pic.Bands.Count}, stacked", width: fw);
                    Widgets.Field("Pixels", $"{plan.W} x {plan.H}, from {(fromSign ? "the sign's " : "")}{header.W} x {header.H}" +
                                            (plan.W < header.W ? $" ({header.W / (double)plan.W:0.#} to one)" : " (every one)"), width: fw);
                    Widgets.Field("Size", $"{plan.WidthM:0.##} x {plan.HeightM:0.##} m, a pixel is " +
                                          (pxMm >= 10 ? $"{pxMm / 10:0.#} cm" : $"{pxMm:0.#} mm"), width: fw);
                    if (fineShift != 0)                                     // Fine Width (0.20.128)
                        Widgets.Field("", $"{fineShift:+0;-0} px for this size" + (fineLimited ? " (limit)" : ""), Theme.Small, fw);
                    if (showCoarsened)
                        Widgets.Field("", $"file is {header.W} px wide", Theme.Warn, fw);
                    if (showCapped)
                        Widgets.Field("", $"{PictureOptions.MaxPixels / 1000} k pixel limit", Theme.Warn, fw);
                    if (showCostError) Widgets.Field("Cost", costError ?? "error", Theme.Warn, fw);
                    else if (Have || guess)
                    {
                        long glyphs = Have ? cost.Glyphs : Est(cost.Glyphs), bytes = SaveBytes();
                        if (glyphs < plan.Pixels)
                            Widgets.Field("See-through", $"{plan.Pixels - glyphs} pixels, free", width: fw);
                        double build = Load(), down = SaveCost.DownloadSeconds(bytes), wait = build + down;
                        int level = SaveCost.Level(wait);
                        string limit = Limit(s);                            // multiplayer: the sign and area limits, no asking
                        Widgets.Field("Arriving", $"{wait:0.#} s ({down:0.#} s download, {build:0.#} s build)",
                                      level == 2 ? Theme.Warn : level == 1 ? Theme.Notice : Theme.Good, fw);
                        if (limit != null) Widgets.Field("Multiplayer", limit, Theme.Warn, fw);
                        else if (SignText.Shared()) Widgets.Field("Multiplayer", "fits the limits", Theme.Good, fw);
                        else Widgets.Field("Multiplayer", "no limit here", Theme.Small, fw);
                        Widgets.Field("World save", (guess ? "about " : "") + SaveCost.Label(bytes) +   // last (author, 2026-10-11)
                                                    (cost.Pic.Bands.Count > 1 && !guess ? $", the largest sign {SaveCost.Label(cost.Largest)}" : "") +
                                                    (guess ? " (counting ...)" : ""), width: fw);
                    }
                }
            }

            if (s.Editing && !fromSign && s.Held.Recipe.Kind == SignRecipe.Kinds.Picture)   // made with other Settings: Update applies today's
            {
                SignRecipe h = s.Held.Recipe;
                var made = new List<string>();
                if (h.Transparent != o.Transparent) made.Add(h.Transparent ? "with PNG transparency" : "without PNG transparency");
                if (made.Count > 0) Widgets.Hint($"Made {string.Join(" and ", made.ToArray())}");
            }

        }
    }
}
