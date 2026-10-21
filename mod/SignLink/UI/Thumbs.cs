using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using BepInEx;
using SignLink.Pictures;
using SignLink.Presets;
using UnityEngine;

namespace SignLink.UI
{
    /// <summary>Thumbnails for the lists (at most Side pixels a side): made one at a time while a list shows (the others
    /// draw an empty box meanwhile), kept in memory (about 200) and on disk (BepInEx/cache/SignLink/thumbs, a raw RGBA
    /// file per key: read on a worker, uploaded in one call, no decoder). A key names the file with its size and date
    /// (or a piece's path and the library version). The main thread only ever uploads a ready buffer, except for the
    /// game's own decode of a small JPEG (an EXIF preview) or of a picture up to MaxDecodePixels without one; bigger
    /// pictures keep an empty box with their size until they are picked, when the decoded picture is handed in (Put):
    /// a full decode of a 24 MP photo is 0.5-1.5 s, and thirty of them in a row would be thirty freezes.</summary>
    internal static class Thumbs
    {
        public const int Side = 96, Version = 1;
        public const long MaxDecodePixels = 4000000;
        /// <summary>A JPEG's EXIF preview larger than this is not decoded (the picture itself is, up to MaxDecodePixels).</summary>
        public const long MaxPreviewPixels = 1000000;
        const int Keep = 200, Drop = 50;

        public static string Folder => Path.Combine(Paths.CachePath, Path.Combine("SignLink", "thumbs"));

        /// <summary>What a thumbnail's work returns: RGBA rows top-down, at most Side a side.</summary>
        public sealed class Source
        {
            public byte[] Rgba;
            public int W, H;
        }

        sealed class Entry
        {
            public Texture2D Tex;
            public float Used;
            public bool Failed;
        }

        static readonly Dictionary<string, Entry> cache = new Dictionary<string, Entry>();
        static readonly Job<Source> job = new Job<Source>();
        static int frame = -1;
        static bool startedThisFrame;

        public static bool Has(string key) => cache.TryGetValue(key, out Entry e) && e.Tex;

        /// <summary>The thumbnail for a key if it is made (any event; nothing is started).</summary>
        public static Texture2D Peek(string key) => key != null && cache.TryGetValue(key, out Entry e) ? e.Tex : null;

        /// <summary>The thumbnail for a key, or null while it is not made yet: then one is started, if none is running
        /// and none was started this frame (Layout events only). begin runs on the main thread and returns the pool work.</summary>
        public static Texture2D Get(string key, Func<Func<Source>> begin, float now)
        {
            if (key == null) return null;
            if (cache.TryGetValue(key, out Entry e))
            {
                e.Used = now;
                return e.Tex;
            }
            Start(key, begin, now);
            return null;
        }

        /// <summary>A thumbnail from a picture decoded already (the picked file, too big to decode for its row alone):
        /// shrunk on the pool. True when started or there already; false when the worker is busy (try next Layout).</summary>
        public static bool Put(string key, byte[] rgba, int w, int h, float now)
        {
            if (key == null || Has(key)) return true;
            if (job.Busy) return false;
            cache.Remove(key);                                              // a failed entry gives way
            return Start(key, ShrinkWork(rgba, w, h), now);
        }

        /// <summary>Put's work, made only when it starts (the closure over its arguments was made at every Layout the picked
        /// picture showed, 0.20.132, review).</summary>
        static Func<Func<Source>> ShrinkWork(byte[] rgba, int w, int h) => () => () => Shrink(rgba, w, h);

