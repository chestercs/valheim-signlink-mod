using System;
using System.Collections.Generic;
using System.Text;

namespace SignLink.Pictures
{
    /// <summary>What sign content adds to the world save. Every sign is one object in a .chunk file, which is not
    /// compressed: its text is stored as UTF-8 with a length prefix, plus the object itself (position, rotation, prefab,
    /// author; about 100 bytes). A saved animation adds its packed frames.</summary>
    internal static class SaveCost
    {
        public const int PerSign = 100;

        public static long Bytes(IList<string> texts, long blobBytes = 0)
        {
            long n = blobBytes > 0 ? blobBytes + 8 : 0;
            foreach (string t in texts) n += PerSign + Encoding.UTF8.GetByteCount(t ?? "");
            return n;
        }

        public static string Label(long bytes) =>
            bytes >= 1048576 ? $"{bytes / 1048576.0:0.##} MB" : bytes >= 1024 ? $"{bytes / 1024.0:0.#} KB" : $"{bytes} bytes";

        /// <summary>The yellow level of the wait (WaitSeconds): noticeable for a player who arrives.</summary>
        public const double NoticeSeconds = 3;

        /// <summary>The red level of the wait: a player who arrives next to it waits this long before it, and the objects
        /// around it, are there; Place asks first.</summary>
        public const double HeavySeconds = 10;

        /// <summary>Seconds a client spends building the text meshes of this many glyphs when the signs load (about
        /// 13.5 µs each, docs/limits.md); the objects of an area are made a few per frame, so it is felt as a freeze.</summary>
        public static double LoadSeconds(long glyphs) => glyphs * GlyphSeconds;

        /// <summary>A count made for pixelsWas pixels scaled to pixelsNow (a picture's bytes and glyphs grow with its
        /// pixels): what its cost is while its new size is being counted (0.20.125, the author: it keeps clicking the
        /// button below, which flashes yellow); -1 when there is nothing to scale.</summary>
        public static long Scaled(long counted, long pixelsWas, long pixelsNow) =>
            counted < 0 || pixelsWas <= 0 ? -1 : (long)Math.Round(counted * (double)pixelsNow / pixelsWas);

        /// <summary>A glyph's mesh building on a client, in seconds (docs/limits.md: about 13.5 µs each).</summary>
        public const double GlyphSeconds = 13.5e-6;

        /// <summary>Seconds to receive this many bytes on a server, at best: the server sends a player the objects around
        /// them nearest first, each whole, over a link capped at 150 KB/s (ZSteamSocket SendRateMin/Max 153600,
        /// ZDOMan.SendZDOs); a big sign holds back everything behind it. The author, 2026-10-11: three very large pictures
        /// kept a base from showing for about 2 minutes after logging in.</summary>
        public static double DownloadSeconds(long bytes) => Math.Max(0, bytes) / (150.0 * 1024);

        /// <summary>The wait of a player who arrives next to it on a server: the download, then the build. bytes &lt; 0:
        /// not counted yet, the build only.</summary>
        public static double WaitSeconds(double buildSeconds, long bytes) => DownloadSeconds(bytes) + buildSeconds;

        /// <summary>0 green, 1 yellow (over NoticeSeconds), 2 red (over HeavySeconds).</summary>
        public static int Level(double waitSeconds) => waitSeconds > HeavySeconds ? 2 : waitSeconds > NoticeSeconds ? 1 : 0;

        /// <summary>The red level: a long wait for every player who arrives, or a sign close to the 512 KiB a player can
        /// receive.</summary>
        public static bool Heavy(double buildSeconds, long bytes, long largestBytes) =>
            Level(WaitSeconds(buildSeconds, bytes)) == 2 || largestBytes > PixelText.NetworkKb * 1024;

        /// <summary>The cost line's sentence (no closing full stop).</summary>
        public static string WaitLine(double buildSeconds, long bytes) =>
            $"Every player who arrives here on a server waits about {WaitSeconds(buildSeconds, bytes):0.#} s for it: " +
            $"{DownloadSeconds(bytes):0.#} s to download (150 KB/s at best), {buildSeconds:0.#} s to build";

