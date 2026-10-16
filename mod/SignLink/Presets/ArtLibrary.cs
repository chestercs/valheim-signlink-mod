using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;

namespace SignLink.Presets
{
    /// <summary>The art that comes with SignLink, embedded in the DLL (mod/SignLink/Art, made by signart/presets.py from
    /// the concept rounds and the art library's lib modules): glyph art (Unicode pieces, each with a rendered picture) and
    /// pixel art (pixel pictures and animations), in groups (Art/groups.tsv: the classics first, then the library's themes).
    /// Nothing to copy: every player has it.</summary>
    internal static class ArtLibrary
    {
        public const string Classics = "Classics";
        const string Prefix = "SignLink.Art.", GroupsFile = "SignLink.Art.groups.tsv";

        static List<Preset> items;

        /// <summary>By group (Art/groups.tsv's order), A-Z inside each.</summary>
        public static List<Preset> Items => items ?? (items = Load());

        static List<Preset> Load()
        {
            var list = new List<Preset>();
            Assembly asm = typeof(ArtLibrary).Assembly;
            var names = new HashSet<string>(asm.GetManifestResourceNames());
            var order = new List<string> { Classics };
            var groupOf = Groups(order);
            foreach (string res in names)
            {
                if (!res.StartsWith(Prefix, StringComparison.Ordinal)) continue;
                string rest = res.Substring(Prefix.Length).Replace('\\', '/');
                int slash = rest.IndexOf('/');
                if (slash < 0) continue;
                string folder = rest.Substring(0, slash), file = rest.Substring(slash + 1);
                if (!Preset.FromFileName(file, out string name, out Preset.Kinds kind)) continue;
                string picture = names.Contains(Prefix + rest.Substring(0, slash + 1) + name + ".png")
                    ? Prefix + rest.Substring(0, slash + 1) + name + ".png" : null;
                string fit = names.Contains(Prefix + rest.Substring(0, slash + 1) + name + ".fit")
                    ? Prefix + rest.Substring(0, slash + 1) + name + ".fit" : null;
                string resource = res;
                long bytes;
                using (Stream s = asm.GetManifestResourceStream(res)) bytes = s?.Length ?? 0;
                list.Add(new Preset
                {
                    Name = name, Kind = kind, Bytes = bytes, Path = res,
                    Category = groupOf.TryGetValue(folder + "/" + name, out string g) ? g : Classics,
                    Pixel = folder == "pixel",
                    ReadText = () => ReadString(resource),
                    ReadPicture = picture == null ? (Func<byte[]>)null : () => ReadBytes(picture),
                    ReadFit = fit == null ? (Func<string>)null : () => ReadString(fit),
                });
            }
            list.Sort((a, b) => a.Category != b.Category ? Rank(order, a.Category).CompareTo(Rank(order, b.Category))
                                                         : string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase));
            return list;
        }

        /// <summary>Art/groups.tsv: "#group NAME" lines give the groups' order (into order), "folder/name TAB group" lines
        /// a piece's group. Empty when the file is missing: every piece is a classic.</summary>
        static Dictionary<string, string> Groups(List<string> order)
        {
            var map = new Dictionary<string, string>(StringComparer.Ordinal);
            if (Array.IndexOf(typeof(ArtLibrary).Assembly.GetManifestResourceNames(), GroupsFile) < 0) return map;
            foreach (string raw in ReadString(GroupsFile).Split('\n'))
            {
                string line = raw.TrimEnd('\r');
                int tab = line.IndexOf('\t');
                if (tab < 0) continue;
                string key = line.Substring(0, tab), group = line.Substring(tab + 1);
                if (key == "#group") { if (!order.Contains(group)) order.Add(group); }
                else if (!key.StartsWith("#", StringComparison.Ordinal)) map[key] = group;
            }
            return map;
        }

        static int Rank(List<string> order, string group)
        {
            int i = order.IndexOf(group);
            return i < 0 ? order.Count : i;
        }

        static byte[] ReadBytes(string resource)
        {
            using (Stream s = typeof(ArtLibrary).Assembly.GetManifestResourceStream(resource))
            using (var m = new MemoryStream())
            {
                s.CopyTo(m);
                return m.ToArray();
            }
        }

        static string ReadString(string resource) => new UTF8Encoding(false).GetString(ReadBytes(resource));
    }
}
