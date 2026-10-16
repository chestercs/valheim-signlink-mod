using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text;

namespace SignLink.Animation
{
    /// <summary>The bytes of a saved animation on a sign (StoredAnims.Key, "signlink_anim"): a versioned header (magic
    /// "SLA1", the frame rate, the frame count, the packed length) and the frames, joined with U+001E and gzipped. No
    /// Unity here (mod/SignLink.Tests checks the round trip and the limits). A blob from any player's sign is read within
    /// caps: frames, the whole (in characters, as it unpacks), one frame; past them it is not played, so a hostile blob
    /// takes neither memory nor time from everyone near it. TryUnpack never throws: a broken blob is false.</summary>
    internal static class AnimBlob
    {
        const int Magic = 0x31414C53;                 // "SLA1"
        public const char FrameSep = '\u001e';
        /// <summary>Text plus blob must stay under the 512 KiB network limit (docs/limits.md).</summary>
        public const int MaxBytes = 400 * 1024;
        public const int MaxFrames = 5000, MaxFrameChars = 512 * 1024;

        /// <summary>The most characters a stored animation's frames hold together (UTF-16, as they take memory): what
        /// StoredAnims.Store packs at most, and what versions before 0.20.132 read at most (16 MB of UTF-8, so every blob
        /// they played still plays; 0.20.132, review).</summary>
        public const int MaxChars = 16 << 20;

        /// <summary>What a blob is read to at most, counted as it unpacks, so a hostile one stops there.</summary>
        public const int MaxReadChars = MaxChars;

        /// <summary>Do the frames fit what Store packs (MaxChars), and how many characters they hold.</summary>
        public static bool Fits(IList<string> frames, out long chars)
        {
            chars = 0;
            foreach (string f in frames) chars += f?.Length ?? 0;
            return chars <= MaxChars;
        }

        public static byte[] Pack(IList<string> frames, float fps)
        {
            var ms = new MemoryStream();
            using (var w = new BinaryWriter(ms))
            {
                w.Write(Magic);
                w.Write(fps);
                w.Write(frames.Count);
                byte[] packed = Deflate(Encoding.UTF8.GetBytes(string.Join(FrameSep.ToString(), frames)));
                w.Write(packed.Length);
                w.Write(packed);
            }
            return ms.ToArray();
        }

        public static bool TryUnpack(byte[] blob, out string[] frames, out float fps)
        {
            frames = null;
            fps = 0;
            if (blob == null || blob.Length < 16) return false;
            try
            {
                using (var r = new BinaryReader(new MemoryStream(blob)))
                {
                    if (r.ReadInt32() != Magic) return false;
                    fps = r.ReadSingle();
                    int n = r.ReadInt32(), length = r.ReadInt32();
                    if (n < 1 || n > MaxFrames || length < 1 || length > blob.Length - 16 || !(fps > 0 && fps <= 60)) return false;
                    frames = Read(new MemoryStream(blob, 16, length, false), n);
                    return frames != null;
                }
            }
            catch (Exception)
            {
                frames = null;
                return false;
            }
        }

        /// <summary>The n frames, read as they unpack (0.20.132, review): the UTF-8 decoded a read at a time (the decoder
        /// keeps a character cut by a read's end for the next) and split on FrameSep as it comes, the frames the same as
        /// decoding the whole and splitting it. Null past MaxReadChars characters in all, a frame past MaxFrameChars, or
        /// another number of frames: a hostile blob stops there, its frames so far the only memory it took (before, its
        /// 16 MB of bytes, then the whole as one string, then split: some 100 MB of garbage a look).</summary>
        static string[] Read(Stream packed, int n)
        {
            using (var gz = new GZipStream(packed, CompressionMode.Decompress))
            {
                Decoder utf8 = Encoding.UTF8.GetDecoder();
                var bytes = new byte[65536];
                var chars = new char[Encoding.UTF8.GetMaxCharCount(bytes.Length)];
                var frames = new List<string>(Math.Min(n, 256));
                var frame = new StringBuilder();
                long total = 0;
                while (true)
                {
                    int read = gz.Read(bytes, 0, bytes.Length);
                    bool end = read <= 0;
                    int got = utf8.GetChars(bytes, 0, end ? 0 : read, chars, 0, end);   // the last call flushes a cut sequence
                    total += got;
                    if (total > MaxReadChars) return null;
                    int start = 0;
                    for (int i = 0; i < got; i++)
                    {
                        if (chars[i] != FrameSep) continue;
                        frame.Append(chars, start, i - start);
                        if (frame.Length > MaxFrameChars || frames.Count >= n - 1) return null;   // the last frame follows the last separator
                        frames.Add(frame.ToString());
                        frame.Clear();
                        start = i + 1;
                    }
                    frame.Append(chars, start, got - start);
                    if (frame.Length > MaxFrameChars) return null;
                    if (end) break;
                }
                frames.Add(frame.ToString());
                return frames.Count == n ? frames.ToArray() : null;
            }
        }

        static byte[] Deflate(byte[] bytes)
        {
            using (var o = new MemoryStream())
            {
                using (var gz = new GZipStream(o, CompressionMode.Compress, true)) gz.Write(bytes, 0, bytes.Length);
                return o.ToArray();
            }
        }
    }
}