        /// <summary>The most SignLink stores on one sign stack in a shared world (texts, signs and a saved animation, as
        /// Bytes counts them): about 2 s for every player who arrives. The author, 2026-10-11, after three large pictures froze
        /// a client on a dedicated server: in multiplayer the limit is 600 KB for an area and 300 KB for
        /// one sign.</summary>
        public const long SignLimit = 300 * 1024;

        /// <summary>The most SignLink's signs around one hold together in a shared world (AreaRadiusM around it, every
        /// player's counted, SignArea): about 4 s for a player who arrives there. Vanilla signs do not count (0.20.67, the
        /// author: count only these into the 600 KB hard limit ... all SignLink posts taken together): the
        /// game's editor keeps each small.</summary>
        public const long AreaLimit = 600 * 1024;

        /// <summary>The area of AreaLimit: this many metres around the sign.</summary>
        public const int AreaRadiusM = 100;

        /// <summary>May SignLink store this much on one stack? Single player: anything; a shared world: up to SignLimit
        /// (bytes &lt; 0, not counted yet: no).</summary>
        public static bool Fits(long bytes, bool shared) => !shared || (bytes >= 0 && bytes <= SignLimit);

        /// <summary>Why a stack of this size is refused in a shared world (no closing full stop).</summary>
        public static string OverLimit(long bytes) =>
            $"{Label(bytes)} is over the {Label(SignLimit)} SignLink stores on one sign in a multiplayer world";

        /// <summary>May a stack that holds oldBytes now hold newBytes, with others bytes on the other signs of its area?
        /// Single player: always. A shared world: within SignLimit (a stack over it can only become one under it), and
        /// within AreaLimit unless it is no bigger than what the stack holds now (a full area can be made lighter). why:
        /// the reason when not.</summary>
        public static bool Allowed(long newBytes, long oldBytes, long others, bool shared, out string why)
        {
            why = null;
            if (!shared) return true;
            if (newBytes < 0) why = "not counted yet";
            else if (newBytes > SignLimit) why = OverLimit(newBytes);
            else if (newBytes > Math.Max(0, oldBytes) && others + newBytes > AreaLimit)
                why = $"the SignLink signs within {AreaRadiusM} m hold {Label(others)} already: with {Label(newBytes)} more that is " +
                      $"{Label(others + newBytes)}, over the {Label(AreaLimit)} multiplayer limit for an area";
            return why == null;
        }

        /// <summary>The area meter's words: what the signs within AreaRadiusM hold now against the limit, and what they
        /// would hold with the picked content in place of the sign's (when that differs).</summary>
        public static string AreaLine(long now, long after) =>
            $"{Label(now)} / {Label(AreaLimit)} multiplayer limit within {AreaRadiusM} m" +
            (after != now ? $"; with this: {Label(after)}" : "");

        /// <summary>The visible glyphs of sign texts, what TMP builds a mesh for: characters outside tags (read as TMP reads
        /// them, like SignGlow.Strip), spaces and line breaks not counted.</summary>
        public static long Glyphs(IList<string> texts)
        {
            long n = 0;
            foreach (string t in texts)
            {
                if (string.IsNullOrEmpty(t)) continue;
                int i = 0;
                while (i < t.Length)
                {
                    char c = t[i];
                    if (c == '<')
                    {
                        int end = t.IndexOfAny(Brackets, i + 1);
                        if (end > 0 && t[end] == '>')
                        {
                            // a sprite is a tag that draws (0.20.151, review: counted as nothing, a frame of sprites passed
                            // every check of how long a stored animation takes to show)
                            if (end - i > 6 && string.Compare(t, i + 1, "sprite", 0, 6, StringComparison.OrdinalIgnoreCase) == 0) n++;
                            i = end + 1;
                            continue;
                        }
                    }
                    if (c > ' ' && !char.IsLowSurrogate(c)) n++;
                    i++;
                }
            }
            return n;
        }

        /// <summary>A tag's ends, for IndexOfAny (SignGlow too).</summary>
        internal static readonly char[] Brackets = { '<', '>' };
    }
}
