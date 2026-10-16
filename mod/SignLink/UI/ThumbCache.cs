using System;
using System.Collections.Generic;
using System.IO;

namespace SignLink.UI
{
    /// <summary>The thumbnail files on disk (Thumbs: one *.rgba per key) stay under a cap: when the window closes, the
    /// oldest go until the rest fit, so a player who looks at new photos every day does not grow the cache for ever.
    /// No Unity here (mod/SignLink.Tests).</summary>
    internal static class ThumbCache
    {
        public const long Cap = 100L << 20;

        /// <summary>Deletes the oldest *.rgba files of the folder until the rest hold at most cap bytes; the number
        /// deleted (0 for a missing folder, or one under the cap). A file that cannot be deleted is skipped.</summary>
        public static int Prune(string folder, long cap)
        {
            if (!Directory.Exists(folder)) return 0;
            var files = new List<FileInfo>();
            long total = 0;
            foreach (string f in Directory.GetFiles(folder, "*.rgba"))
            {
                var fi = new FileInfo(f);
                files.Add(fi);
                total += fi.Length;
            }
            if (total <= cap) return 0;
            files.Sort((a, b) => a.LastWriteTimeUtc.CompareTo(b.LastWriteTimeUtc));
            int gone = 0;
            foreach (FileInfo fi in files)
            {
                if (total <= cap) break;
                long bytes = fi.Length;
                try
                {
                    fi.Delete();
                    total -= bytes;
                    gone++;
                }
                catch (Exception) { }
            }
            return gone;
        }
    }
}
