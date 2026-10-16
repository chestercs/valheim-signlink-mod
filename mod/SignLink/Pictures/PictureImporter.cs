using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Threading;
using BepInEx;
using SignLink.Api;
using SignLink.Signs;
using UnityEngine;
using Object = UnityEngine.Object;

namespace SignLink.Pictures
{
    /// <summary>A picture made into sign texts: its pixel size, one text per stacked sign, where it sits, and the GIF's
    /// frames when it moves.</summary>
    internal sealed class EncodedPicture
    {
        public int SourceW, SourceH, W, H;
        public float Pitch;
        public PicturePlan Plan;
        public double CenterX, Bottom;                       // wu, relative to the sign
        public List<PixelText.Band> Bands;
        /// <summary>A moving GIF's frames for the screen over its still (null: nothing plays).</summary>
        public ScreenFrames Screen;
        /// <summary>What placing it also did or could not do ("; the GIF plays over it ..."), for the status line.</summary>
        public string Note = "";
        public double Ms;
        /// <summary>The grid's share of Ms (resampling), logged with it.</summary>
        public double GridMs;
        /// <summary>The file it came from, kept on the sign (SignMarks.NameKey).</summary>
        public string Name;

        public int MaxBytes
        {
            get
            {
                int m = 0;
                foreach (PixelText.Band b in Bands) m = Math.Max(m, b.Bytes);
                return m;
            }
        }

        /// <summary>What it adds to the world save (SaveCost): all its signs together, from the bands' own byte counts
        /// (0.20.132, review: every text's UTF-8 was counted again, twice a placement).</summary>
        public long SaveBytes => PixelText.SaveBytes(Bands);

        /// <summary>The same picture for another placement (the cost line's texts placed again, 0.20.132): the bands shared
        /// (nothing writes them), its own screen and note.</summary>
        public EncodedPicture With(ScreenFrames screen)
        {
            var copy = (EncodedPicture)MemberwiseClone();
            copy.Screen = screen;
            copy.Note = "";
            return copy;
        }

        public string Summary =>
            $"{SourceW}x{SourceH} -> {W}x{H} pixels ({W * Pitch / 40f:0.0} x {H * Pitch / 40f:0.0} m) on {Bands.Count} sign(s), " +
            $"{SaveCost.Label(SaveBytes)} in the world save (the largest sign {MaxBytes / 1024} KB)" +
            (Screen != null ? $", {Screen.Frames.Length} frames" : "");
    }

    /// <summary>The frames a moving GIF plays over its still for SignLink players (ScreenPlayer.StartTimed): RGB24 rows
    /// bottom-up (Unity's order), shrunk to at most PictureImporter.ScreenSide a side, each with its delay.</summary>
    internal sealed class ScreenFrames
    {
        public int W, H;
        public byte[][] Frames;
        public int[] DelaysMs;
        public long Bytes;
        /// <summary>The GIF's own size: the still's plan comes from it (PictureOptions.PlanFor), so frames parked in
        /// memory play again without the file (ScreenRestore).</summary>
        public int SourceW, SourceH;
        /// <summary>What the frames came from (PictureImporter.Stamp of the file): the screens park them under it.</summary>
        public string Key;
    }

    /// <summary>PNG, JPG or GIF to a picture made of stacked signs that vanilla clients see (SignBuilder.SetContent), the
    /// same pipeline as signart/vanilla_hq.py. PNG and JPG are decoded by Unity (main thread), GIFs by GifDecoder (any
    /// thread); encoding runs on any thread; placing on the main thread. A moving GIF also plays over its first frame
    /// as a texture screen, the way vanilla_hq.py --screen does.</summary>
    internal static class PictureImporter
    {
        public static string Folder => Path.Combine(Paths.ConfigPath, Path.Combine("SignLink", "images"));

        /// <summary>Opens the images folder in the system's file browser, made first when it is not there yet (the Picture
        /// list's Open folder, Settings › About's, 0.20.119).</summary>
        public static void OpenFolder()
        {
            Directory.CreateDirectory(Folder);
            Application.OpenURL("file:///" + Folder.Replace(Path.DirectorySeparatorChar, '/'));
        }

