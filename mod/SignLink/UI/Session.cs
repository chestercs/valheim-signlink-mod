using System;
using System.Collections.Generic;
using System.Text;
using SignLink.Animation;
using SignLink.Pictures;
using SignLink.Presets;
using SignLink.Signs;
using UnityEngine;

namespace SignLink.UI
{
    /// <summary>The window's one shared state: the selected sign and its stack (read again twice a second and after
    /// every action), what it holds as the sources know it (Held), what the tabs chose (Choice: the player's own
    /// settings, or the ones loaded from a sign holding something a source made), and what the footer's button does
    /// about it (Verb). Everything here changes in Layout events only.</summary>
    internal sealed class Session
    {
        public Sign Target { get; private set; }
        public bool HasTarget => Target && SignFinder.Zdo(Target) != null;
        public List<Sign> Stack { get; private set; } = new List<Sign>();
        /// <summary>The stack's first sign (found from the selected sign while the stack is not read yet): what the frame,
        /// the preview and Move the sign work on.</summary>
        public Sign First => Stack.Count > 0 ? Stack[0] : SignFinder.Root(Target);
        public string Name = "", Version = "", MadeWith = "";
        public int Bytes, Largest, Gifs, Tools, Frames;
        public long SaveBytes;
        /// <summary>What SignLink's other signs within SaveCost.AreaRadiusM of the stack hold (SignArea; 0 in single
        /// player), and how many they are (a stack once): the multiplayer area limit's part beside this stack.</summary>
        public long AreaOthers;
        public int AreaSigns;
        /// <summary>SignLink made what the stack holds (its marks): then it counts in the area too (0.20.67).</summary>
        public bool SignLinks;
        /// <summary>What the area holds now, and how many of SignLink's signs: the others, and this stack when it is
        /// SignLink's (a vanilla sign counts once SignLink puts something on it).</summary>
        public long AreaNow => AreaOthers + (SignLinks ? SaveBytes : 0);
        public int AreaCount => AreaSigns + (SignLinks ? 1 : 0);
        public float Fps;
        public bool HasAnim, CanEdit = true, Loaded = true;
        /// <summary>The game hides the stack's text on this computer (Sign.m_isViewable false: a blocked or muted author,
        /// user content off; client/Sign.cs:145-162): SignLink shows none of it either, no words, no picture read back,
        /// nothing loaded into the editor (0.20.132, review).</summary>
        public bool Hidden;
        /// <summary>What shows instead of such a sign's words.</summary>
        public const string HiddenWords = "[hidden by the game's content settings]";

        /// <summary>The name SignLink gave a sign's content, or HiddenWords when the game hides the sign here (its author
        /// chose the name: 0.20.151, review, it showed for hidden signs in the window and the lists).</summary>
        public static string ShownName(Sign s) =>
            !s ? "" : !s.m_isViewable ? HiddenWords : SignRecipe.Clean(SignMarks.Name(s));
        const string HiddenMissing = "Its content is hidden by the game's content settings (a blocked or muted author, or user content off); " +
                                     "pick something to put on instead.";
        /// <summary>Why the actions on the sign are disabled (a ward you have no access to), or null.</summary>
        public string Reason;
        /// <summary>Why what the sign holds could not be loaded into the choice (made from another player's picture
        /// file or a piece that is gone, a text too long for the editor): one sentence for the bar, or null. Nothing is
        /// picked then (Blank).</summary>
        public string Missing;
        public readonly Held Held = new Held();
        /// <summary>Where the held content's middle sits as made (SignFit), when known.</summary>
        public double C0x, C0y;
        public bool CentreKnown;
        /// <summary>The stack's quick view (read on a worker when the stack changed) and, for a pixel picture, its size
        /// as made in metres (zero when unknown).</summary>
        public readonly Thumb View = new Thumb();
        public Vector2 NaturalM;
        /// <summary>The quick view's words where it shows no picture: the sign's own words (0.20.174, the author: the quick
        /// view needs no explanation, it should always show just the content), else empty; only a stack the game hides
        /// says so.</summary>
        public string ViewInfo = "";

        readonly Choice own = new Choice();
        Choice loaded;
        /// <summary>The active choice: the one loaded from the sign while it holds something a source made, else the
        /// player's own.</summary>
        public Choice Chosen => loaded ?? own;
        public bool Editing => loaded != null;