        static bool Start(string key, Func<Func<Source>> begin, float now)
        {
            if (Time.frameCount != frame)
            {
                frame = Time.frameCount;
                startedThisFrame = false;
            }
            if (job.Busy || startedThisFrame) return false;
            string file = Path.Combine(Folder, Hash(key) + ".rgba");
            startedThisFrame = true;
            bool cached = false;
            job.Forget();
            job.Want(key, now, 0f,
                () =>
                {
                    cached = File.Exists(file);
                    Func<Source> work = begin();
                    return () =>
                    {
                        if (cached)
                        {
                            try { return ReadRaw(file); }                   // the disk cache
                            catch (Exception)
                            {
                                cached = false;                             // a bad file: made again, and written again
                                try { File.Delete(file); }
                                catch (Exception) { }
                            }
                        }
                        return work();
                    };
                },
                src => Deliver(key, src, Time.unscaledTime, cached ? null : file),   // used when delivered: a slow one is
                ex =>                                                               // not the oldest at once (0.20.132, review)
                {
                    cache[key] = new Entry { Failed = true, Used = Time.unscaledTime };
                    Plugin.Log.LogDebug($"thumbnail {key}: {ex.Message}");
                });
            return true;
        }

        /// <summary>8 bytes of header (int W, int H) then W x H x 4 bytes; refuses a side over Side or a short file.</summary>
        static Source ReadRaw(string file)
        {
            byte[] bytes = PictureImporter.ReadFile(file);
            if (bytes.Length < 8) throw new InvalidDataException("a short thumbnail file");
            int w = BitConverter.ToInt32(bytes, 0), h = BitConverter.ToInt32(bytes, 4);
            if (w <= 0 || h <= 0 || w > Side || h > Side || bytes.Length != 8 + w * h * 4) throw new InvalidDataException("a bad thumbnail file");
            var rgba = new byte[w * h * 4];
            Buffer.BlockCopy(bytes, 8, rgba, 0, rgba.Length);
            return new Source { Rgba = rgba, W = w, H = h };
        }

        /// <summary>The texture from the rows (flipped: Unity's go bottom-up), kept; the file written on a worker when
        /// it was not there.</summary>
        static void Deliver(string key, Source src, float now, string writeTo)
        {
            if (src == null || src.Rgba == null || src.W <= 0 || src.H <= 0 || src.W > Side || src.H > Side || src.Rgba.Length < src.W * src.H * 4)
            {
                cache[key] = new Entry { Failed = true, Used = now };
                return;
            }
            int row = src.W * 4;
            var flipped = new byte[row * src.H];
            for (int y = 0; y < src.H; y++) Buffer.BlockCopy(src.Rgba, y * row, flipped, (src.H - 1 - y) * row, row);
            var tex = new Texture2D(src.W, src.H, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.HideAndDontSave,
            };
            tex.LoadRawTextureData(flipped);
            tex.Apply(false, true);                                         // only drawn: no CPU copy kept (0.20.132, review)
            Remember(key, tex, now);
            if (writeTo == null) return;
            byte[] rgba = src.Rgba;
            int w = src.W, h = src.H;
            ThreadPool.QueueUserWorkItem(_ =>
            {
                try
                {
                    Directory.CreateDirectory(Folder);
                    var bytes = new byte[8 + w * h * 4];
                    Buffer.BlockCopy(BitConverter.GetBytes(w), 0, bytes, 0, 4);
                    Buffer.BlockCopy(BitConverter.GetBytes(h), 0, bytes, 4, 4);
                    Buffer.BlockCopy(rgba, 0, bytes, 8, w * h * 4);
                    File.WriteAllBytes(writeTo, bytes);
                }
                catch (Exception ex)
                {
                    Plugin.Log.LogDebug($"thumbnail cache: {ex.Message}");
                }
            });
        }

        static void Remember(string key, Texture2D tex, float now)
        {
            if (cache.TryGetValue(key, out Entry old) && old.Tex) UnityEngine.Object.Destroy(old.Tex);
            cache[key] = new Entry { Tex = tex, Used = now };
            if (cache.Count <= Keep) return;
            var oldest = new List<KeyValuePair<string, Entry>>(cache);     // the least recently used go
            oldest.Sort((a, b) => a.Value.Used.CompareTo(b.Value.Used));
            for (int i = 0; i < Drop && i < oldest.Count; i++)
            {
                if (oldest[i].Value.Used >= now - 1f) break;                // shown now: kept, the cache grows instead (0.20.130)
                if (oldest[i].Value.Tex) UnityEngine.Object.Destroy(oldest[i].Value.Tex);
                cache.Remove(oldest[i].Key);
            }
        }