        /// <summary>Pictures larger than this are refused before decoding: Unity's decoder holds about 12 bytes per pixel at
        /// once on the main thread (the game stalls, and may run out of memory), and a sign picture uses at most a million
        /// pixels anyway.</summary>
        public const long MaxSourcePixels = 50000000;

        /// <summary>A screen's frames are shrunk to at most this many pixels a side (a 4K GIF is not 24 MB a frame).</summary>
        public const int ScreenSide = 1024;

        /// <summary>Picture formats SignLink reads (a .jfif is a JPEG) and ones it does not but players drop into the
        /// folder: the window lists the latter greyed, with what to do, instead of leaving them out.</summary>
        public static readonly string[] Readable = { ".png", ".jpg", ".jpeg", ".jfif", ".gif" },
                                        Unreadable = { ".heic", ".heif", ".webp", ".bmp", ".tif", ".tiff", ".avif" };

        public static bool IsReadable(string file) => Array.IndexOf(Readable, Path.GetExtension(file).ToLowerInvariant()) >= 0;

        /// <summary>Name, length and time: a file written again is another picture under the same name (the Picture
        /// source's thumbnails and the screens' parked frames are keyed by it). Null when the file cannot be looked at.</summary>
        public static string Stamp(string file)
        {
            try
            {
                var fi = new FileInfo(file);
                return fi.Name + "|" + fi.Length + "|" + fi.LastWriteTimeUtc.Ticks;
            }
            catch (Exception) { return null; }
        }

        /// <summary>The pictures in the images folder, sorted by name; withUnreadable: the known other formats too.</summary>
        public static List<string> Files(bool withUnreadable = false)
        {
            var list = new List<string>();
            try
            {
                Directory.CreateDirectory(Folder);
                StockPictures.Place(Folder);                                   // the paintings that come with SignLink, once
                foreach (string f in Directory.GetFiles(Folder))
                {
                    string ext = Path.GetExtension(f).ToLowerInvariant();
                    if (Array.IndexOf(Readable, ext) >= 0 || (withUnreadable && Array.IndexOf(Unreadable, ext) >= 0)) list.Add(f);
                }
                list.Sort(StringComparer.OrdinalIgnoreCase);
            }
            catch (Exception e)
            {
                Plugin.Log.LogWarning($"images: {e.Message}");
            }
            return list;
        }

        /// <summary>UnityEngine.ImageConversion.LoadImage, found at run time: its module is built against netstandard 2.1,
        /// which a net462 build cannot reference.</summary>
        static MethodInfo loadImage;

        static bool LoadImage(Texture2D tex, byte[] data)
        {
            if (loadImage == null)
            {
                Type t = Type.GetType("UnityEngine.ImageConversion, UnityEngine.ImageConversionModule")
                         ?? throw new InvalidOperationException("Unity's image decoder is missing");
                loadImage = t.GetMethod("LoadImage", new[] { typeof(Texture2D), typeof(byte[]), typeof(bool) })
                            ?? throw new InvalidOperationException("ImageConversion.LoadImage not found");
            }
            return (bool)loadImage.Invoke(null, new object[] { tex, data, false });
        }

        /// <summary>A render (a piece's PNG beside it) is at most this many pixels: a quick view, not a photo.</summary>
        public const long MaxRenderPixels = 4000000;

        /// <summary>A PNG or JPG as a texture (main thread), for quick views; null if it does not decode or is over
        /// MaxRenderPixels by its header.</summary>
        public static Texture2D LoadTexture(byte[] file)
        {
            if (!ImageHeader.Size(file, out int w, out int h, out _) || (long)w * h > MaxRenderPixels) return null;
            var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false) { hideFlags = HideFlags.HideAndDontSave };
            bool ok = false;
            try
            {
                ok = LoadImage(tex, file);
                return ok ? tex : null;
            }
            finally
            {
                if (!ok) Object.DestroyImmediate(tex);
            }
        }

        /// <summary>A picture's EXIF orientation from its header (1 when it has none); refuses pictures over
        /// MaxSourcePixels before anything decodes them.</summary>
        public static int Orientation(byte[] file)
        {
            if (!ImageHeader.Size(file, out int hw, out int hh, out int o))
                throw new ArgumentException("not a PNG, JPG or GIF picture (its header cannot be read)");   // the size guard would be skipped
            if ((long)hw * hh > MaxSourcePixels)
                throw new ArgumentException($"{hw} x {hh} is {(long)hw * hh / 1000000} megapixels: SignLink takes pictures up to " +
                                            $"{MaxSourcePixels / 1000000} (scale it down first; a sign picture has at most a million)");
            return o;
        }