        /// <summary>The glyphs the stack's texts make (SaveCost.Glyphs), read with its quick view, for the stack as it
        /// is now only (GlyphsKnown).</summary>
        public long Glyphs { get; private set; }
        string glyphsFor;
        public bool GlyphsKnown => glyphsFor != null && glyphsFor == stackKey;
        /// <summary>The stack as it is now (its signs and their revisions): what a read of its texts was for.</summary>
        public string StackKey => stackKey;
        public Verb Verb { get; private set; }
        public bool Live { get; private set; }
        /// <summary>Set by the Content tab: puts the item a recipe names into a choice (switching to its source); false
        /// when the item is not here (its file, its piece).</summary>
        public Func<SignRecipe, Choice, bool> LoadInto = null;

        /// <summary>Signs may have changed (after any action, or a selection).</summary>
        public event Action Changed;

        float stackAt = -1f;
        string stackKey, heldKey;
        /// <summary>The stack found whole (SignFinder.IsLoaded) as it was then; the sign the area was counted around, and
        /// when.</summary>
        string loadedFor;
        Sign areaOf;
        float areaAt;
        const float AreaEvery = 2f;
        /// <summary>The stack (its signs and revisions) the movable check last ran for: a turn of the sign (Move the sign)
        /// changes heldKey, not the texts, and must not grey the content rows while the check runs again.</summary>
        string movableFor;
        Sign stackOf;
        bool sameContent;
        /// <summary>The mark the loaded slot was made from, and its recipe in the source's own words (a 0.19 mark names
        /// a built-in piece by its name): the slot stays while the mark is the same.</summary>
        string loadedRaw;
        SignRecipe loadedHeld;
        float signTilt, signTurn;
        bool signEdge;

        /// <summary>The selected sign's pose as it is (read with the stack): what a choice's pose is measured against.</summary>
        public float SignTilt => signTilt;
        public float SignTurn => signTurn;

        /// <summary>Where the stack's first sign stands for this fit (the frame and the preview): its own transform while
        /// the fit keeps the pose it holds (Change.SamePose), as the world save keeps it, else the pose to come, worked out
        /// (SignPose.Preview). True: its own.</summary>
        public bool PendingPose(Sign target, Fit fit, out Vector3 pos, out Quaternion rot)
        {
            bool own = Change.SamePose(Held.Fit, fit);
            if (own)
            {
                pos = target.transform.position;
                rot = target.transform.rotation;
            }
            else SignPose.Preview(target, (float)fit.Tilt, (float)fit.Turn, fit.Edge, out pos, out rot);
            return own;
        }

        readonly Job<Reading> viewJob = new Job<Reading>();
        readonly Job<bool> movableJob = new Job<bool>();

        /// <summary>Selects a sign (its stack's first sign is what the tabs work on). sameContent: our own action made
        /// the stack again with the same content (a refit, a placement), so the view and the rows stay instead of being
        /// cleared and read again; otherwise the old sign's view goes at once.</summary>
        public void Select(Sign s, bool sameContent = false)
        {
            Target = s;
            this.sameContent = sameContent && s;
            stackAt = -1f;
            if (!this.sameContent)
            {
                View.Clear();
                ViewInfo = "";
                NaturalM = Vector2.zero;
                viewJob.Abandon();
            }
            Changed?.Invoke();
        }

        /// <summary>The window opens: the sign read again as a new selection, so the rows and the choice start from what it
        /// holds and nothing from before waits (0.20.187, the author: I open it once and an undo is already waiting; opening on the
        /// same sign kept the last choice, rows the sign no longer held).</summary>
        public void Fresh()
        {
            stackOf = null;
            heldKey = null;
            sameContent = false;
            Touch();
        }

        /// <summary>After any action: read again at the next Layout.</summary>
        public void Touch()
        {
            stackAt = -1f;
            Changed?.Invoke();
        }

        /// <summary>Layout events only. Reads the stack (twice a second, or after Touch), what the sign holds, the quick
        /// view, keeps the choice slots, and derives the verb.</summary>
        public void Refresh(float now)
        {
            if (Event.current.type != EventType.Layout) return;
            if (Target && SignFinder.Zdo(Target) == null) Target = null;      // gone: unloaded or removed
            bool other = !ReferenceEquals(stackOf, Target);
            if (now - stackAt >= 0.5f || other)
            {
                stackAt = now;
                stackOf = Target;
                ReadStack(now, other);
                // hidden by the game or shown again: read again (nothing of a hidden one loaded, 0.20.132)
                string key = Stack.Count > 0 ? stackKey + "|" + Stack[0].transform.rotation + (Hidden ? "|hidden" : "") : null;
                if (key != heldKey)
                {
                    // our own action made the stack again (Apply, Reset, a refit: sameContent): the same content, so the
                    // slot and what the player chose in it stay (0.20.91, review: Apply sign coord changes threw away a
                    // typed text, a new pick, its size and a pending tilt)
                    ReadHeld(other && !sameContent);
                    heldKey = key;                                          // read: a read that threw is tried again (0.20.130)
                }
            }
            UpdateView(now);
            Verb = Change.Decide(Held, Chosen);
            Live = Change.Live(Held, Chosen, Verb);
        }

