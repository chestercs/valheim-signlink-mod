using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using SignLink.Pictures;

namespace SignLink.Signs
{
    /// <summary>What Copy puts on the clipboard (0.20.49): every sign's text of the stack, and, for content SignLink made
    /// (a picture, a piece), what made it (its recipe and name) and how it sits (the fit and its middle as made), so
    /// Paste gives the other sign the same content to edit, not only its look (the author: what happens if I copy
    /// a sign and paste it?). The header goes first, between STX and ETX, one key=value a line, values escaped;
    /// the texts follow, joined by GS as before 0.20.49, whose copies still paste (their texts only). A plain text
    /// copies as itself. A moving piece carries its saved frames too (Anim, StoredAnims' blob, base64; 0.20.178, the author:
    /// a moving glyph art copied, cleared and pasted back stood still). Unpack never throws: a header it cannot read is
    /// dropped. No Unity here (mod/SignLink.Tests).</summary>
    internal sealed class SignClip
    {
        public const char SignSep = '\u001d';
        const string Head = "\u0002SignLink clip 1\n";
        const char End = '\u0003';

        public List<string> Texts = new List<string>();
        public string Recipe = "", Name = "";
        public bool HasFit;
        public Fit Fit = Fit.AsMade;
        public double C0x, C0y;
        public bool C0Known;
        /// <summary>A stored animation's blob (AnimBlob), null when the sign holds none.</summary>
        public byte[] Anim;

        public string Pack()
        {
            string texts = string.Join(SignSep.ToString(), Texts.ToArray());
            bool anim = Anim != null && Anim.Length > 0;
            if (Recipe.Length == 0 && Name.Length == 0 && !HasFit && !anim) return texts;
            var sb = new StringBuilder(Head);
            sb.Append("recipe=").Append(Uri.EscapeDataString(Recipe)).Append('\n');
            sb.Append("name=").Append(Uri.EscapeDataString(Name)).Append('\n');
            if (HasFit)
                sb.Append("fit=").Append(string.Join(",", new[]
                {
                    N(Fit.Scale), N(Fit.Right), N(Fit.Up), N(Fit.Out), N(Fit.Turn), N(Fit.Tilt), Fit.Edge ? "1" : "0",
                    N(C0x), N(C0y), C0Known ? "1" : "0",
                })).Append('\n');
            if (anim) sb.Append("anim=").Append(Convert.ToBase64String(Anim)).Append('\n');
            return sb.Append(End).Append(texts).ToString();
        }

        public static SignClip Unpack(string clip)
        {
            var c = new SignClip();
            clip = clip ?? "";
            int end = clip.StartsWith(Head, StringComparison.Ordinal) ? clip.IndexOf(End) : -1;
            if (end >= 0)
            {
                try { ReadHeader(clip.Substring(Head.Length, end - Head.Length), c); }
                catch (Exception)
                {
                    c = new SignClip();                                     // a header it cannot read: the texts alone
                }
                clip = clip.Substring(end + 1);
            }
            c.Texts.AddRange(clip.Split(SignSep));
            return c;
        }

        static void ReadHeader(string header, SignClip c)
        {
            foreach (string line in header.Split('\n'))
            {
                int eq = line.IndexOf('=');
                if (eq <= 0) continue;
                string key = line.Substring(0, eq), value = line.Substring(eq + 1);
                if (key == "recipe") c.Recipe = Uri.UnescapeDataString(value);
                else if (key == "name") c.Name = Uri.UnescapeDataString(value);
                else if (key == "anim") c.Anim = Convert.FromBase64String(value);
                else if (key == "fit")
                {
                    string[] v = value.Split(',');
                    if (v.Length != 10) continue;
                    var f = new Fit { Scale = D(v[0]), Right = D(v[1]), Up = D(v[2]), Out = D(v[3]), Turn = D(v[4]), Tilt = D(v[5]), Edge = v[6] == "1" };
                    double c0x = D(v[7]), c0y = D(v[8]);
                    // as SignFit.Read takes a fit from a sign: one past the bounds is none (0.20.132, review)
                    if (!Fit.Sane(f.Scale, f.Right, f.Up, f.Out) || !Fit.SanePoint(c0x, c0y)) continue;
                    c.Fit = f;
                    c.C0x = c0x;
                    c.C0y = c0y;
                    c.C0Known = v[9] == "1";
                    c.HasFit = true;
                }
            }
        }

        static string N(double v) => v.ToString("R", CultureInfo.InvariantCulture);

        /// <summary>A number of the header; anything else throws (the whole header is dropped).</summary>
        static double D(string s)
        {
            double v = double.Parse(s, NumberStyles.Float, CultureInfo.InvariantCulture);
            if (double.IsNaN(v) || double.IsInfinity(v)) throw new FormatException("not a number");
            return v;
        }
    }
}
