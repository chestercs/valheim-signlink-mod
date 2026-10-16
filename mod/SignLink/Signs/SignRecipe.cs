using System;
using System.Globalization;
using SignLink.Pictures;

namespace SignLink.Signs
{
    /// <summary>What put content on a stack's first sign (SignMarks.SourceKey, "signlink_src"), so the source that did it
    /// can edit it again: "pic|width|pitch|cover|alpha|glow|animate|file" (the Picture source; the file last), or since
    /// 0.20.149 "pic|width|pitch|cover|alpha|glow|animate|resolution|file" (the player's Resolution: a Windows file name
    /// holds no '|'), "art|Light|path" (a built-in piece's path; 0.19.0 wrote its name; "art|Light|still|path" an animated
    /// piece put as its first frame, 0.20.144), "text" (the Text source). Parse never
    /// throws: anything else, and a picture recipe with numbers outside what the window offers, is Kind None ("saved|..."
    /// too: Saved went in 0.20.63, nothing of it is kept; such a sign is plain content). No Unity here (mod/SignLink.Tests).</summary>
    internal sealed class SignRecipe
    {
        public enum Kinds { None, Picture, Art, Text }

        public Kinds Kind;
        /// <summary>Picture: the file's name (no folder).</summary>
        public string File = "";
        /// <summary>Art: the piece's resource name (0.19.0 wrote its name).</summary>
        public string Path = "";
        /// <summary>Art: how it was lit.</summary>
        public GlowMode Light = GlowMode.AsMade;
        /// <summary>Art: an animated piece put as a still, its first frame (0.20.144).</summary>
        public bool Still;
        /// <summary>Picture: its settings (PictureOptions): the width and the pixel it was made with (since 0.20.35 the
        /// plan's own, so a Detail-model picture reads back as exactly its grid).</summary>
        public float WidthM, Pitch, Cover;
        public bool Transparent, Glow, Animate;
        /// <summary>Picture: the Resolution the player chose (PictureOptions.Detail), 0 when not known (before 0.20.149): the
        /// grid on the sign may have a few pixels more or less (Fine Width), and loading the grid as the Resolution let it
        /// creep down at every update (0.20.149, the author's test).</summary>
        public int Detail;

        public static SignRecipe None => new SignRecipe();

        /// <summary>The widest picture a recipe holds (m): Parse reads anything wider as no recipe.</summary>
        public const float MaxWidthM = 300f;

        public static SignRecipe Parse(string src)
        {
            var r = new SignRecipe();
            if (string.IsNullOrEmpty(src)) return r;
            if (src == "text")
            {
                r.Kind = Kinds.Text;
                return r;
            }
            bool pic = src.StartsWith("pic|", StringComparison.Ordinal);
            string[] p = src.Split(new[] { '|' }, pic ? 9 : 3);
            var c = CultureInfo.InvariantCulture;
            const NumberStyles f = NumberStyles.Float;
            if (pic)
            {
                // 0.20.149: the Resolution before the file; a field there that is no number is part of an older recipe's
                // file name (one written elsewhere may hold '|')
                bool resolution = p.Length == 9 && int.TryParse(p[7], NumberStyles.Integer, c, out r.Detail);
                string file = resolution ? p[8] : p.Length == 9 ? p[7] + "|" + p[8] : p.Length == 8 ? p[7] : "";
                if (p.Length < 8 || !float.TryParse(p[1], f, c, out r.WidthM) || !float.TryParse(p[2], f, c, out r.Pitch) ||
                    !float.TryParse(p[3], f, c, out r.Cover) || file.Length == 0) return None;
                if (resolution && (r.Detail < 0 || r.Detail > 100000)) return None;
                // numbers from another player's sign: finite and in the ranges the window offers, else no recipe
                if (!(r.WidthM >= 0.001f && r.WidthM <= MaxWidthM) || !PictureOptions.ValidPitch(r.Pitch) ||
                    !(r.Cover == 0f || r.Cover == 2f)) return None;
                r.Transparent = p[4] == "1";
                r.Glow = p[5] == "1";
                r.Animate = p[6] == "1";
                r.File = file;
                r.Kind = Kinds.Picture;
                return r;
            }
            if (p[0] == "art" && p.Length == 3 && p[2].Length > 0)
            {
                r.Kind = Kinds.Art;
                r.Light = Enum.IsDefined(typeof(GlowMode), p[1]) ? (GlowMode)Enum.Parse(typeof(GlowMode), p[1]) : GlowMode.AsMade;
                r.Still = p[2].StartsWith("still|", StringComparison.Ordinal);
                r.Path = r.Still ? p[2].Substring(6) : p[2];
                if (r.Path.Length == 0) return None;
                return r;
            }
            return None;
        }