        /// <summary>The stack and its sizes (a picture's texts are megabytes: UTF-8 counts are cached by revision). Twice a
        /// second, so the costly parts are kept (0.20.132, review): a stack found whole stays whole while it is the same
        /// (StackKey: its signs and revisions), and the area around it is counted again every AreaEvery seconds, or for
        /// another sign.</summary>
        void ReadStack(float now, bool other)
        {
            Stack = Target ? SignFinder.Stack(Target) : new List<Sign>();
            Bytes = Largest = Gifs = Tools = 0;
            var key = new StringBuilder();
            foreach (Sign s in Stack)
            {
                int b = SignText.TextBytes(s);
                Bytes += b;
                Largest = Math.Max(Largest, b);
                key.Append(SignFinder.Id(s)).Append('@').Append(SignFinder.Zdo(s)?.DataRevision).Append(' ');
                if (Plugin.Screens.Plays(s)) Gifs++;
                if (Plugin.Text.Drives(s)) Tools++;
            }
            Sign root = Stack.Count > 0 ? Stack[0] : null;
            Name = root ? ShownName(root) : "";
            Version = root ? SignRecipe.Clean(SignMarks.MadeWith(root)) : "";
            SignLinks = root && (Version.Length > 0 || SignMarks.IsSignLinks(Stack));
            MadeWith = !root ? "" : Version.Length > 0 ? $"SignLink {Version}" : SignLinks ? "SignLink (older version)" : "not SignLink";
            byte[] blob = root ? StoredAnims.Blob(root) : null;
            SaveBytes = Bytes + (long)SaveCost.PerSign * Stack.Count + (blob != null && blob.Length > 0 ? blob.Length + 8 : 0);
            if (!root || !SignText.Shared())
            {
                AreaSigns = 0;
                AreaOthers = 0;
                areaOf = null;
            }
            else if (other || !ReferenceEquals(root, areaOf) || now - areaAt >= AreaEvery)   // the area limit
            {
                areaOf = root;
                areaAt = now;
                AreaOthers = SignArea.Others(root.transform.position, Stack, out AreaSigns);
            }
            HasAnim = root && Plugin.Stored.Info(root, out Frames, out Fps);
            if (!HasAnim)
            {
                Frames = 0;
                Fps = 0f;
            }
            CanEdit = !root || SignText.CanEdit(root);
            stackKey = Stack.Count > 0 ? key.ToString() : null;
            if (!root) Loaded = true;
            else if (stackKey != null && stackKey == loadedFor) Loaded = true;   // found whole: the same signs, unchanged
            else
            {
                Loaded = SignFinder.IsLoaded(root);
                loadedFor = Loaded ? stackKey : null;
            }
            Hidden = root && !root.m_isViewable;
            Reason = root && !CanEdit ? "a ward you have no access to protects this sign" : null;
            Held.HasSign = HasTarget;
            Held.Empty = Bytes == 0;
            Held.Bytes = Bytes;
            Held.Text = root ? SignText.SavedText(root) : "";
        }

