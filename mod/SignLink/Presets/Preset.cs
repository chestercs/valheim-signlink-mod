using System;
using System.Globalization;
using PetyusPolisz.Kit;

namespace SignLink.Presets
{
    /// <summary>Ready sign content: built-in art (ArtLibrary). Its file name
    /// tells the kind:
    ///   NAME.txt         one sign text
    ///   NAME.stack.txt   texts separated by U+001D: the sign and copies stacked on it (a picture too big for one sign,
    ///                    or a glyph piece whose paint levels lie one behind the other)
    ///   NAME.anim.txt    "#signlink anim fps=F" on the first line, then frames separated by U+001E
    /// Texts are relative to the sign they go on, so any sign can take them. NAME.png beside one is its quick view
    /// (glyph art cannot be read back from its text); NAME.fit holds "centre=X,Y", where its middle sits as made (wu
    /// from the sign's centre), so it can turn around its own centre (Fit).</summary>
    internal sealed class Preset
    {
        public enum Kinds { Text, Stack, Anim }

        /// <summary>The content: the texts (one, the stack's, or the frames) and the frame rate.</summary>
        internal sealed class Content
        {
            public string[] Texts;
            public float Fps;
        }

        const char FrameSep = '\u001e', SignSep = '\u001d';
        const string AnimHeader = "#signlink anim";

        public string Name, Category, Path;
        /// <summary>Pixel art (its quick view is read back from its text), else glyph art (its render beside it).</summary>
        public bool Pixel;
        public Kinds Kind;
        public long Bytes;
        public Func<string> ReadText;
        /// <summary>The render beside it, or null.</summary>
        public Func<byte[]> ReadPicture;
        /// <summary>The NAME.fit beside it, or null.</summary>
        public Func<string> ReadFit;

        /// <summary>Where its middle sits as made (NAME.fit), if known.</summary>
        public bool Centre(out double x, out double y)
        {
            x = y = 0;
            try { return ReadFit != null && ParseFit(ReadFit(), out x, out y); }
            catch (Exception) { return false; }
        }

        /// <summary>A piece's file is hand-made or shared: these keep one from creating thousands of signs or exhausting
        /// memory. Positions and sizes in wu are finite and within MaxWu; a file is at most MaxBytes; a stack holds at
        /// most MaxSigns signs, an animation at most MaxFrames frames.</summary>
        public const double MaxWu = 32767;
        public const long MaxBytes = 32L << 20;
        public const int MaxSigns = 64, MaxFrames = 5000;

        static bool Wu(string s, out double v) =>
            double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out v) && !double.IsNaN(v) && !double.IsInfinity(v) &&
            Math.Abs(v) <= MaxWu;

        public static bool ParseFit(string text, out double x, out double y)
        {
            x = y = 0;
            foreach (string line in (text ?? "").Split('\n'))
            {
                string l = line.Trim();
                if (!l.StartsWith("centre=", StringComparison.Ordinal)) continue;
                string[] xy = l.Substring(7).Split(',');
                return xy.Length == 2 && Wu(xy[0], out x) && Wu(xy[1], out y);
            }
            return false;
        }

        /// <summary>The piece's size as made in wu (NAME.fit "size=w,h", written for the built-in art by art_centres.py).</summary>
        public bool Size(out double w, out double h)
        {
            w = h = 0;
            try
            {
                foreach (string line in (ReadFit?.Invoke() ?? "").Split('\n'))
                {
                    string l = line.Trim();
                    if (!l.StartsWith("size=", StringComparison.Ordinal)) continue;
                    string[] wh = l.Substring(5).Split(',');
                    return wh.Length == 2 && Wu(wh[0], out w) && Wu(wh[1], out h) && w > 0 && h > 0;
                }
            }
            catch (Exception) { }
            return false;
        }

        public string KindLabel => Kind == Kinds.Anim ? "animation" : Kind == Kinds.Stack ? "several signs" : "one sign";

        public string SizeLabel => Bytes >= 1048576 ? $"{Bytes / 1048576.0:0.#} MB" : $"{Math.Max(1, Bytes / 1024)} KB";
        /// <summary>Its thumbnail's key: an update that redraws a piece gives it a new one (0.20.152, review: the path alone
        /// kept the old render in the disk cache for good). Made once.</summary>
        public string ThumbKey => thumbKey ?? (thumbKey = Path + "|" + Bytes + "|" + typeof(Preset).Assembly.GetName().Version);
        string thumbKey;

        /// <summary>The kind and the name from a file name ("Cat (still).txt", "Fjord.anim.txt").</summary>
        public static bool FromFileName(string file, out string name, out Kinds kind)
        {
            name = null;
            kind = Kinds.Text;
            if (file.EndsWith(".anim.txt", StringComparison.OrdinalIgnoreCase)) { kind = Kinds.Anim; name = file.Substring(0, file.Length - 9); }
            else if (file.EndsWith(".stack.txt", StringComparison.OrdinalIgnoreCase)) { kind = Kinds.Stack; name = file.Substring(0, file.Length - 10); }
            else if (file.EndsWith(".txt", StringComparison.OrdinalIgnoreCase)) name = file.Substring(0, file.Length - 4);
            return name != null;
        }

        /// <summary>The piece a recipe names: by its path, else by its name when only one piece has it (0.19.0 wrote
        /// names; two built-in pieces are called Dragon ship); -1 when none.</summary>
        public static int Find(System.Collections.Generic.List<Preset> items, string id)
        {
            if (string.IsNullOrEmpty(id)) return -1;
            int i = items.FindIndex(p => p.Path == id);
            if (i >= 0) return i;
            i = items.FindIndex(p => p.Name == id);
            return i >= 0 && items.FindLastIndex(p => p.Name == id) == i ? i : -1;
        }

        /// <summary>Reads the piece; a file over MaxBytes is refused before it is read, a stack over MaxSigns and an
        /// animation over MaxFrames after.</summary>
        public Content Load()
        {
            if (Bytes > MaxBytes)
                throw new ArgumentException(Lang.F("{0}: {1:0.#} MB is too large to be a sign piece (up to {2} MB)", Name, Bytes / 1048576.0, MaxBytes / 1048576));
            string all = ReadText();
            switch (Kind)
            {
                case Kinds.Stack:
                    string[] texts = all.Split(SignSep);
                    if (texts.Length > MaxSigns) throw new ArgumentException(Lang.F("{0}: {1} signs is too large to be a sign piece (up to {2})", Name, texts.Length, MaxSigns));
                    return new Content { Texts = texts };
                case Kinds.Anim:
                    int nl = all.IndexOf('\n');
                    if (!all.StartsWith(AnimHeader, StringComparison.Ordinal) || nl < 0) throw new ArgumentException(Lang.F("{0}: no '{1} fps=F' first line", Name, AnimHeader));
                    float fps = 4f;
                    foreach (string part in all.Substring(0, nl).Split(' '))
                        if (part.StartsWith("fps=", StringComparison.Ordinal) &&
                            float.TryParse(part.Substring(4), NumberStyles.Float, CultureInfo.InvariantCulture, out float f) && f > 0 && f <= 60)
                            fps = f;
                    string[] frames = all.Substring(nl + 1).Split(FrameSep);
                    if (frames.Length > MaxFrames) throw new ArgumentException(Lang.F("{0}: {1} frames is too large to be a sign piece (up to {2})", Name, frames.Length, MaxFrames));
                    return new Content { Texts = frames, Fps = fps };
                default:
                    return new Content { Texts = new[] { all } };
            }
        }
    }
}
