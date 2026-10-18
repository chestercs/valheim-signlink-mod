using System;
using System.IO;

namespace SignLink.Pictures
{
    /// <summary>The names pictures go by on signs and in requests (1.0.2, the owner: folders copied into the images folder,
    /// walked in the window): a picture's path under the images folder with '/' between the folders ("cat.gif",
    /// "funny/cat.gif"), the same on every computer whatever its system writes. No Unity here: mod/SignLink.Tests checks
    /// them, as a name from another player's sign must name nothing outside the folder.</summary>
    internal static class PictureNames
    {
        /// <summary>A picture's name from its path: under root with '/' between the folders; a path elsewhere gives its file
        /// name, one the system cannot read "".</summary>
        public static string Relative(string root, string file)
        {
            if (string.IsNullOrEmpty(file)) return "";
            try
            {
                string top = Top(root), full = Path.GetFullPath(file);
                if (full.StartsWith(top + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                    return full.Substring(top.Length + 1).Replace(Path.DirectorySeparatorChar, '/').Replace(Path.AltDirectorySeparatorChar, '/');
                return Path.GetFileName(file);
            }
            catch (Exception) { return ""; }
        }

        /// <summary>The folder part of a name ("" for a picture in the images folder itself).</summary>
        public static string DirOf(string name)
        {
            int cut = string.IsNullOrEmpty(name) ? -1 : name.LastIndexOf('/');
            return cut < 0 ? "" : name.Substring(0, cut);
        }

        /// <summary>The path under root a name means ("" root itself; "funny/cat.gif" a file or a folder in it), or null when it
        /// is no such name: rooted, a '\' or ':' in it, an empty step or one of spaces, '.' or '..', a name Windows keeps for a
        /// device (Reserved), or characters a file name cannot hold. The path need not exist.</summary>
        public static string Under(string root, string name)
        {
            try
            {
                string top = Top(root);
                if (string.IsNullOrEmpty(name)) return top;
                if (name.IndexOf('\\') >= 0 || name.IndexOf(':') >= 0 || name.StartsWith("/", StringComparison.Ordinal)) return null;
                foreach (string part in name.Split('/'))
                    if (part.Trim().Length == 0 || part == "." || part == ".." || Reserved(part) || part.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 ||
                        part.EndsWith(".", StringComparison.Ordinal) || part.EndsWith(" ", StringComparison.Ordinal))   // Windows drops a trailing dot
                        return null;                                        // or space: another name than written (review)
                string full = Path.GetFullPath(Path.Combine(top, name.Replace('/', Path.DirectorySeparatorChar)));
                return full.StartsWith(top + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) ? full : null;
            }
            catch (Exception) { return null; }                               // characters the system does not take in a path
        }

        static string Top(string root) => Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

        /// <summary>CON, PRN, AUX, NUL, COM0-9 and LPT0-9, with or without an extension: Windows opens a device for such a name
        /// in any folder (reading CON waits on the console), so another player's recipe must not name one (0.20.132, review).</summary>
        public static bool Reserved(string name)
        {
            int dot = name.IndexOf('.');
            string stem = (dot < 0 ? name : name.Substring(0, dot)).TrimEnd(' ').ToUpperInvariant();
            if (stem == "CON" || stem == "PRN" || stem == "AUX" || stem == "NUL") return true;
            return stem.Length == 4 && (stem.StartsWith("COM", StringComparison.Ordinal) || stem.StartsWith("LPT", StringComparison.Ordinal)) &&
                   "0123456789¹²³".IndexOf(stem[3]) >= 0;
        }
    }
}