        /// <summary>What the sign holds as the sources know it, and the choice slots: a sign holding something a source
        /// made loads it (unless the item is not here); any other sign, an empty one too, gets a slot of its own with nothing
        /// picked (Blank); with no sign selected the player's own choice is back. The pose is never
        /// part of a slot's memory: it follows the selected sign.</summary>
        void ReadHeld(bool other)
        {
            Sign root = Stack.Count > 0 ? Stack[0] : null;
            string raw = root ? SignMarks.Source(root) : "";
            Fit heldWas = Held.Fit;                                         // what the rows started from (Follow, below)
            if (root)
            {
                Held.Fit = SignFit.Read(root, out C0x, out C0y, out CentreKnown);
                Held.Recipe = SignRecipe.Parse(raw);
                Held.OldLayers = Stack.Count > 1 && Held.Recipe.Kind == SignRecipe.Kinds.Art &&   // put before the layer anchors
                                 Held.Recipe.Path.StartsWith("SignLink.Art.glyph/", StringComparison.Ordinal) && Before(Version, 0, 20, 145);
                // a text the Text source put, as made (its fit taken off): what its editor loads and compares, so the
                // Size rows measure the text as made whatever its size on the sign (0.20.68); none of a text the game
                // hides (its Reset would load it, 0.20.132)
                Held.Made = Hidden ? ""
                    : Held.Recipe.Kind == SignRecipe.Kinds.Text && Held.Text.Length <= Sources.TextSource.MaxChars
                    ? MadeOf(Held.Text, Held.Fit) : Held.Text;
                if (stackKey != movableFor)                                // new texts: read every tag again
                {
                    movableFor = stackKey;
                    if (!sameContent) Held.Movable = false;                  // until the worker has read every tag
                    string checkFor = stackKey;
                    List<string> texts = SignBuilder.Content(root);          // the strings themselves, not copies
                    byte[] blob = StoredAnims.Blob(root);
                    movableJob.Abandon();
                    movableJob.Want(checkFor, 0f, 0f,
                        () => () =>
                        {
                            IList<string> what = AnimBlob.TryUnpack(blob, out string[] frames, out _) ? frames : (IList<string>)texts;
                            return TextTransform.Supports(what, out _) && what.Count > 0 && what[0].Length > 0;
                        },
                        can => { if (movableFor == checkFor) Held.Movable = can; },
                        e => { if (movableFor == checkFor) Held.Movable = false; });
                }
            }
            else
            {
                Held.Fit = Fit.AsMade;
                Held.Recipe = SignRecipe.None;
                Held.OldLayers = false;
                Held.Made = Held.Text;
                Held.Movable = false;
                movableFor = null;
                C0x = C0y = 0;
                CentreKnown = false;
            }

            Missing = null;
            // a text the game hides here goes into no editor: a slot with nothing picked, as for a file not here (0.20.132,
            // review); hidden or shown again, the slot is made again
            bool hideText = Hidden && Held.Recipe.Kind == SignRecipe.Kinds.Text, wasHidden = ReferenceEquals(loadedMissing, HiddenMissing);
            if (root && Held.Recipe.Kind != SignRecipe.Kinds.None && LoadInto != null)
            {
                if (loaded != null && !other && raw == loadedRaw && wasHidden == hideText)         // the same mark read again (the slot may
                {                                                                                  // hold another pick by now): as first read
                    Held.Recipe = loadedHeld.Copy();
                    Missing = loadedMissing;                                // its file not here: said again (0.20.91, review)
                    Follow(heldWas, other);
                }
                else if (loaded == null || other || hideText || wasHidden || !loaded.Recipe.SameItem(Held.Recipe))   // the slot stays
                {
                    var c = new Choice { Fit = Held.Fit, Light = Change.LightOf(Held), Text = Held.Made, Recipe = Held.Recipe.Copy() };
                    if (!hideText && LoadInto(Held.Recipe, c))
                    {
                        loaded = c;
                        loadedRaw = raw;
                        loadedHeld = c.Recipe.Copy();                       // the item as the source names it: so a 0.19 sign is "as it is"
                        loadedMissing = null;
                        Held.Recipe = loadedHeld.Copy();
                    }
                    else
                    {
                        Missing = hideText ? HiddenMissing : Held.Recipe.Kind == SignRecipe.Kinds.Text
                            ? "It holds a text too long to edit here (Copy, Paste and the API handle it); pick something to put on instead."
                            : $"Made from {SignRecipe.Clean(Held.Recipe.Kind == SignRecipe.Kinds.Picture ? Held.Recipe.File : Held.Recipe.Path)}, " +
                              "which is not on this computer; pick something to put on instead.";
                        Blank(raw, Missing);                                // not here: nothing picked for it (0.20.89)
                    }
                }
                else if (raw != loadedRaw)
                {
                    // the same item put on again (by any source: a put, an update, a refit): a new slot as read now, so
                    // every source takes it again and no source offers its old pick over it (0.20.148, review: after a
                    // put from Art, the Picture tab offered its picture back over the piece)
                    loaded = loaded.Copy();
                    loadedRaw = raw;
                    loadedHeld = Held.Recipe.Copy();
                    loadedMissing = null;
                }
            }
            // a sign holding what no source made (no record: an older picture, a typed text): nothing picked for it, not
            // the player's own pick of before (0.20.89, the author: I wanted to edit my picture and overwrote it with one
            // I had just used for an upload on another sign); an empty sign too (0.20.192, the author: an empty sign starts
            // clean; if its content was deleted, it was deleted; it took the player's own choice of before, a typed text,
            // an Art pick)
            else if (root)
            {
                // ... also when the same sign's content lost its record (a plain text pasted over a picture: 0.20.91,
                // review): the slot of what it held before would put that back
                if (loaded == null || other || raw != loadedRaw) Blank(raw, null);
                else Follow(heldWas, other);
            }
            else loaded = null;                                             // no sign: the player's own settings are back

            SignPose.Read(root, out float tilt, out float turn, out bool edge);
            if (root && (other || Mathf.Abs(tilt - signTilt) > 0.05f || Math.Abs(Mathf.DeltaAngle(turn, signTurn)) > 0.05f || edge != signEdge))
            {
                // another sign, or its pose changed: start from it, the content's place along the face kept (Fit.Posed, as
                // the rows keep it: 0.20.159, review: the raw tilt sent the Up row's place into the hidden normal)
                Chosen.Fit = Chosen.Fit.Posed(turn, tilt, edge);
            }
            signTilt = tilt;
            signTurn = turn;
            signEdge = edge;
            sameContent = false;
        }