        /// <summary>PNG or JPG bytes to RGBA, rows top-down, turned as a JPEG's EXIF orientation says (main thread: Unity
        /// decodes). Refuses pictures over MaxSourcePixels before decoding. The window's Picture source does the same in
        /// two halves (ReadRaw here, RawPixels on a worker) so a photo costs the main thread one copy.</summary>
        public static byte[] Decode(byte[] file, out int w, out int h)
        {
            int orientation = Orientation(file);
            RawTexture raw = ReadRaw(file);
            byte[] rgba = RawPixels.ToRgba(raw.Bytes, raw.Format, raw.W, raw.H, 0, out w, out h);
            return ImageHeader.Orient(rgba, w, h, orientation, out w, out h);
        }

        /// <summary>A decoded texture's bytes as Unity holds them: rows bottom-up, in Format.</summary>
        public sealed class RawTexture
        {
            public byte[] Bytes;
            public int W, H;
            public RawPixels.Format Format;
        }

        /// <summary>Decodes a PNG or JPG (main thread) and takes the texture's bytes as they are (one copy), by the format
        /// Unity chose; a format RawPixels does not read goes through GetPixels32 instead. The texture is destroyed at
        /// once, not at the frame's end.</summary>
        public static RawTexture ReadRaw(byte[] file)
        {
            var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            try
            {
                if (!LoadImage(tex, file)) throw new ArgumentException("not a PNG, JPG or GIF picture");
                var raw = new RawTexture { W = tex.width, H = tex.height };
                switch (tex.format)
                {
                    case TextureFormat.RGBA32: raw.Format = RawPixels.Format.Rgba32; break;
                    case TextureFormat.RGB24: raw.Format = RawPixels.Format.Rgb24; break;
                    case TextureFormat.ARGB32: raw.Format = RawPixels.Format.Argb32; break;
                    case TextureFormat.BGRA32: raw.Format = RawPixels.Format.Bgra32; break;
                    default:
                        Color32[] px = tex.GetPixels32();
                        raw.Format = RawPixels.Format.Rgba32;
                        raw.Bytes = new byte[px.Length * 4];
                        for (int i = 0, j = 0; i < px.Length; i++, j += 4)
                        {
                            raw.Bytes[j] = px[i].r;
                            raw.Bytes[j + 1] = px[i].g;
                            raw.Bytes[j + 2] = px[i].b;
                            raw.Bytes[j + 3] = px[i].a;
                        }
                        return raw;
                }
                raw.Bytes = tex.GetRawTextureData();
                return raw;
            }
            finally
            {
                Object.DestroyImmediate(tex);                           // a big photo's texture: gone now, not at frame end
            }
        }

        /// <summary>A GIF frame (RGB, GifDecoder) as RGBA.</summary>
        public static byte[] GifFrameRgba(byte[] rgb)
        {
            var rgba = new byte[rgb.Length / 3 * 4];
            for (int i = 0, j = 0; i < rgb.Length; i += 3, j += 4)
            {
                rgba[j] = rgb[i];
                rgba[j + 1] = rgb[i + 1];
                rgba[j + 2] = rgb[i + 2];
                rgba[j + 3] = 255;
            }
            return rgba;
        }

        /// <summary>A GIF to sign texts (any thread): its first frame, at full size, is the still; a moving GIF's frames
        /// are made ready for the screen here, on the worker (shrunk to at most ScreenSide a side, in Unity's row
        /// order), each as it is decoded: no full-size copy of every frame (0.20.132, review). Frames that could never
        /// fit the screens' budget are not decoded at all: the still only, and the note says so. cancelled stops the
        /// decode (the window closed, another file picked).</summary>
        public static EncodedPicture EncodeGif(byte[] file, PictureOptions o, Func<bool> cancelled = null)
        {
            string note = o.Animate ? ScreenNote(file) : null;
            bool moving = o.Animate && note == null;
            byte[] still = null;
            int k = 0, sw = 0, sh = 0;
            GifImage gif = GifDecoder.Decode(file, maxFrames: moving ? int.MaxValue : 1, cancelled: cancelled, frame: (canvas, w, h) =>
            {
                if (still == null)
                {
                    still = GifFrameRgba(canvas);                           // the still: the first frame at full size
                    ScreenShape(w, h, out k, out sw, out sh);
                }
                return moving ? ShrinkFlip(canvas, w, h, k, sw, sh) : null;
            });
            EncodedPicture pic = Encode(still, gif.Width, gif.Height, o);
            if (moving && gif.Frames.Count > 1) pic.Screen = Screen(gif, sw, sh);
            if (note != null) pic.Note = note;
            return pic;
        }