        static volatile bool pruning;

        /// <summary>Every texture goes (the window closed, or the plugin is destroyed), and the disk cache is pruned to
        /// its cap on a worker (ThumbCache).</summary>
        public static void Clear()
        {
            foreach (Entry e in cache.Values)
                if (e.Tex) UnityEngine.Object.Destroy(e.Tex);
            cache.Clear();
            job.Abandon();
            if (pruning) return;
            pruning = true;
            string folder = Folder;
            ThreadPool.QueueUserWorkItem(_ =>
            {
                try
                {
                    int gone = ThumbCache.Prune(folder, ThumbCache.Cap);
                    if (gone > 0) Plugin.Log.LogInfo($"thumbnail cache: {gone} old file(s) removed (over {ThumbCache.Cap >> 20} MB)");
                }
                catch (Exception ex) { Plugin.Log.LogDebug($"thumbnail cache: {ex.Message}"); }
                finally { pruning = false; }
            });
        }

        static string Hash(string key)
        {
            using (var sha = SHA1.Create())
            {
                byte[] digest = sha.ComputeHash(Encoding.UTF8.GetBytes(key + "|v" + Version));
                var sb = new StringBuilder(digest.Length * 2);
                foreach (byte b in digest) sb.Append(b.ToString("x2"));
                return sb.ToString();
            }
        }

        static Source Shrink(byte[] rgba, int w, int h)
        {
            byte[] small = Resampler.Box(rgba, w, h, Side, out int sw, out int sh);
            return new Source { Rgba = small, W = sw, H = sh };
        }

        /// <summary>The work for a picture file (PNG, JPG or GIF): a GIF's first frame; a JPEG's EXIF preview when it has
        /// one (turned as the photo is, its letterbox bars cropped away; a preview of another shape is a stale one and is
        /// not used); else the whole picture up to MaxDecodePixels, decoded by the game; else nothing (an empty box with
        /// the size until the file is picked).</summary>
        public static Func<Source> FromFile(string path) => () =>
        {
            byte[] bytes = PictureImporter.ReadFile(path);
            if (GifDecoder.IsGif(bytes))
            {
                GifImage g = GifDecoder.Decode(bytes, maxFrames: 1);
                return Shrink(PictureImporter.GifFrameRgba(g.Frames[0]), g.Width, g.Height);
            }
            if (!ImageHeader.Size(bytes, out int w, out int h, out int orientation)) throw new ArgumentException("its header cannot be read");
            bool swap = orientation >= 5;
            int pw = swap ? h : w, ph = swap ? w : h;                       // the photo as shown
            // the preview's own header first: a crafted one declaring 16k x 16k made the game allocate a gigabyte on the
            // main thread (0.20.132, review); real ones are about 160 x 120
            if (ImageHeader.Thumbnail(bytes, out byte[] small) && ImageHeader.Size(small, out int sw, out int sh, out _) &&
                (long)sw * sh <= MaxPreviewPixels)
            {
                try
                {
                    PictureImporter.RawTexture raw = MainThread.Run(() => PictureImporter.ReadRaw(small));
                    byte[] rgba = RawPixels.ToRgba(raw.Bytes, raw.Format, raw.W, raw.H, 0, out int tw, out int th);
                    rgba = ImageHeader.Orient(rgba, tw, th, orientation, out tw, out th);
                    byte[] cropped = CropToAspect(rgba, tw, th, pw, ph, out int cw, out int ch);
                    if (cropped != null) return Shrink(cropped, cw, ch);
                }
                catch (Exception) { }                                       // a broken preview: the picture itself, if small
            }
            if ((long)w * h > MaxDecodePixels) throw new ArgumentException($"{(long)w * h / 1000000} MP: made when it is picked");
            PictureImporter.RawTexture full = MainThread.Run(() => PictureImporter.ReadRaw(bytes));
            byte[] px = RawPixels.ToRgba(full.Bytes, full.Format, full.W, full.H, 0, out int fw, out int fh);
            px = ImageHeader.Orient(px, fw, fh, orientation, out fw, out fh);
            return Shrink(px, fw, fh);
        };