        /// <summary>The sign's fit changed under a slot the player has not touched (another player moved, sized or posed the
        /// same content): the slot takes the new fit, so the rows show it and nothing puts the old one back (0.20.157,
        /// review: the old fit made a Refit, which the live apply wrote over the other player's change).</summary>
        void Follow(Fit heldWas, bool other)
        {
            if (other || loaded == null || Held.Fit.Same(heldWas) || !loaded.Fit.Same(heldWas)) return;
            loaded.Fit = Held.Fit;
        }

        /// <summary>Was it made by a SignLink older than a.b.c (an unknown version counts as older)?</summary>
        static bool Before(string version, int a, int b, int c)
        {
            string[] p = (version ?? "").Split('.');
            if (p.Length < 3 || !int.TryParse(p[0], out int x) || !int.TryParse(p[1], out int y) || !int.TryParse(p[2], out int z)) return true;
            return x != a ? x < a : y != b ? y < b : z < c;
        }

        /// <summary>A slot of its own with nothing picked, for a sign whose content no source loads: the sources show no
        /// pick of before, and the verb waits for one made for this sign (0.20.89).</summary>
        void Blank(string raw, string missing)
        {
            loaded = new Choice { Fit = Held.Fit, Light = GlowMode.AsMade, Recipe = SignRecipe.None, Text = "" };
            loadedRaw = raw;
            loadedHeld = Held.Recipe.Copy();                                // what the sign says it holds, read again so
            loadedMissing = missing;                                        // (0.20.91, review: None, and the hint went)
        }

        string loadedMissing;

        /// <summary>A Text sign's text as made (TextTransform.Unfit), kept while its text and fit stay: a turn of the sign
        /// reads it again, and up to 50 000 characters were rewritten twice each time (0.20.91, review).</summary>
        string MadeOf(string text, Fit fit)
        {
            if (madeFrom == null || text != madeFrom || !madeFit.Equals(fit))
            {
                madeFrom = text;
                madeFit = fit;
                madeText = TextTransform.Unfit(text, fit);
            }
            return madeText;
        }

        string madeFrom, madeText;
        Fit madeFit;