        /// <summary>Why a GIF's frames could never play over its still: the frames it keeps (GifDecoder.Measure, from its
        /// blocks), shrunk for the screen, are over the screens' whole budget; null when they fit, or when its blocks do not
        /// tell (the decode says why). Checked before any frame is decoded (0.20.132, review).</summary>
        public static string ScreenNote(byte[] file)
        {
            if (!GifDecoder.Measure(file, out int w, out int h, out int kept) || kept < 2) return null;
            long bytes = ScreenBytes(w, h, kept), budget = SignLink.Animation.ScreenPlayer.Budget;
            if (bytes <= budget) return null;
            Plugin.Log.LogWarning($"the GIF stays a still: its {kept} frames would be {bytes >> 20} MB (the screens hold at most {budget >> 20} MB)");
            return $"; too big to play over the picture (its frames would be {bytes >> 20} MB, the screens hold at most {budget >> 20} MB)";
        }

        /// <summary>A GIF's frames for its screen alone (any thread), each shrunk as it is decoded: a screen restored from the
        /// file (ScreenRestore), an Update whose still is counted already. Null for a GIF of one frame.</summary>
        public static ScreenFrames GifScreen(byte[] file, Func<bool> cancelled = null)
        {
            int k = 0, sw = 0, sh = 0;
            GifImage gif = GifDecoder.Decode(file, cancelled: cancelled, frame: (canvas, w, h) =>
            {
                if (sw == 0) ScreenShape(w, h, out k, out sw, out sh);
                return ShrinkFlip(canvas, w, h, k, sw, sh);
            });
            return gif.Frames.Count < 2 ? null : Screen(gif, sw, sh);
        }

        /// <summary>The screen's size for a GIF of w x h: shrunk by a whole factor k to at most ScreenSide a side (a box
        /// average: a 4K GIF is not 24 MB a frame).</summary>
        static void ScreenShape(int w, int h, out int k, out int W, out int H)
        {
            k = Math.Max(1, (int)Math.Ceiling(Math.Max(w, h) / (double)ScreenSide));
            W = Math.Max(1, w / k);
            H = Math.Max(1, h / k);
        }

        /// <summary>What a screen of `frames` frames of a w x h GIF holds (ScreenShape's size, RGB24).</summary>
        public static long ScreenBytes(int w, int h, int frames)
        {
            ScreenShape(w, h, out _, out int W, out int H);
            return (long)frames * W * H * 3;
        }

        /// <summary>The decoded (shrunk, flipped) frames as a screen's, each with its delay.</summary>
        static ScreenFrames Screen(GifImage g, int W, int H)
        {
            byte[][] frames = g.Frames.ToArray();
            long bytes = 0;
            foreach (byte[] f in frames) bytes += f.Length;
            return new ScreenFrames { W = W, H = H, Frames = frames, DelaysMs = g.DelaysMs.ToArray(), Bytes = bytes, SourceW = g.Width, SourceH = g.Height };
        }

        /// <summary>An RGB24 frame (rows top-down) shrunk k times with a k x k box average, written bottom-up.</summary>
        static byte[] ShrinkFlip(byte[] rgb, int w, int h, int k, int W, int H)
        {
            var o = new byte[W * H * 3];
            int kk = k * k;
            for (int y = 0; y < H; y++)
            {
                int dst = (H - 1 - y) * W * 3;
                for (int x = 0; x < W; x++)
                    for (int c = 0; c < 3; c++)
                    {
                        int sum = 0;
                        for (int yy = 0; yy < k; yy++)
                            for (int xx = 0; xx < k; xx++)
                                sum += rgb[(Math.Min(h - 1, y * k + yy) * w + Math.Min(w - 1, x * k + xx)) * 3 + c];   // a short side repeats its edge
                        o[dst + x * 3 + c] = (byte)((sum + kk / 2) / kk);
                    }
            }
            return o;
        }