        public override string ToString()
        {
            switch (Kind)
            {
                case Kinds.Picture:
                    return Detail > 0
                        ? string.Join("|", new[] { "pic", Num(WidthM), Num(Pitch), Num(Cover), Transparent ? "1" : "0", Glow ? "1" : "0",
                                                   Animate ? "1" : "0", Detail.ToString(CultureInfo.InvariantCulture), File })
                        : string.Join("|", new[] { "pic", Num(WidthM), Num(Pitch), Num(Cover), Transparent ? "1" : "0", Glow ? "1" : "0",
                                                   Animate ? "1" : "0", File });
                case Kinds.Art: return "art|" + Light + "|" + (Still ? "still|" : "") + Path;
                case Kinds.Text: return "text";
                default: return "";
            }
        }

        static string Num(float v) => v.ToString("R", CultureInfo.InvariantCulture);

        public static SignRecipe ForPicture(PictureOptions o, string file) => new SignRecipe
        {
            Kind = Kinds.Picture, WidthM = o.WidthM, Pitch = o.Pitch, Cover = o.Cover, Transparent = o.Transparent, Glow = o.Glow,
            Animate = o.Animate, File = file ?? "", Detail = Math.Max(0, o.Detail),
        };

        /// <summary>A picture as its plan made it: the plan's own width and pixel (not the width asked for, nor the
        /// Detail model's unused Pitch), so the recipe reads back as exactly its grid (Apply, PlanFor).</summary>
        public static SignRecipe ForPicture(PictureOptions o, PicturePlan plan, string file)
        {
            SignRecipe r = ForPicture(o, file);
            r.WidthM = plan.WidthM;
            r.Pitch = plan.Pitch;
            return r;
        }

        /// <summary>A built-in piece by its resource name; still: an animated one as its first frame.</summary>
        public static SignRecipe ForPiece(Kinds kind, GlowMode light, string path, bool still = false) =>
            new SignRecipe { Kind = kind, Light = light, Path = path ?? "", Still = still };

        public static SignRecipe ForText() => new SignRecipe { Kind = Kinds.Text };

        /// <summary>A name or file name from another player's sign as the window may show it: no tags, no control
        /// characters, at most 80 characters.</summary>
        public static string Clean(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            var sb = new System.Text.StringBuilder(Math.Min(s.Length, 80));
            foreach (char c in s)
            {
                if (sb.Length >= 80) break;
                if (c == '<' || c == '>' || c < ' ' || c == '\u007f') continue;
                sb.Append(c);
            }
            return sb.ToString();
        }

        /// <summary>The picture's settings into options (what the Picture source edits from).</summary>
        public void Apply(PictureOptions o)
        {
            o.WidthM = WidthM;
            o.Pitch = PictureOptions.ValidPitch(Pitch) ? Pitch : PictureOptions.Pitches[0];
            o.Detail = Detail;                                              // the player's Resolution when the recipe has it (checked against
            o.DetailAuto = false;                                           // the grid in PictureSource.Tick), else 0: the window reads it
                                                                            // from the grid its width and pixel make
            o.Cover = Cover;
            o.Transparent = Transparent;
            o.Glow = Glow;
            o.Animate = Animate;
            o.Place = Placement.Custom;
        }

        /// <summary>The same thing (the same file, piece or text source), whatever its settings.</summary>
        public bool SameItem(SignRecipe o) =>
            o != null && Kind == o.Kind && Kind != Kinds.None &&
            (Kind == Kinds.Picture ? string.Equals(File, o.File, StringComparison.OrdinalIgnoreCase) : Kind == Kinds.Text || Path == o.Path);

        /// <summary>The same thing with the same settings (a picture's numbers and switches; a piece's light and Still).</summary>
        public bool SameSettings(SignRecipe o) =>
            SameItem(o) && (Kind == Kinds.Picture
                ? WidthM == o.WidthM && Pitch == o.Pitch && Cover == o.Cover && Transparent == o.Transparent && Glow == o.Glow && Animate == o.Animate
                : Kind == Kinds.Text || (Light == o.Light && Still == o.Still));

        public SignRecipe Copy() => Parse(ToString());
    }
}