        /// <summary>Reads the stack again on a worker when it changed; the result replaces the view on the main thread
        /// unless the stack changed meanwhile (Job). Not while the stack is still loading: no half picture.</summary>
        void UpdateView(float now)
        {
            if (stackKey == null || !Target)
            {
                if (View.Has) View.Clear();
                ViewInfo = "";
                NaturalM = Vector2.zero;
                return;
            }
            if (!Loaded) return;
            if (Hidden)                                                     // the game hides it: no words, no picture (0.20.132)
            {
                if (View.Has) View.Clear();
                ViewInfo = "Hidden by the game's content settings.";
                NaturalM = Vector2.zero;
                glyphsFor = null;                                           // nor a preview of it moved (ContentPreview)
                if (viewJob.Busy || viewJob.Done != null) viewJob.Abandon();   // shown again: read again
                return;
            }
            Sign root = Stack[0];
            viewJob.Want(stackKey, now, 0f,
                () =>
                {
                    List<string> texts = SignBuilder.Content(root);
                    byte[] blob = StoredAnims.Blob(root);
                    var pieces = new List<Preset>(ArtLibrary.Items);
                    string piece = Name;
                    return () => Read(texts, blob, pieces, piece);
                },
                r =>
                {
                    Glyphs = r.Glyphs;
                    glyphsFor = viewJob.Done;
                    // a pixel picture's size as made, in metres: the size rows are a width and a height then
                    NaturalM = r.Pitch > 0 && Held.Fit.Scale > 0
                        ? new Vector2((float)(r.W * r.Pitch / 40 / Held.Fit.Scale), (float)(r.H * r.Pitch / 40 / Held.Fit.Scale))
                        : Vector2.zero;
                    if (r.Rows != null) View.Set(r.W, r.H, r.Rows, r.Fps);
                    else if (r.Render != null)
                    {
                        Texture2D tex = PictureImporter.LoadTexture(r.Render);
                        if (tex) View.SetTexture(tex);
                        else View.Clear();
                    }
                    else View.Clear();
                    ViewInfo = r.Info;
                },
                e => { ViewInfo = ""; Plugin.Log.LogWarning($"quick view: {e.Message}"); });
        }

        sealed class Reading
        {
            public long Glyphs;
            public int W, H;
            public double Pitch;
            public List<byte[]> Rows;
            public float Fps;
            public byte[] Render;
            public string Info = "";
        }

        /// <summary>What a stack shows (any thread): a saved animation or a pixel picture read back from its texts (as
        /// RGBA rows, ready to upload), glyph art matched with the built-in piece it came from (its render),
        /// anything else its words.</summary>
        static Reading Read(List<string> texts, byte[] blob, List<Preset> pieces, string piece)
        {
            var r = new Reading { Glyphs = SaveCost.Glyphs(texts) };      // what every player's game builds (as the cost counts)
            if (blob != null && blob.Length > 0 && AnimBlob.TryUnpack(blob, out string[] frames, out float fps))
            {
                var input = new List<IList<string>>();
                foreach (string f in frames) input.Add(new[] { f });
                if (TextReadback.ReadFrames(input, out r.W, out r.H, out List<int[]> animGrids, asSeen: true))
                {
                    r.Rows = animGrids.ConvertAll(g => Thumb.Rows(g, r.W, r.H));
                    r.Fps = fps;
                    return r;
                }
            }
            if (texts.Count == 1 && texts[0].Length == 0) return r;
            if (TextReadback.ReadFrames(new[] { (IList<string>)texts }, out int w, out int h, out List<int[]> grids, out double pitch, true))
            {
                r.W = w;
                r.H = h;
                r.Pitch = pitch;
                r.Rows = grids.ConvertAll(g => Thumb.Rows(g, w, h));
                return r;
            }
            Preset match = Match(texts, pieces) ?? pieces.Find(p => p.Name == piece && p.ReadPicture != null);
            if (match != null)
            {
                r.Render = match.ReadPicture();
                return r;
            }
            r.Info = TextReadback.Words(texts, 140);                       // the words alone, no picture to read
            return r;
        }

        /// <summary>The texts with their glowing tags dropped, per piece with a render (glyph art), made once.</summary>
        static readonly Dictionary<string, string[]> plainPieces = new Dictionary<string, string[]>();

        /// <summary>The built-in piece with a render that the stack holds, glowing or lit (any thread; one view is read at
        /// a time).</summary>
        static Preset Match(List<string> texts, List<Preset> pieces)
        {
            lock (plainPieces)
            {
                var plain = new string[texts.Count];
                for (int i = 0; i < plain.Length; i++) plain[i] = SignGlow.Strip(texts[i]);
                foreach (Preset p in pieces)
                {
                    if (p.ReadPicture == null || p.Kind == Preset.Kinds.Anim) continue;
                    if (!plainPieces.TryGetValue(p.Path, out string[] mine))
                    {
                        try
                        {
                            string[] t = p.Load().Texts;
                            mine = new string[t.Length];
                            for (int i = 0; i < t.Length; i++) mine[i] = SignGlow.Strip(t[i]);
                        }
                        catch (Exception) { mine = new string[0]; }
                        plainPieces[p.Path] = mine;
                    }
                    if (mine.Length != plain.Length) continue;
                    bool same = true;
                    for (int i = 0; i < mine.Length && same; i++) same = mine[i] == plain[i];
                    if (same) return p;
                }
                return null;
            }
        }
    }
}