        /// <summary>RGBA to sign texts (any thread), sized and placed by the options' plan (PictureOptions.PlanFor,
        /// UpFor).</summary>
        public static EncodedPicture Encode(byte[] rgba, int w, int h, PictureOptions o) => Encode(rgba, w, h, w, h, o);

        /// <summary>The same for a buffer already halved (RawPixels): the plan comes from the source's own size srcW x srcH,
        /// so a pre-halved photo gets the very grid the full one would (Grid halves what is left the same way).</summary>
        public static EncodedPicture Encode(byte[] rgba, int w, int h, int srcW, int srcH, PictureOptions o)
        {
            var sw = Stopwatch.StartNew();
            PicturePlan plan = o.PlanFor(srcW, srcH);
            int[] grid = Grid(rgba, w, h, plan, !o.Transparent);
            double gridMs = sw.Elapsed.TotalMilliseconds;
            EncodedPicture pic = EncodeGrid(grid, plan, o);
            pic.GridMs = gridMs;
            pic.SourceW = srcW;
            pic.SourceH = srcH;
            pic.Ms = sw.Elapsed.TotalMilliseconds;
            return pic;
        }

        /// <summary>The pixel grid of a plan (any thread): see-through pixels empty, half-transparent ones with their alpha
        /// (Resampler.Grid), unless opaque (alpha dropped, as vanilla_hq.py does). Photos far larger than the result are
        /// first halved with a 2 x 2 box (it spares the resampler's work; kept since the float planes went, 0.20.132, so the
        /// grids stay the same); up to 4 megapixels the result matches the Python pipeline exactly. stale: as
        /// Resampler.Grid's.</summary>
        public static int[] Grid(byte[] rgba, int w, int h, PicturePlan plan, bool opaque, Func<bool> stale = null)
        {
            while ((long)w * h > 4000000 && (long)(w / 2) * (h / 2) >= 4L * plan.Pixels) rgba = RawPixels.Halve(rgba, ref w, ref h);
            return Resampler.Grid(rgba, w, h, plan.W, plan.H, opaque, stale);
        }

        /// <summary>A grid to sign texts (any thread): what Place puts on the signs, and what the Picture tab counts the
        /// world-save size from. Half-transparent pixels are made fainter where the glyphs overlap (PixelText.ForCover).
        /// stale: as PixelText.Bands'.</summary>
        public static EncodedPicture EncodeGrid(int[] px, PicturePlan plan, PictureOptions o, Func<bool> stale = null)
        {
            double size = PixelText.GlyphSize(plan.Pitch, o.Cover);
            px = PixelText.ForCover(px, o.Cover);
            o.Origin(plan, out double cx, out double bottom);
            List<PixelText.Band> bands = PixelText.Bands(px, plan.W, plan.H, plan.Pitch, cx, bottom, size, o.MaxKb, glow: o.Glow, stale: stale);
            return new EncodedPicture { W = plan.W, H = plan.H, Pitch = plan.Pitch, Plan = plan, CenterX = cx, Bottom = bottom,
                                        Bands = bands };
        }

        /// <summary>Puts the picture on the sign's stack, as many signs as it needs (SignBuilder.SetContent; more than
        /// one is building: only in your own world unless allowed), in the options' pose (SignPose: a new pose makes the
        /// stack's signs again), on the main thread; a moving GIF then plays over it. Returns the stack, its first sign
        /// first.</summary>
        public static List<Sign> Place(Sign target, EncodedPicture pic, PictureOptions o)
        {
            if (!target) throw new InvalidOperationException("the sign is gone (unloaded or removed)");
            var texts = new List<string>();
            foreach (PixelText.Band b in pic.Bands) texts.Add(b.Text);
            List<Sign> placed = SignFit.PutPlaced(target, texts, o.FitFor(pic.Plan), pic.Name);
            if (!string.IsNullOrEmpty(pic.Name)) SignMarks.SetSource(placed[0], SignRecipe.ForPicture(o, pic.Plan, pic.Name).ToString());   // the Picture source edits it
            if (pic.Screen != null) PlayOver(placed[0], pic, lit: !o.Glow);
            return placed;
        }