        /// <summary>A preview the shape of the photo: as it is when the shapes agree within 5 %; its letterbox bars (DCF
        /// previews are 4:3, a 16:9 photo gets black bands) cropped away when the bands are dark; null when the bands
        /// hold a picture, which makes it another photo's preview.</summary>
        static byte[] CropToAspect(byte[] rgba, int w, int h, int pw, int ph, out int cw, out int ch)
        {
            cw = w;
            ch = h;
            double have = w / (double)h, want = pw / (double)ph;
            if (Math.Abs(have / want - 1) <= 0.05) return rgba;
            int x0 = 0, y0 = 0;
            if (have < want) ch = Math.Max(1, (int)Math.Round(w / want));     // taller than the photo: rows go
            else cw = Math.Max(1, (int)Math.Round(h * want));                 // wider: columns go
            y0 = (h - ch) / 2;
            x0 = (w - cw) / 2;
            long sum = 0, count = 0;                                            // how bright the bands are
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    if (x >= x0 && x < x0 + cw && y >= y0 && y < y0 + ch) continue;
                    int o = (y * w + x) * 4;
                    sum += rgba[o] + rgba[o + 1] + rgba[o + 2];
                    count++;
                }
            if (count == 0 || sum / (3.0 * count) > 24) return null;
            var o2 = new byte[cw * ch * 4];
            for (int y = 0; y < ch; y++) Buffer.BlockCopy(rgba, ((y0 + y) * w + x0) * 4, o2, y * cw * 4, cw * 4);
            return o2;
        }

        /// <summary>The work for a piece: its render (the PNG beside it) decoded by the game, else its first pixel grid
        /// read from its text; glyph art without a render has no thumbnail.</summary>
        public static Func<Source> FromPiece(Preset p) => () =>
        {
            if (p.ReadPicture != null)
            {
                byte[] bytes = p.ReadPicture();
                if (!ImageHeader.Size(bytes, out int rw, out int rh, out _) || (long)rw * rh > PictureImporter.MaxRenderPixels)
                    throw new ArgumentException("no render to read");
                PictureImporter.RawTexture raw = MainThread.Run(() => PictureImporter.ReadRaw(bytes));
                byte[] rgba = RawPixels.ToRgba(raw.Bytes, raw.Format, raw.W, raw.H, 0, out int tw, out int th);
                return Shrink(rgba, tw, th);
            }
            Preset.Content c = p.Load();
            var input = new List<IList<string>> { p.Kind == Preset.Kinds.Anim ? new[] { c.Texts[0] } : (IList<string>)c.Texts };
            if (!TextReadback.ReadFrames(input, out int w, out int h, out List<int[]> grids, asSeen: true)) throw new ArgumentException("no quick view");
            return Shrink(GridRgba(grids[0], w, h), w, h);
        };

        /// <summary>RGBA rows top-down of a grid (rows top-down, PixelText.Pack): empty pixels clear.</summary>
        static byte[] GridRgba(int[] px, int w, int h)
        {
            var rgba = new byte[w * h * 4];
            for (int i = 0; i < px.Length; i++)
            {
                int c = px[i];
                if (!PixelText.Drawn(c)) continue;
                int o = i * 4;
                rgba[o] = (byte)(c >> 16);
                rgba[o + 1] = (byte)(c >> 8);
                rgba[o + 2] = (byte)c;
                rgba[o + 3] = (byte)PixelText.Alpha(c);
            }
            return rgba;
        }
    }
}
