using System;
using System.Collections.Generic;
using System.IO;
using SignLink.Api;
using SignLink.Pictures;
using SignLink.Signs;
using UnityEngine;

namespace SignLink.UI.Sources
{
    /// <summary>Picture: a PNG, JPG or GIF from BepInEx/config/SignLink/images onto the selected sign as saved sign text
    /// (as many stacked copies as it needs) that vanilla players see; a moving GIF also plays over it for SignLink
    /// players. Its Resolution (pixels wide: the data), its width in metres (the pixels' size, free), seamless pixels and
    /// its light are chosen here; PNG transparency and moving GIFs once, on Settings (Pictures); where
    /// it sits on 3 Place. The quick view shows the exact pixel grid. The folder's sizes are read on a worker;
    /// the picked file is decoded once (kept while the window is open), its grid and its sign texts are made on workers
    /// as the settings settle, and Place puts those very texts on: a photo costs one decode and one encode. A sign whose
    /// file is not on this computer (another player's picture) is edited from the sign itself (0.20.49, SignPicture):
    /// its pixels read back stand in for the decoded file; Resolution goes down only, Width is free.</summary>
    internal sealed class PictureSource : Source
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
        }

        /// <summary>The previous or next file of the list (a picture that cannot be read is skipped), picked as a click picks
        /// it; the list stays as it is (open or shut).</summary>
        public override bool Step(Session s, int dir)
        {
            if (files.Count == 0) return false;
            for (int k = selected < 0 ? (dir > 0 ? 0 : files.Count - 1) : selected + dir; k >= 0 && k < files.Count; k += dir)
            {
                if (rowBad != null && k < rowBad.Length && rowBad[k]) continue;
                Pick(s, k);
                revealRow = k;
                return true;
            }
            return false;
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
            files = PictureImporter.Files(withUnreadable: true);
            selected = keep == null ? -1 : files.IndexOf(keep);
            long folder = 0;
            try { folder = Directory.GetLastWriteTimeUtc(PictureImporter.Folder).Ticks; }
            catch (Exception) { }
            headerKey = folder + "|" + string.Join("\n", files.ToArray());
            headerJob.Forget();                                             // the same names may hold other bytes now
        }

        int IndexOf(string fileName) =>
            string.IsNullOrEmpty(fileName) ? -1 : files.FindIndex(f => string.Equals(Path.GetFileName(f), fileName, StringComparison.OrdinalIgnoreCase));

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

        /// <summary>The rows take the choice slot's recipe when the session switched slots (a sign holding a picture was
        /// selected, or one that holds none and the player's own choice is back): its settings into the options, its
        /// file picked. The player's own settings and pick are kept while a sign's own show, and come back with the
        /// own slot (a sign holding nothing, or something no source knows). PNG transparency and
        /// moving GIFs come from Settings (Tick) whatever the recipe says: a picture made otherwise shows Update.</summary>
        void Take(Session s)
        {
            maxSteps = 0;                                                   // Max was for the last choice (0.20.130, review)
            if (shownOwn && s.Editing)                                      // leaving the player's own choice: kept
            {
                ownOptions = o.Clone();
                ownFile = selected >= 0 && selected < files.Count ? files[selected] : null;
            }
            shownFor = s.Chosen;
            shownOwn = !s.Editing;
            browsing = false;                                               // a sign's own picture, or the player's pick back: the list waits
            bool restored = false;
            if (!s.Editing && ownOptions != null)                           // back to it: as it was
            {
                o = ownOptions;
                ownOptions = null;
                selected = ownFile != null ? files.IndexOf(ownFile) : -1;
                restored = true;
            }
            SignRecipe r = s.Chosen.Recipe;
            LeaveSign();
            if (r.Kind != SignRecipe.Kinds.Picture)
            {
                if (s.Editing) selected = -1;                               // a sign holding something else: no pick of
                return;                                                     // before on it (0.20.89); the own one is kept
            }
            if (!restored)
            {
                r.Apply(o);                                                 // restored: the player's Resolution, not the plan's pixels
                checkDetail = r.Detail > 0;
            }
            int i = IndexOf(r.File);
            if (i < 0)
            {
                Relist();
                i = IndexOf(r.File);
            }
            selected = i;
            if (i < 0 && s.Editing)                                         // its file is not here: the sign's own pixels
            {
                fromSign = true;
                signHeld = r.Copy();
                signFileCols = r.Pitch > 0 ? (int)Math.Round(r.WidthM * 40 / r.Pitch) : 0;
                signError = null;
                source = null;
                decodeJob.Abandon();                                        // a file still decoding must not land here
                thumbFile = null;                                           // another sign's quick view goes at once
            }
        }

        /// <summary>Out of sign mode (another choice, a file picked from the list): a read still running is dropped,
        /// so it cannot land on the next choice's settings.</summary>
        void LeaveSign()
        {
            fromSign = false;
            keptHashSet = false;                                            // another choice: its own settings
            signJob.Abandon();
            signRead = null;
            signHeader = null;
        }

        /// <summary>The choice's pose and place into the options. The sign's own picture keeps where its canvas's middle
        /// sits from the fit's when its box could not be padded (SignPicture.Canvas), along the face as the chosen tilt
        /// lays it.</summary>
        void ApplyFit(Fit f)
        {
            o.TiltDeg = (float)f.Tilt;
            o.TurnDeg = (float)f.Turn;
            o.EdgePivot = f.Edge;
            double right = f.Right, up = f.Up, outward = f.Out;
            if (fromSign && signRead != null && (signRead.ShiftR != 0 || signRead.ShiftU != 0))
            {
                double a = f.Tilt * Math.PI / 180;
                right += signRead.ShiftR;
                up += signRead.ShiftU * Math.Cos(a);
                outward -= signRead.ShiftU * Math.Sin(a);
            }
            o.RightM = (float)(right / 40);
            o.UpM = (float)(up / 40);
            o.OutM = (float)(outward / 40);
        }

        /// <summary>The sign's picture read back once the stack is loaded (a worker), again whenever it changes: its
        /// canvas stands in for the decoded file (the grid, the cost and Update work from it), at every pixel the sign
        /// holds and the width it shows.</summary>
        void ReadSign(Session s, float now)
        {
            Sign root = s.Stack.Count > 0 ? s.Stack[0] : null;
            string key = root && s.Loaded && !s.Hidden ? s.StackKey : null;   // nothing of a text the game hides (0.20.132)
            if (signJob.Wants(key, now, 0f)) StartRead(s, root, key, now);  // the delegates only when work starts (0.20.132)
        }

        void StartRead(Session s, Sign root, string key, float now)
        {
            signJob.Want(key, now, 0f,
                () =>
                {
                    List<string> texts = SignBuilder.Content(root);
                    Fit held = s.Held.Fit;
                    SignRecipe recipe = s.Held.Recipe.Copy();
                    held.Centre(0, 0, out double fx, out double fy);         // where the fit puts a picture's middle
                    thumb.Info = "reading the picture from the sign ...";
                    return () =>
                    {
                        if (!SignPicture.Read(texts, fx, fy, out SignPicture p, out string why)) throw new InvalidOperationException(why);
                        Fit.TurnPoint(p.ShiftX, p.ShiftY, held.Turn, out double r, out double u);
                        return new FromSign { P = p, ShiftR = r, ShiftU = u, Held = recipe, Hash = SignPicture.TextsHash(texts) };
                    };
                },
                got =>
                {
                    if (!fromSign) return;                                  // left meanwhile (LeaveSign drops it too)
                    bool same = (signRead != null && signRead.Hash == got.Hash) || (keptHashSet && keptHash == got.Hash);   // moved, not changed
                    keptHash = got.Hash;
                    keptHashSet = true;
                    if (got.Held.Kind == SignRecipe.Kinds.Picture && got.Held.SameItem(signHeld))
                    {
                        signHeld = got.Held;                                // made again since (an Update here, or another
                        signFileCols = signHeld.Pitch > 0 ? (int)Math.Round(signHeld.WidthM * 40 / signHeld.Pitch) : 0;   // player's)
                    }
                    signRead = got;
                    signError = null;
                    signHeader = new Header { W = got.P.W, H = got.P.H, Stamp = signJob.Done };
                    source = new Decoded { File = SignFile, Stamp = signJob.Done, Rgba = got.P.Rgba, W = got.P.W, H = got.P.H,
                                           SrcW = got.P.W, SrcH = got.P.H };
                    signNativeM = (float)(got.P.W * got.P.Pitch / 40);        // as it is: every pixel, the width it shows
                    if (same) return;                                       // the player's Resolution and Width stay
                    o.Detail = got.P.W;
                    o.DetailAuto = false;
                    o.WidthM = signNativeM;
                    maxSteps = 0;
                },
                e =>
                {
                    if (!fromSign) return;
                    signRead = null;
                    signHeader = null;
                    signError = e.Message;
                    thumb.Clear();
                    thumb.Info = "no quick view: " + e.Message;
                });
        }

        /// <summary>The sign's picture as it is (every pixel, the width it shows, the same seamlessness and light): its
        /// own recipe, so the button says As it is (or moves it) and nothing is made again.</summary>
        bool AsOnSign() => signRead != null && plan.W == signRead.P.W && Math.Abs(o.WidthM - signNativeM) < 1e-4f &&
                           Math.Abs(o.Cover - signHeld.Cover) < 1e-4f && o.Glow == signHeld.Glow;

        /// <summary>What Update writes for the sign's picture made again from its own pixels: these settings, its file's
        /// name and how it was made (see-through, moving: whoever has the file still sees it move), the columns counted
        /// on the file's full width (SignPicture.FileColumns), so the file makes it again at the same size.</summary>
        SignRecipe SignRecipeFor(PicturePlan p)
        {
            PictureOptions made = o.Clone();
            made.Transparent = signHeld.Transparent;
            made.Animate = signHeld.Animate;
            SignRecipe r = SignRecipe.ForPicture(made, p, signHeld.File);
            r.WidthM = signRead.P.RecipeColumns(p.W, p.Pitch, signFileCols) * p.Pitch / 40f;
            return r;
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
                name = file != null ? Path.GetFileName(file) : "";
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

        /// <summary>Each file's row: its words (name, size or why it cannot be read, moving), its thumbnail's key, the words
        /// of its empty box (a photo too large to decode for its row alone) and whether it can be picked.</summary>
        void RowWords()
        {
            if (ReferenceEquals(rowFiles, files) && rowHeaders == headersChanged) return;
            rowFiles = files;
            rowHeaders = headersChanged;
            int n = files.Count;
            rowWords = new string[n];
            rowKey = new string[n];
            rowBox = new string[n];
            rowBad = new bool[n];
            for (int i = 0; i < n; i++)
            {
                string f = files[i];
                Header h = headers.TryGetValue(f, out Header hh) ? hh : null;
                bool bad = h != null && h.Error != null;
                string size = h == null ? "" : bad ? "   " + h.Error : $"   {h.W} x {h.H}";
                bool gif = f.EndsWith(".gif", StringComparison.OrdinalIgnoreCase);
                long px = h != null && !bad ? (long)h.W * h.H : 0;
                rowWords[i] = $"{Path.GetFileName(f)}{size}{(gif && !bad ? "   moving" : "")}";
                rowKey[i] = h != null && !bad ? f + "|" + h.Stamp : null;
                rowBox[i] = px > Thumbs.MaxDecodePixels ? $"{px / 1000000} MP" : "";
                rowBad[i] = bad;
            }
        }

        // PlanFor's inputs when plan, fineShift and fineLimited were made last (0.20.132, review: the plan's pitch search,
        // with its string work, and three keys with their numbers written out, every Layout on every tab)
        int planSrcW = -1, planSrcH, planDetail, planFineCap, planPixels;
        float planWidth, planPitch;
        bool planFine;

        /// <summary>The same float, to the sign of a zero (a key writes -0 and 0 apart); NaN is NaN.</summary>
        static bool Same(float a, float b) => a == b ? a != 0f || 1f / a == 1f / b : float.IsNaN(a) && float.IsNaN(b);

        /// <summary>The plan for the header and the options (PictureOptions.PlanFor: a function of exactly these inputs)
        /// and what Fine Width made of the Resolution, made again only when one of them changed.</summary>
        void Plan()
        {
            if (!known)
            {
                plan = default;
                fineShift = 0;
                fineLimited = false;
                planSrcW = -1;                                              // made again when it is known again
                return;
            }
            if (planSrcW == header.W && planSrcH == header.H && planDetail == o.Detail && Same(planWidth, o.WidthM) && planFine == o.Fine &&
                planFineCap == o.FineCap && Same(planPitch, o.Pitch) && planPixels == o.Pixels) return;
            planSrcW = header.W;
            planSrcH = header.H;
            planDetail = o.Detail;
            planWidth = o.WidthM;
            planFine = o.Fine;
            planFineCap = o.FineCap;
            planPitch = o.Pitch;
            planPixels = o.Pixels;
            plan = o.PlanFor(header.W, header.H);
            // a new size with the same pixels makes no new grid: its caption follows here (0.20.191, test: it kept the old
            // metres after a Width change with Fine Width)
            if (thumb.Has && thumb.Texture.width == plan.W && thumb.Texture.height == plan.H) thumb.Info = Caption(plan);
            int most = MostDetail(header), chosen = Math.Min(o.Detail, most);   // Fine Width: the pixels the size took
            fineShift = o.Fine && !plan.Coarsened && !plan.Capped ? plan.W - chosen : 0;
            // the cap held it, not the size's own fewer pixels (0.20.189, review)
            int fineWant = PictureOptions.FineDetail(o.WidthM, chosen, most, out _);
            fineLimited = fineShift != 0 && o.FineCap > 0 && fineWant > plan.W && fineWant > Math.Max(o.FineCap, chosen) &&
                          fineWant - chosen <= Math.Max(2, chosen * PictureOptions.FineSpread);
        }

        static string Caption(PicturePlan p) => $"What the signs will show: {p.W} x {p.H} pixels, {p.WidthM:0.##} x {p.HeightM:0.##} m";

        // the keys' inputs when they were made last
        string keyFile, keyStamp, keyDecode, keyWant;
        int keyW, keyH;
        bool keyTransparent, keyGlow;
        float keyPitch, keyCover, keyRight, keyUp, keyOut, keyMaxKb, keyTilt, keyTurn;

        /// <summary>decodeKey, wantKey and optionsKey for the file, the plan and the options, each written out again only
        /// when what it is made of changed (the very strings otherwise).</summary>
        void Keys()
        {
            if (!known) decodeKey = null;
            else if (decodeKey == null || file != keyFile || header.Stamp != keyStamp)
            {
                keyFile = file;
                keyStamp = header.Stamp;
                decodeKey = file + "|" + header.Stamp;
            }
            if (!decoded) wantKey = null;
            else if (wantKey == null || decodeKey != keyDecode || plan.W != keyW || plan.H != keyH || o.Transparent != keyTransparent)
            {
                keyDecode = decodeKey;
                keyW = plan.W;
                keyH = plan.H;
                keyTransparent = o.Transparent;
                wantKey = $"{decodeKey}|{plan.W}x{plan.H}|{o.Transparent}";
            }
            if (wantKey == null) optionsKey = null;
            else if (optionsKey == null || wantKey != keyWant || !Same(plan.Pitch, keyPitch) || !Same(o.Cover, keyCover) || !Same(o.RightM, keyRight) ||
                     !Same(o.UpM, keyUp) || !Same(o.OutM, keyOut) || !Same(o.MaxKb, keyMaxKb) || o.Glow != keyGlow ||
                     !Same(o.TiltDeg, keyTilt) || !Same(o.TurnDeg, keyTurn))
            {
                keyWant = wantKey;
                keyPitch = plan.Pitch;
                keyCover = o.Cover;
                keyRight = o.RightM;
                keyUp = o.UpM;
                keyOut = o.OutM;
                keyMaxKb = o.MaxKb;
                keyGlow = o.Glow;
                keyTilt = o.TiltDeg;
                keyTurn = o.TurnDeg;
                optionsKey = $"{wantKey}|{plan.Pitch}|{o.Cover}|{o.RightM}|{o.UpM}|{o.OutM}|{o.MaxKb}|{o.Glow}|{o.TiltDeg}|{o.TurnDeg}";
            }
        }

        static Dictionary<string, Header> ReadHeaders(string[] list)
        {
            var got = new Dictionary<string, Header>();
            foreach (string f in list)
            {
                var h = new Header { Stamp = Stamp(f) ?? "" };
                try
                {
                    if (!PictureImporter.IsReadable(f) || !ImageHeader.ImageSize(f, out h.W, out h.H)) h.Error = Unreadable;
                }
                catch (Exception) { h.Error = Unreadable; }
                got[f] = h;
            }
            return got;
        }

        /// <summary>Decodes the picked file once it has been the pick for a moment: the bytes are read on a worker, the
        /// game decodes them (one copy of the texture's bytes, the only main-thread part), a worker makes the RGBA rows,
        /// halved as Grid would first (a 50 MP photo is held as about 50 MB), turned as the EXIF orientation says.</summary>
        void Decode(float now)
        {
            if (!decodeJob.Wants(decodeKey, now, 0.3f)) return;             // the delegates only when work starts (0.20.132)
            decodeJob.WantStoppable(decodeKey, now, 0.3f,
                () =>
                {
                    string f = file, stamp = header.Stamp;
                    thumb.Info = (long)header.W * header.H > LargePhoto ? "reading a large photo ..." : $"reading {name} ...";
                    readErrorKey = null;
                    return stale => DecodeFile(f, stamp, stale);
                },
                d =>
                {
                    if (fromSign) return;                                   // the sign's own picture is the pick now
                    source = d;
                    if (headers.TryGetValue(d.File, out Header h) && (h.W != d.SrcW || h.H != d.SrcH))
                    {
                        h.W = d.SrcW;                                       // a header that lies: one plan everywhere
                        h.H = d.SrcH;
                        headersChanged++;                                   // its row says the size it has
                    }
                },
                e =>
                {
                    readError = "cannot read: " + e.Message;
                    readErrorKey = decodeJob.Done;
                    thumb.Info = "no quick view: " + e.Message;
                });
        }

        /// <summary>Worker: the file to a Decoded (the game decodes PNG and JPG on the main thread, GIFs decode here).
        /// stale (the pick, or the window, moved on): asked before the main thread's decode is queued and again when it
        /// would run (0.20.132, review: a 24-50 MP photo froze the game for a second after the window had closed).</summary>
        static Decoded DecodeFile(string file, string stamp, Func<bool> stale)
        {
            byte[] bytes = File.ReadAllBytes(file);
            var d = new Decoded { File = file, Stamp = stamp };
            if (GifDecoder.IsGif(bytes))
            {
                GifImage g = GifDecoder.Decode(bytes, maxFrames: 1, cancelled: stale);   // its first frame
                d.Rgba = PictureImporter.GifFrameRgba(g.Frames[0]);
                d.W = d.SrcW = g.Width;
                d.H = d.SrcH = g.Height;
                for (int n = RawPixels.Halvings(d.W, d.H, 4 * PictureOptions.MaxPixels); n > 0; n--)
                    d.Rgba = RawPixels.Halve(d.Rgba, ref d.W, ref d.H);
                return d;
            }
            int orientation = PictureImporter.Orientation(bytes);
            if (stale()) throw new OperationCanceledException("no longer wanted");
            PictureImporter.RawTexture raw = MainThread.Run(() => stale() ? null : PictureImporter.ReadRaw(bytes));
            if (raw == null) throw new OperationCanceledException("no longer wanted");
            bytes = null;
            d.Rgba = RawPixels.ToRgba(raw.Bytes, raw.Format, raw.W, raw.H, RawPixels.Halvings(raw.W, raw.H, 4 * PictureOptions.MaxPixels),
                                      out d.W, out d.H);
            d.SrcW = raw.W;
            d.SrcH = raw.H;
            raw = null;
            d.Rgba = ImageHeader.Orient(d.Rgba, d.W, d.H, orientation, out d.W, out d.H);
            if (orientation >= 5)
            {
                int t = d.SrcW;
                d.SrcW = d.SrcH;
                d.SrcH = t;
            }
            return d;
        }

        /// <summary>The quick view's grid for the decoded picture and the plan, once they have stood still for a moment.</summary>
        void MakeGrid(float now)
        {
            if (!gridJob.Wants(wantKey, now, 0.3f)) return;
            gridJob.WantStoppable(wantKey, now, 0.3f,
                () =>
                {
                    Decoded d = source;
                    PictureOptions opts = o.Clone();
                    string key = wantKey;
                    thumb.Info = "drawing the quick view ...";
                    return stale =>                                         // a grid nobody waits for stops (0.20.132)
                    {
                        PicturePlan p = opts.PlanFor(d.SrcW, d.SrcH);
                        int[] px = PictureImporter.Grid(d.Rgba, d.W, d.H, p, !opts.Transparent, stale);   // the very grid Place encodes
                        return new Gridded { Px = px, Rows = Thumb.Rows(px, p.W, p.H), Plan = p, Key = key };
                    };
                },
                g =>
                {
                    thumb.Set(g.Plan.W, g.Plan.H, new[] { g.Rows }, 0);
                    gridPx = g.Px;
                    gridKey = g.Key;
                    thumb.Info = Caption(g.Plan);
                },
                e =>
                {
                    thumb.Info = "no quick view: " + e.Message;
                    gridError = e.Message;                                  // these settings: said by Problem (0.20.130, review:
                    gridErrorKey = gridJob.Done;                            // "Counting ..." for ever)
                });
        }

        /// <summary>The sign texts for the grid and the options, once they have stood still for a moment: their world-save
        /// size for the cost line, and the texts themselves for Place.</summary>
        void Count(float now)
        {
            // half a second still: a few Shift + wheel notches or a drag make one count, not one a pause (0.20.125)
            if (!costJob.Wants(costKey, now, 0.5f)) return;
            costJob.WantStoppable(costKey, now, 0.5f,
                () =>
                {
                    int[] px = gridPx;
                    PictureOptions opts = o.Clone();
                    PicturePlan p = plan;
                    string nm = name, stamp = header.Stamp, f = file;
                    int sw = header.W, sh = header.H;
                    if (px == null || px.Length != (long)p.W * p.H) throw new InvalidOperationException("the quick view is not this grid");
                    return stale =>                                         // a count nobody waits for stops (0.20.132)
                    {
                        EncodedPicture pic = PictureImporter.EncodeGrid(px, p, opts, stale);
                        pic.Name = nm;
                        pic.SourceW = sw;
                        pic.SourceH = sh;
                        int glyphs = 0;
                        foreach (int v in px) if (PixelText.Drawn(v)) glyphs++;
                        return new Costed { Pic = pic, Bytes = pic.SaveBytes, Largest = pic.MaxBytes, Glyphs = glyphs, Stamp = stamp,
                                            File = f, Pixels = p.Pixels };
                    };
                },
                c =>
                {
                    cost = c;
                    costFor = costJob.Done;
                    costError = null;
                },
                e =>
                {
                    cost = null;
                    costFor = costJob.Done;
                    costError = e.Message;
                });
        }

        bool Have => counted && costError == null && cost != null;

        /// <summary>The quick view's grid failed for these very settings (out of memory on a huge photo, say).</summary>
        string gridError, gridErrorKey;
        bool GridFailed => wantKey != null && gridErrorKey == wantKey;

        /// <summary>The size or the Resolution moved since this very picture was counted, and the new count is still to
        /// come: the last count scaled to the pixels now (Est) stands in for it, so the button and the rows stay as they
        /// are instead of going grey and lighting up again after every step (0.20.125, the author: the count runs 3-4 times
        /// or keeps toggling the button below, which flashes yellow ... let us make a smooth solution).</summary>
        bool Estimated => !counted && known && cost != null && costError == null && cost.Pixels > 0 &&
                          cost.File == file && cost.Stamp == header.Stamp && !GridFailed;

        /// <summary>A number of the last count (bytes, glyphs) for the pixels the plan has now (Estimated).</summary>
        long Est(long n) => SaveCost.Scaled(n, cost.Pixels, plan.Pixels);

        /// <summary>The bytes it stores: counted, else estimated (Estimated), else -1.</summary>
        long SaveBytes() => Have ? cost.Bytes : Estimated ? Est(cost.Bytes) : -1;

        /// <summary>Seconds every player's game spends building it: its counted glyphs (see-through pixels are none),
        /// estimated while a new count comes, else every pixel of the plan.</summary>
        double Load() => Have ? SaveCost.LoadSeconds(cost.Glyphs) : Estimated ? SaveCost.LoadSeconds(Est(cost.Glyphs)) : plan.LoadSeconds;

        /// <summary>The button pressed while the numbers were estimated: it goes on once they are counted (the texts Place
        /// writes, the limits checked on the counted bytes), the button saying "Working ..." meanwhile (Busy).</summary>
        Action pendingAct;
        SignLinkWindow pendingIn;

        void Pending()
        {
            if (pendingAct == null || Estimated) return;
            Action act = pendingAct;
            pendingAct = null;
            pendingIn = null;
            act();
        }

        /// <summary>The picture or the sign changed under a pending press: nothing goes on, the window stays open.</summary>
        void DropPending()
        {
            pendingIn?.KeepOpen();
            pendingAct = null;
            pendingIn = null;
        }

        public override void Draw(Session s, SignLinkWindow w, float now)
        {
            bool can = Source.Ready(s);                                     // the light and the size are set for a sign
            bool picked = fromSign || selected >= 0 && selected < files.Count;
            if (!picked && !browsing && Event.current.type == EventType.Layout) browsing = true;   // nothing to show: the list
            using (Widgets.Horizontal())
            {
                Widgets.Header("Picture");
                GUILayout.FlexibleSpace();
                // nothing picked yet: the list is all there is, Hide list grey (0.20.69, the author: until I have chosen a
                // picture, the Hide list button should be grey)
                GUI.enabled = picked || !browsing;
                bool open = Widgets.Toggle(browsing, browsing ? "Hide list" : "Browse pictures", Theme.Tab, Widgets.W(170));   // lit while it shows
                GUI.enabled = true;
                if (open != browsing) w.Later(() => browsing = open);
                if (browsing)                                               // with the list only (0.20.89)
                {
                    if (Widgets.Button("Reload", Theme.Button, Widgets.W(80))) w.Later(Reload);
                    if (Widgets.Button("Open folder", Theme.Button, Widgets.W(110))) PictureImporter.OpenFolder();
                }
            }
            if (fromSign && !browsing)                                      // the sign's own picture, its file not here
            {
                GUILayout.Label($"{SignRecipe.Clean(signHeld.File)}{(known ? $"   {header.W} x {header.H}" : "")}   · on the selected sign", Theme.Label);
                Widgets.Hint(signError != null ? "Its file is not on this computer, and the sign's picture cannot be read back: " + signError
                             : "Its file is not on this computer: read back from the sign. Resolution goes down only (Native is every pixel " +
                               "the sign holds), Width is free; a moving GIF stays still here (whoever has its file still sees it move).");
            }
            else if (picked && !browsing)                                   // the pick in one line; the quick view shows it
            {
                string f = files[selected];
                Header ph = headers.TryGetValue(f, out Header got) ? got : null;
                bool own = s.Editing && s.Chosen.Recipe.Kind == SignRecipe.Kinds.Picture;
                GUILayout.Label($"{Path.GetFileName(f)}{(ph != null && ph.Error == null ? $"   {ph.W} x {ph.H}" : "")}" +
                                (own ? "   · on the selected sign" : ""), Theme.Label);
            }
            bool layout = Event.current.type == EventType.Layout, repaint = Event.current.type == EventType.Repaint;
            if (browsing)
            using (Widgets.Scroll(ref listScroll, 160))
            {
                if (files.Count == 0) Widgets.Hint("No pictures yet: put PNG, JPG or GIF files in the images folder (Open folder), then Reload.");
                if (layout)                                                 // the rows in view only, their words made once (0.20.132)
                {
                    RowWords();
                    if (revealRow >= 0)                                     // an arrow's pick: scrolled into view
                    {
                        rows.Reveal(revealRow, ref listScroll.y, Widgets.U(Widgets.Tall(160)));
                        revealRow = -1;
                    }
                    rows.Plan(rowFiles.Count, listScroll.y, Widgets.U(Widgets.Tall(160)), rowFiles, Widgets.ContentWidth, Widgets.U(1f));
                }
                if (rows.Above > 0f) GUILayout.Space(rows.Above);
                for (int i = rows.First; i <= rows.Last && rowFiles != null && i < rowFiles.Count; i++)
                {
                    string f = rowFiles[i], key = rowKey[i];
                    bool bad = rowBad[i];
                    Texture2D t = layout ? Thumbs.Get(key, () => Thumbs.FromFile(f), now) : Thumbs.Peek(key);   // in view: marked used
                    int pick = i;
                    GUI.enabled = !bad;
                    bool hit = Widgets.PictureItem(i == selected, t, rowWords[i], Widgets.ContentWidth - 30, t == null ? rowBox[i] : "");
                    if (repaint)
                    {
                        Rect r = GUILayoutUtility.GetLastRect();
                        rows.Saw(i, r.y, r.yMax);
                    }
                    if (hit)
                        w.Later(() =>
                        {
                            Pick(s, pick);
                            browsing = false;                               // chosen: the list has done its job
                        });
                    GUI.enabled = true;
                }
                if (rows.Below > 0f) GUILayout.Space(rows.Below);
            }
            if (Widgets.Foldout("picture.view", "Quick view")) thumb.Draw(Widgets.ContentWidth - 30, 220);   // folds (0.20.171)
            GUI.enabled = can;

            Widgets.Header("Resolution");                                   // its size in metres: 3 Place › Content size (0.20.59)
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
                    Widgets.Hint($"Max: {plan.W} px is already the most that fits {SaveCost.Label(maxTarget)} at this size.");
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
                o.Cover = Widgets.Check(o.Cover >= 2f, "Seamless pixels: no gaps between them from afar (costs nothing)") ? 2f : 0f;
            }
            GUI.enabled = true;
            if (!can) Widgets.Hint(Source.NotReady(s));
            Widgets.Hint("Resolution (pixels wide) sets the cost: half the resolution is a quarter of the data. The size " +
                         "(3 Place › Content › Size) is free; with Fine Width steps, this row moves to the resolutions that size takes. " +
                         "PNG transparency: Settings › Pictures.");
            GUI.enabled = can && picked;                                    // before Summary (0.20.85, the author: swap the light
            LightRow.Draw(s, w, LightRow.PictureLights, LightRow.PictureNames, null);   // and the summary); a moving GIF's
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
                Widgets.Hint(o.Animate ? "On: it moves for every SignLink player who has the same GIF file; players without it see its first frame."
                                       : "Off: it goes on as a still picture, its first frame.");
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
                        Widgets.Field("", $"{fineShift:+0;-0} px from the Resolution you set ({plan.W - fineShift}) for exactly this size" +
                                          (fineLimited ? ", fewer to stay within the limit" : ""), Theme.Small, fw);
                    if (showCoarsened)
                        Widgets.Field("", $"only {header.W} pixels wide: Resolution stops there (a picture is never enlarged)", Theme.Warn, fw);
                    if (showCapped)
                        Widgets.Field("", $"over the {PictureOptions.MaxPixels / 1000} k pixel limit: Resolution stops at {plan.W}", Theme.Warn, fw);
                    if (showCostError) Widgets.Field("Cost", "these settings do not work: " + (costError ?? ""), Theme.Warn, fw);
                    else if (Have || guess)
                    {
                        long glyphs = Have ? cost.Glyphs : Est(cost.Glyphs), bytes = SaveBytes();
                        if (glyphs < plan.Pixels)
                            Widgets.Field("See-through", $"{plan.Pixels - glyphs} pixels, free", width: fw);
                        double build = Load(), down = SaveCost.DownloadSeconds(bytes), wait = build + down;
                        int level = SaveCost.Level(wait);
                        string limit = Limit(s);                            // multiplayer: the sign and area limits, no asking
                        Widgets.Field("Arriving", $"about {wait:0.#} s on a server: {down:0.#} s download, {build:0.#} s build" +
                                                  (limit == null && level == 2 ? " (very large: the button asks first)" : ""),
                                      level == 2 ? Theme.Warn : level == 1 ? Theme.Notice : Theme.Good, fw);
                        if (limit != null) Widgets.Field("Multiplayer", "the button on 3 Place stays grey: " + limit, Theme.Warn, fw);
                        else if (SignText.Shared()) Widgets.Field("Multiplayer", "fits the limits", Theme.Good, fw);
                        else Widgets.Field("Multiplayer", $"no limit in single player (on a server: {SaveCost.Label(SaveCost.SignLimit)} a sign, " +
                                                          $"{SaveCost.Label(SaveCost.AreaLimit)} within {SaveCost.AreaRadiusM} m)", Theme.Small, fw);
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
                if (made.Count > 0) Widgets.Hint($"Made {string.Join(" and ", made.ToArray())}; Update makes it as Settings › Pictures says now.");
            }

        }

        public override bool Size(Session s, out Vector2 metres)
        {
            metres = known ? new Vector2(plan.WidthM, plan.HeightM) : Vector2.zero;
            if (known && Screened(s)) metres += Vector2.one * (2f * plan.Pitch / 40f);   // a pixel more on every side
            return known;
        }

        /// <summary>Does this picture show on a screen (a moving GIF: PictureImporter.PlayOver), a pixel larger on every
        /// side than its still? A picked GIF while moving GIFs are on; the sign's own when one plays over it now (0.20.116,
        /// the author: the frame was a pixel short of the GIF all round).</summary>
        bool Screened(Session s)
        {
            if (fromSign) return Plugin.Screens.ScreenOver(s.Stack.Count > 0 ? s.Stack : new List<Sign> { s.Target }) != null;
            return o.Animate && file != null && file.EndsWith(".gif", StringComparison.OrdinalIgnoreCase);
        }

        Func<float, float> snapWidth, snapHeight;

        /// <summary>Fine Width steps (Settings › Pictures): a picture's size is the size asked for, to half a pixel, dragged
        /// and typed too (0.20.126); the plan takes the pixels nearest the Resolution that make it (PictureOptions.Fine,
        /// 0.20.128). Off, the next size it takes for the same data (Planned).</summary>
        bool FineSteps() => PluginConfig.PictureFineWidth.Value && known && plan.W > 0 && plan.Pitch > 0;

        /// <summary>A Shift + wheel notch with Fine Width: the next size up or down, about a pixel (0.5-4 cm), the Resolution
        /// kept (0.20.128, the author: when I edit and lower the size, its density gets very bad: a pixel
        /// more or less at the same pixel size, 0.20.122-0.20.127, took the Resolution away with the size).</summary>
        void Finer(int dir)
        {
            float next = NextWidth(dir);
            if (!float.IsNaN(next)) o.WidthM = next;
        }

        /// <summary>The width Finer goes to that way, NaN at the end of the range.</summary>
        float NextWidth(int dir)
        {
            float now = plan.WidthM, px = plan.Pitch / 40f;
            // the fewest pixels' worth away that the plan takes as a new size: about one where the size is fine, the next
            // step where it steps (PictureOptions.FineSpread); doubled until it moves, then halved back to the nearest
            int lo = 0, hi = 1;
            while (hi <= 4096 && !Moves(now + dir * hi * px, now, dir))
            {
                lo = hi;
                hi *= 2;
            }
            if (hi > 4096) return float.NaN;                                // at the end of the range
            while (hi - lo > 1)
            {
                int mid = (lo + hi) / 2;
                if (Moves(now + dir * mid * px, now, dir)) hi = mid;
                else lo = mid;
            }
            return Mathf.Clamp(now + dir * hi * px, 0.25f, 60f);
        }

        /// <summary>Does a picture asked to be m metres wide take a size past now, in the direction dir?</summary>
        bool Moves(float m, float now, int dir)
        {
            PictureOptions probe = o.Clone();
            probe.WidthM = Mathf.Clamp(m, 0.25f, 60f);
            float got = probe.PlanFor(header.W, header.H).WidthM;
            return dir > 0 ? got > now + 1e-4f : got < now - 1e-4f;
        }

        /// <summary>Fine Width's cap (0.20.128, the author: as I set the size the KB grows and I cannot save it): a size
        /// may take a few pixels more than the Resolution, never so many that this picture's counts say it goes over what
        /// Max aims at (MaxTarget: the multiplayer limits, 2 % short); never under the Resolution itself (the player's;
        /// over the limit, Limit says so). 0 before a count. Kept for the picture, its Resolution and the aim: each count
        /// is taken in once and may only lower it (PictureOptions.NextCap, 0.20.134: made fresh from every count, the cap
        /// and the plan fed each other, 109, 110, 109, ... for good, and a press waiting for the last count never went on).</summary>
        int FineCapFor(Session s)
        {
            if (!o.Fine || !known || !SignText.Shared()) return 0;          // single player: no multiplayer limit (0.20.149, review)
            int own = Math.Min(o.Detail, MostDetail(header));
            long target = MaxTarget(s);
            if (target <= 0) return Math.Max(1, own);                       // the area is full: no pixel more than the Resolution
            if (capFile != file || capStamp != header.Stamp || capOwn != own || capTarget != target || capTransparent != o.Transparent ||
                capGlow != o.Glow || !Same(capCover, o.Cover))
            {
                capFile = file;                                             // another picture, Resolution or aim: found again
                capStamp = header.Stamp;
                capOwn = own;
                capTarget = target;
                capTransparent = o.Transparent;
                capGlow = o.Glow;
                capCover = o.Cover;
                fineCap = 0;
                capFrom = null;
            }
            if (cost == null || costError != null || cost.File != file || cost.Stamp != header.Stamp || ReferenceEquals(cost, capFrom))
                return fineCap;
            capFrom = cost;                                                 // each count once
            fineCap = PictureOptions.NextCap(fineCap, own, cost.Bytes, cost.Pixels, (long)(target * 0.98), header.H / (double)header.W);
            return fineCap;
        }

        // the cap as the counts made it, and what it is for (FineCapFor)
        int fineCap, capOwn;
        string capFile, capStamp;
        long capTarget;
        bool capTransparent, capGlow;
        float capCover;
        Costed capFrom;

        /// <summary>The width (high: the height) a picture asked to be m metres wide (high) really takes: its plan's, whole
        /// pixels TMP adds up exactly, so Shift + wheel on the size rows goes to the next one (0.20.98, the author: Shift +
        /// wheel does not work on Content size width and height: a centimetre snapped back to the same width).</summary>
        /// <summary>The Resolution row's snap (PictureOptions.Achievable), made once.</summary>
        Func<float, float> snapDetail;

        float Planned(float m, bool high)
        {
            if (!known || plan.W <= 0 || plan.H <= 0) return m;
            PictureOptions probe = o.Clone();
            probe.WidthM = high ? m * plan.W / plan.H : m;
            PicturePlan p = probe.PlanFor(header.W, header.H);
            return high ? p.HeightM : p.WidthM;
        }

        /// <summary>A width asked by the slider or a typed number: as asked when the plan moves that way, else the next size it
        /// takes that way (0.20.149, the author's test: between the sizes a Resolution takes, a drag or a typed 3 m snapped back
        /// to 2.54 m and the row seemed stuck).</summary>
        /// slid: the slider's way (Widgets.Slid): a drag goes the mouse's way, and to the next size only once the mouse is
        /// past halfway to it (0.20.175, the author: a slow drag flickered between two sizes).
        void SizeTo(float m, float now, bool high, int slid)
        {
            int dir = slid != 0 ? slid : Math.Sign(m - now);
            if (dir == 0 || Math.Abs(m - now) < 1e-4f) return;
            if (!known || plan.W <= 0) { o.WidthM = m; return; }
            float got = Planned(m, high);
            if (Math.Sign(got - now) == dir && Math.Abs(got - now) > 1e-4f) o.WidthM = m;
            else if (slid == 0) Finer(dir);
            else
            {
                float next = NextWidth(dir);
                if (!float.IsNaN(next) && (m - (now + Planned(next, false)) / 2f) * dir >= 0) o.WidthM = next;
            }
        }

        /// <summary>Width and Height in metres (3 Place › Content size): they only make the pixels bigger or smaller, for free.</summary>
        public override void DrawSize(Session s, SignLinkWindow w)
        {
            bool was = GUI.enabled;
            GUI.enabled = was && Source.Ready(s) && (selected >= 0 || fromSign);
            using (Widgets.Vertical(Theme.Section))
            {
                float aspect = known ? header.H / (float)header.W : 0.75f;
                float widthNow = known ? plan.WidthM : o.WidthM;            // snapped: the pixel TMP adds up exactly
                if (snapWidth == null)
                {
                    snapWidth = m => Planned(m, false);
                    snapHeight = m => Planned(m, true);
                }
                bool fine = FineSteps();                                    // the size as asked (PictureOptions.Fine), or snapped
                // the sizes these pixels can take: a pixel is PitchStep to MaxPitch wu (0.20.149, review: from 0.25 m, the
                // slider's first metres did nothing for a fine picture and a drag there snapped back), within 0.25-30 m
                // With Fine Width the low end is 0.25 m whatever the Resolution (0.20.184, the author: width and height could have
                // one global minimum, since now it changes): a size under what these pixels make takes fewer of them
                // (PlanFor, 0.20.183), and the Height row keeps the picture's own shape, not the pixels' rounding
                float wLo = 0.25f, wHi = 30f;
                if (known && plan.W > 0)
                {
                    if (!fine) wLo = Mathf.Clamp(plan.W * PictureOptions.PitchStep / 40f, 0.25f, 30f);
                    wHi = Mathf.Clamp(plan.W * PictureOptions.MaxPitch / 40f, wLo, 30f);
                }
                float k = !fine && known && plan.W > 0 && plan.H > 0 ? plan.H / (float)plan.W : aspect;
                float wm = Widgets.Number("pic.width", "Width (free)", widthNow, wLo, wHi, 0.05f, "m", snap: fine ? null : snapWidth);
                int wSlid = Widgets.Slid;
                if (Widgets.Notched != 0 && fine) Finer(Widgets.Notched);
                else if (Math.Abs(wm - widthNow) > 0.0001f) SizeTo(wm, widthNow, false, wSlid);
                float hNow = known ? plan.HeightM : o.WidthM * aspect;
                float hm = Widgets.Number("pic.height", "Height (same shape)", hNow, wLo * k, wHi * k, 0.05f, "m", snap: fine ? null : snapHeight);
                int hSlid = Widgets.Slid;
                if (Widgets.Notched != 0 && fine) Finer(Widgets.Notched);
                else if (Math.Abs(hm - hNow) > 0.0001f)                     // the width of that many pixels at that height's pixel
                    SizeTo(Mathf.Clamp(known && plan.H > 0 ? hm * plan.W / plan.H : hm / aspect, wLo, wHi), widthNow, false, hSlid);
            }
            GUI.enabled = was;
            Widgets.Hint(PluginConfig.PictureFineWidth.Value
                ? "Width and Height move finely; your Resolution (2 Content) stays: for the exact size the picture takes the pixels " +
                  "nearest it, a few more or less (on a server, never more than the multiplayer limit allows; Settings › Pictures)."
                : "Width and Height only make the pixels bigger or smaller and cost nothing; they move in steps, so every pixel " +
                  "stays one the game adds up exactly. What it costs is its Resolution (2 Content); finer steps: Settings › Pictures.");
        }

        // the width asked, not the one planned: a Resolution moves the planned width by millimetres (0.20.187: Undo lit for
        // a Resolution change and did nothing)
        public override bool SizeDiffers(Session s, SignRecipe r) =>
            r.Kind == SignRecipe.Kinds.Picture && s.Chosen.Recipe.SameItem(r) && Math.Abs(o.WidthM - r.WidthM) > 1e-3f;

        public override void SizeAs(Session s, SignRecipe r)
        {
            if (SizeDiffers(s, r)) o.WidthM = r.WidthM;
        }

        public override bool Reverts(Session s)
        {
            SignRecipe h = s.Held.Recipe, c = s.Chosen.Recipe;
            if (!s.HasTarget || c.Kind != SignRecipe.Kinds.Picture) return false;
            if (h.Kind != SignRecipe.Kinds.Picture || !h.SameItem(c)) return true;   // another picture picked
            return Math.Abs(h.WidthM - c.WidthM) > 1e-4f || Math.Abs(h.Pitch - c.Pitch) > 1e-6f || Math.Abs(h.Cover - c.Cover) > 1e-4f;
        }

        /// <summary>The sign's own picture as it holds it: its recipe taken again (Take: its file, or the sign itself), or
        /// none picked when the sign holds something else.</summary>
        public override void Revert(Session s)
        {
            if (s.Held.Recipe.Kind == SignRecipe.Kinds.Picture)
            {
                s.Chosen.Recipe = s.Held.Recipe.Copy();
                shownFor = null;                                            // Take applies it, the file or the sign's own pixels
                maxSteps = 0;
                if (fromSign && signRead != null)
                {
                    o.Detail = signRead.P.W;                                // the sign's own: every pixel, the width it shows
                    o.DetailAuto = false;
                    o.WidthM = signNativeM;
                }
            }
            else
            {
                LeaveSign();
                selected = -1;
            }
        }

        public override string Problem(Session s) => Why(out _);

        /// <summary>Still reading or counting: Problem's waiting reasons, or a press waiting for its last count (the button
        /// says "Calculating ...", 0.20.134).</summary>
        public override bool Calculating(Session s) => pendingAct != null || (Why(out bool waiting) != null && waiting);

        /// <summary>Why the button is grey, or null; waiting: only because something is still read or counted.</summary>
        string Why(out bool waiting)
        {
            waiting = false;
            if (fromSign)
            {
                if (signError != null) return "The sign's picture cannot be read back: " + signError;
                if (!known) return Waiting("Reading the picture from the sign ...", out waiting);
                if (job.Busy) return "Working ...";
                if (GridFailed) return "These settings do not work: " + gridError;
                if (counted && costError != null) return "These settings do not work: " + costError;
                if (!counted && !Estimated) return Waiting("Counting what it costs ...", out waiting);
                return null;
            }
            if (selected < 0) return "Pick a picture above";
            if (header == null) return Waiting("Reading the folder ...", out waiting);
            if (header.Error != null) return header.Error;
            if (!known) return Unreadable;
            if (readErrorKey != null && readErrorKey == decodeKey) return readError;
            if (job.Busy) return "Working ...";
            if (!decoded) return Waiting("Reading the picture ...", out waiting);
            if (GridFailed) return "These settings do not work: " + gridError;
            if (counted && costError != null) return "These settings do not work: " + costError;
            if (!counted && !Estimated) return Waiting("Counting what it costs ...", out waiting);   // the red level needs its bytes (download time)
            return null;
        }

        static string Waiting(string why, out bool waiting)
        {
            waiting = true;
            return why;
        }

        /// <summary>A picture is made centred; the sign's own one whose box could not be padded sits off by its shift
        /// (ApplyFit), so the frame goes there too.</summary>
        public override bool Centre(Session s, out double x, out double y)
        {
            x = y = 0;
            if (fromSign && signRead != null)
            {
                double k = Math.Max(1e-6, s.Chosen.Fit.Scale);
                x = signRead.ShiftR / k;
                y = signRead.ShiftU / k;
            }
            return true;
        }

        public override string What(Session s) => $"This picture ({plan.W} x {plan.H} pixels)";

        /// <summary>The live preview (0.20.129): the quick view's texture, the very grid Place encodes, at the plan's size
        /// and the chosen place (stretched while a new size's grid is drawn); lit by the scene when its light is Lit and no
        /// pixel is see-through (the screens' lit material has no alpha), else glowing.</summary>
        public override PreviewContent Preview(Session s)
        {
            if (!known || !thumb.Has || thumbFile != file || plan.W <= 0 || plan.H <= 0) return null;
            if (!Centre(s, out double x, out double y)) x = y = 0;
            bool solid = cost != null && costError == null && cost.File == file && cost.Glyphs >= cost.Pixels;
            return new PreviewContent { Picture = thumb.Texture, Lit = !o.Glow && solid, Wwu = plan.W * (double)plan.Pitch,
                                        Hwu = plan.H * (double)plan.Pitch, C0x = x, C0y = y, Fit = s.Chosen.Fit, Key = "picture|" + file };
        }

        public override string Lighter(Session s) => "Lower the Resolution on 2 Content: half of it is a quarter of the data (Width costs next to nothing).";

        // the Max button: the most Resolution whose sign texts stay within maxTarget bytes, found from the counted bytes
        int maxSteps, maxLastW = -1;
        /// <summary>Max was pressed (not a new pick's own start), and it found nothing bigger that fits: said under its
        /// row until the Resolution changes (0.20.193, test: a press that changed nothing said nothing).</summary>
        bool maxPressed, maxNothing;
        int maxNothingW = -1;                                               // the pixels it was said for: another Resolution, file or sign hides it
        long maxTarget;

        /// <summary>What Max aims at: 300 KB (SaveCost.SignLimit), and in multiplayer no more than the area has room for
        /// with this content in place of the sign's.</summary>
        static long MaxTarget(Session s) =>
            SignText.Shared() && s.HasTarget ? Math.Min(SaveCost.SignLimit, Math.Max(0, SaveCost.AreaLimit - s.AreaOthers)) : SaveCost.SignLimit;

        /// <summary>One step of Max (PictureOptions.MaxStep), each time the bytes of the Resolution set last are counted;
        /// it stops when it fits and a bigger one would not, after six counts, or on an error.</summary>
        bool StepMax()
        {
            if (maxSteps <= 0 || !known || !counted) return false;
            if (!Have)
            {
                maxSteps = 0;
                return false;
            }
            // the pixels did not move with the last step: this size takes no more (0.20.189, review: Max set an uncounted
            // Resolution there, six times the same)
            bool still = plan.W == maxLastW;
            if (still || PictureOptions.MaxStep(plan.W, cost.Bytes, maxTarget, MostDetail(header), out int next))
            {
                maxNothing = maxPressed && (still || maxLastW == -1);       // no step moved the pixels
                maxNothingW = plan.W;
                maxPressed = false;
                maxSteps = 0;                                               // the most that fits
                return false;
            }
            maxLastW = plan.W;
            o.Detail = next;
            o.DetailAuto = false;
            maxSteps--;
            return true;                                                    // a new Resolution: counted again
        }

        /// <summary>The most Detail a picture can have (PictureOptions.MaxDetail: its own pixels, within MaxPixels).</summary>
        static int MostDetail(Header h) => PictureOptions.MaxDetail(h.W, h.H);

        /// <summary>The red level (SaveCost.Heavy): a long wait for every player who arrives on a server (download and
        /// build), or a sign close to the 512 KiB a player can receive.</summary>
        public override bool Heavy(Session s, out double loadSeconds, out long saveBytes, out long largest)
        {
            loadSeconds = Load();
            saveBytes = SaveBytes();
            // estimated: a stack's largest sign stays about as large (MaxKb splits it), one sign grows with the picture
            largest = Have ? cost.Largest : Estimated ? (cost.Pic.Bands.Count > 1 ? Math.Min(cost.Largest, saveBytes) : saveBytes) : 0;
            return SaveCost.Heavy(loadSeconds, saveBytes, largest);
        }

        /// <summary>Place or Update: the texts the cost line made when they are for these very settings and the file is
        /// unchanged, else the decoded picture encoded again (no second decode); a moving GIF takes its frames from a screen
        /// that plays or parked them (ScreenPlayer.FramesFor), else from its file, and without the counted texts the whole
        /// GIF goes from its file. The new first sign is selected (a new pose makes the stack's signs again).</summary>
        public override void Act(Session s, SignLinkWindow w, Verb v)
        {
            if (!s.HasTarget || file == null) return;
            if (!Have && Estimated)                                         // pressed on the estimate: once it is counted
            {
                Sign pressedOn = s.Target;
                bool asked = Heavy(s, out _, out _, out _);                 // the red-level question was asked on the estimate
                pendingIn = w;
                pendingAct = () =>
                {
                    string why = Have ? Problem(s) ?? Limit(s) : null;
                    if (!Have || !ReferenceEquals(s.Target, pressedOn))     // another pick or sign meanwhile (0.20.130: said)
                        w.Say("error: the picture or the sign changed while it was counted: press the button again");
                    else if (why != null) w.Say("error: " + why);
                    else if (!asked && Heavy(s, out _, out _, out _))       // counted, it is very large: asked first, as always
                        w.Say("error: counted, it is very large for every player who arrives: press the button again to decide");
                    else Act(s, w, v);
                };
                return;
            }
            ApplyFit(s.Chosen.Fit);
            Sign target = s.Target;
            if (fromSign)
            {
                ActFromSign(s, w, target);
                return;
            }
            string f0 = file, key = optionsKey, stamp = header?.Stamp;
            Decoded d = source;
            Costed c = Have && costFor == key ? cost : null;
            bool gif = f0.EndsWith(".gif", StringComparison.OrdinalIgnoreCase);
            job.Say = w.Say;
            Action<Sign> done = x => Placed(s, w, target, x);
            w.Later(() =>
            {
                if (stamp == null || Stamp(f0) != stamp)                    // changed on disk since it was counted: read again,
                {                                                           // not placed unseen (0.20.130, review)
                    Relist();
                    w.Say("error: the file changed on disk since it was counted; it is read again: press the button once more");
                    return;
                }
                if (gif && c != null && c.Stamp == stamp)                   // the counted texts too (0.20.132, review: an Update
                {                                                           // decoded and encoded the GIF again)
                    ScreenFrames frames = o.Animate ? Plugin.Screens.FramesFor(stamp) : null;   // playing or parked: shared
                    if (!o.Animate || frames != null) job.Place(target, c.Pic.With(frames), o, done);
                    else job.StartScreen(target, f0, c.Pic, o, done);       // only the frames are decoded
                }
                else if (gif) job.Start(target, f0, o, done);
                else if (c != null && c.Stamp == stamp) job.Place(target, c.Pic, o, done);
                else if (d != null && d.File == f0 && d.Stamp == stamp) job.Start(target, f0, d.Rgba, d.W, d.H, d.SrcW, d.SrcH, o, done);
                else job.Start(target, f0, o, done);
            });
        }

        /// <summary>After a placement: the choice's fit is the one now on the sign (an Update from the sign's own pixels
        /// writes the box's shift into it; a fit made with another scale comes back at 1), so the rows do not see a
        /// change that is not there and Live Apply moves nothing back. Then the new first sign is selected.</summary>
        static void Placed(Session s, SignLinkWindow w, Sign target, Sign first)
        {
            if (!ReferenceEquals(s.Target, target))
            {
                s.Touch();                                                  // another sign by now: it stays
                return;
            }
            if (first) s.Chosen.Fit = SignFit.Read(first, out _, out _, out _);
            w.Select(first);                                                // still the sign the job was for
        }

        /// <summary>Update of the sign's own picture: the texts the cost line made from its pixels (the button waits for
        /// them), on the sign only if it is still the stack they were read from (another player may have changed it);
        /// the recipe then says how its file makes it (SignRecipeFor).</summary>
        void ActFromSign(Session s, SignLinkWindow w, Sign target)
        {
            Costed c = Have && costFor == optionsKey ? cost : null;
            if (!known || signRead == null || c == null) return;
            string readFor = signJob.Done;
            ulong hash = signRead.Hash;
            SignRecipe write = SignRecipeFor(c.Pic.Plan);
            job.Say = w.Say;
            w.Later(() =>
            {
                Sign root = SignFinder.Root(target);
                if (s.StackKey != readFor && (!root || SignPicture.TextsHash(SignBuilder.Content(root)) != hash))
                {
                    signJob.Forget();                                       // another player changed its picture: read it again
                    w.Say("error: the sign's picture changed meanwhile; it is read again: press the button once more");
                    return;
                }
                job.Place(target, c.Pic, o, x =>
                {
                    SignMarks.SetSource(x, write.ToString());               // how its file makes it again, at the same size
                    Placed(s, w, target, x);
                });
            });
        }
    }
}