        /// <summary>The GIF as a texture screen exactly over its still: same centre, same physical size plus a pixel of
        /// overscan on every side (it covers the half-pixel rim of the overlapping glyphs), each frame for its own time
        /// (ScreenPlayer.StartTimed), lit as the still is (a still without the glow is shaded by the scene, and so is its
        /// screen). Over the screens' budget it stays a still, and the note says so.</summary>
        static void PlayOver(Sign target, EncodedPicture pic, bool lit)
        {
            ScreenFrames s = pic.Screen;
            pic.Screen = null;                                       // the player owns the frames from here
            if (!PlayOver(target, s, pic.Plan, pic.CenterX, pic.Bottom, lit, out string why))
            {
                pic.Note = $"; it stays still here: {why}";
                Plugin.Log.LogWarning($"the GIF stays a still: {why}");
                return;
            }
            pic.Note = "; the GIF plays over it, and again whenever the sign loads, on every computer that has the file in its images folder";
        }

        /// <summary>The same for frames and a plan from anywhere (ScreenRestore: a still's GIF decoded again): centre
        /// and bottom edge in wu as the still was placed (EncodedPicture.CenterX, Bottom). False, nothing started, when
        /// the screens' budget has no room; why says so.</summary>
        public static bool PlayOver(Sign target, ScreenFrames s, PicturePlan plan, double centerX, double bottom, bool lit, out string why)
        {
            why = null;
            // a pixel of overscan on every side, each way on its own (one pitch for both left a tall GIF overhanging)
            float pitch = (float)(plan.Pitch * (plan.W + 2) / (double)s.W);
            float pitchY = (float)(plan.Pitch * (plan.H + 2) / (double)s.H);
            float cy = (float)(bottom + plan.H * plan.Pitch / 2.0);
            try
            {
                // StartTimed frees this sign's old frames and the parked ones under the key first, then checks the budget
                Plugin.Screens.StartTimed(target, s.W, s.H, s.DelaysMs, (float)centerX, cy, pitch, loop: true,
                                          hideText: true, point: false, lit: lit, frames: s.Frames, now: Time.unscaledTime,
                                          key: s.Key, sourceW: s.SourceW, sourceH: s.SourceH, pitchY: pitchY);
            }
            catch (ArgumentException e)
            {
                why = e.Message;
                return false;
            }
            return true;
        }
    }

    /// <summary>One import at a time from the menu: read now, decode and encode on a worker thread (PNG and JPG are
    /// decoded first, on the main thread), place on the main thread when done. The Picture source hands over what it
    /// decoded or encoded already for its quick view and cost line, so a photo is decoded once and encoded once.</summary>
    internal sealed class ImportJob
    {
        public bool Busy { get; private set; }
        /// <summary>Where the result goes (the window's status line): "<name> is on the sign ...", or "error: ...".</summary>
        public Action<string> Say = _ => { };
        volatile bool cancel;

        /// <summary>Stops a GIF still decoding (the window closed, another file picked): Busy clears when it does. A
        /// placement whose decode is over goes on (the player asked for it), and nothing is cancelled before it starts.</summary>
        public void Cancel()
        {
            if (Busy) cancel = true;
        }

        /// <summary>Starts an import from the file; done gets the placed stack's first sign (a new one when the pose changed).</summary>
        public void Start(Sign target, string file, PictureOptions options, Action<Sign> done = null)
        {
            if (Busy) return;
            try
            {
                byte[] bytes = File.ReadAllBytes(file);
                PictureOptions o = options.Clone();
                bool gif = GifDecoder.IsGif(bytes);
                int w = 0, h = 0;
                byte[] rgba = gif ? null : PictureImporter.Decode(bytes, out w, out h);
                Busy = true;
                cancel = false;
                ThreadPool.QueueUserWorkItem(_ =>
                {
                    try
                    {
                        EncodedPicture pic = gif ? PictureImporter.EncodeGif(bytes, o, () => cancel) : PictureImporter.Encode(rgba, w, h, o);
                        pic.Name = Path.GetFileName(file);
                        if (pic.Screen != null) pic.Screen.Key = PictureImporter.Stamp(file);   // parked under it when the stack changes
                        MainThread.Post(() => Finish(target, pic, o, done));
                    }
                    catch (Exception e)
                    {
                        MainThread.Post(() => Fail(e));
                    }
                });
            }
            catch (Exception e)
            {
                Fail(e);
            }
        }

        /// <summary>Starts an import from a picture decoded already (RGBA rows top-down, maybe halved: srcW x srcH is the
        /// picture's own size, which sets the plan): no second decode of a photo.</summary>
        public void Start(Sign target, string file, byte[] rgba, int w, int h, int srcW, int srcH, PictureOptions options, Action<Sign> done = null)
        {
            if (Busy) return;
            PictureOptions o = options.Clone();
            Busy = true;
            cancel = false;
            ThreadPool.QueueUserWorkItem(_ =>
            {
                try
                {
                    EncodedPicture pic = PictureImporter.Encode(rgba, w, h, srcW, srcH, o);
                    pic.Name = Path.GetFileName(file);
                    MainThread.Post(() => Finish(target, pic, o, done));
                }
                catch (Exception e)
                {
                    MainThread.Post(() => Fail(e));
                }
            });
        }

        /// <summary>Places a moving GIF whose still is encoded already (the cost line's texts) once its frames are decoded
        /// for the screen on a worker (0.20.132, review: an Update decoded and encoded the still again too). Frames that
        /// could never fit the screens' budget are not decoded: the still alone, and the note says so.</summary>
        public void StartScreen(Sign target, string file, EncodedPicture still, PictureOptions options, Action<Sign> done = null)
        {
            if (Busy) return;
            try
            {
                byte[] bytes = File.ReadAllBytes(file);
                string key = PictureImporter.Stamp(file);
                PictureOptions o = options.Clone();
                Busy = true;
                cancel = false;
                ThreadPool.QueueUserWorkItem(_ =>
                {
                    try
                    {
                        string note = PictureImporter.ScreenNote(bytes);
                        ScreenFrames frames = note == null ? PictureImporter.GifScreen(bytes, () => cancel) : null;
                        if (frames != null) frames.Key = key;                // parked under it when the stack changes
                        EncodedPicture pic = still.With(frames);
                        if (note != null) pic.Note = note;
                        MainThread.Post(() => Finish(target, pic, o, done));
                    }
                    catch (Exception e)
                    {
                        MainThread.Post(() => Fail(e));
                    }
                });
            }
            catch (Exception e)
            {
                Fail(e);
            }
        }

        /// <summary>Places a picture encoded already (the cost line's texts: the very same ones), now, on the main thread.</summary>
        public void Place(Sign target, EncodedPicture pic, PictureOptions options, Action<Sign> done = null)
        {
            if (Busy) return;
            Busy = true;
            cancel = false;
            Finish(target, pic, options.Clone(), done);
        }

        void Finish(Sign target, EncodedPicture pic, PictureOptions o, Action<Sign> done)
        {
            try
            {
                var sw = Stopwatch.StartNew();
                string summary = pic.Summary;
                List<Sign> signs = PictureImporter.Place(target, pic, o);
                string how = o.FitFor(pic.Plan).Describe(o.Glow ? GlowMode.AsMade : GlowMode.Lit);
                string result = $"{SignRecipe.Clean(pic.Name)} is on the sign{how}" + (signs.Count > 1 ? $" over {signs.Count} signs" : "") +
                                $" ({SaveCost.Label(pic.SaveBytes)} in the world save; encoded in {pic.Ms / 1000:0.0} s, placed in {sw.Elapsed.TotalSeconds:0.0} s)" +
                                pic.Note + ".";
                Plugin.Log.LogInfo($"picture placed: {summary}{how}; grid {pic.GridMs:0} ms, encoded in {pic.Ms / 1000:0.0} s, placed in {sw.Elapsed.TotalSeconds:0.0} s ({signs.Count} signs)");
                Busy = false;
                Say(result);
                done?.Invoke(signs[0]);
            }
            catch (Exception e)
            {
                Fail(e);
            }
        }

        void Fail(Exception e)
        {
            Busy = false;
            if (e is OperationCanceledException)
            {
                Say("the import was stopped.");
                return;
            }
            Plugin.Log.LogWarning($"picture: {e}");
            Say("error: " + e.Message);
        }
    }
}
