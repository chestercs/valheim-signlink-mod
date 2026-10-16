using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;

namespace SignLink.Pictures
{
    /// <summary>The paintings that come with SignLink (mod/SignLink/Stock, public domain, embedded in the DLL): each is put
    /// into the images folder once, so it works like any picture a player adds (Resolution, Max, the recipe). A list in the
    /// folder (.signlink-stock) remembers what was put there: a picture the player deletes stays deleted, and a painting
    /// a later version adds still arrives.</summary>
    internal static class StockPictures
    {
        const string Prefix = "SignLink.Stock.", ListFile = ".signlink-stock";
        static bool done;

        /// <summary>Once a session, before the images folder is first listed.</summary>
        public static void Place(string folder)
        {
            if (done) return;
            done = true;
            try
            {
                string list = Path.Combine(folder, ListFile);
                var placed = new HashSet<string>(File.Exists(list) ? File.ReadAllLines(list) : new string[0], StringComparer.OrdinalIgnoreCase);
                Assembly asm = typeof(StockPictures).Assembly;
                var added = new List<string>();
                foreach (string res in asm.GetManifestResourceNames())
                {
                    if (!res.StartsWith(Prefix, StringComparison.Ordinal)) continue;
                    string name = res.Substring(Prefix.Length);
                    if (placed.Contains(name)) continue;
                    string path = Path.Combine(folder, name);
                    if (!File.Exists(path))
                        using (Stream s = asm.GetManifestResourceStream(res))
                        using (FileStream f = File.Create(path))
                            s.CopyTo(f);
                    added.Add(name);
                }
                if (added.Count == 0) return;
                File.AppendAllLines(list, added);
                File.SetAttributes(list, File.GetAttributes(list) | FileAttributes.Hidden);
                Plugin.Log.LogInfo($"images: {added.Count} stock painting(s) put into the images folder ({string.Join(", ", added.ToArray())})");
            }
            catch (Exception e)
            {
                Plugin.Log.LogWarning($"images: the stock paintings could not be put into the images folder: {e.Message}");
            }
        }
    }
}
