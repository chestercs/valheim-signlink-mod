using System;
using System.IO;
using BepInEx;
using UnityEngine;

namespace SignLink.Pictures
{
    /// <summary>Where the players' pictures live: SignLink/images in the game's own data folder (on Windows
    /// AppData/LocalLow/IronGate/Valheim), next to the worlds and characters. Until 1.0.2 they were in
    /// BepInEx/config/SignLink/images, but a mod manager shares a profile by packing up the config folder, and pictures
    /// there made those profile codes too big (the Thunderstore moderators, 2026-10-18). The old folder moves over once.</summary>
    internal static class PictureFolder
    {
        /// <summary>The images folder; set at the plugin's start (Application.persistentDataPath is main-thread only),
        /// read from any thread after that.</summary>
        public static string Images { get; private set; }

        /// <summary>The folder until 1.0.2.</summary>
        static string Old => Path.Combine(Paths.ConfigPath, Path.Combine("SignLink", "images"));

        /// <summary>At the plugin's start, before anything lists or writes pictures.</summary>
        public static void Find()
        {
            string data = null;
            try { data = Application.persistentDataPath; }
            catch (Exception e) { Plugin.Log.LogWarning($"images: the game's data folder is not known ({e.Message})"); }
            if (string.IsNullOrEmpty(data))
            {
                Images = Old;                                               // nowhere else to go: the old place still works
                return;
            }
            Images = Path.GetFullPath(Path.Combine(data, Path.Combine("SignLink", "images")));   // Unity gives it with '/'
            MoveOld();
        }

        /// <summary>Every file and folder of the old images folder into the new one (copied and deleted, so another drive
        /// works too); a file the new folder already has stays in the old place. The old folder, and BepInEx/config/SignLink
        /// with it, goes when it is empty.</summary>
        static void MoveOld()
        {
            string from = Old;
            if (!Directory.Exists(from)) return;
            int moved = 0, kept = 0;
            try
            {
                foreach (string file in Directory.GetFiles(from, "*", SearchOption.AllDirectories))
                {
                    string to = Path.Combine(Images, file.Substring(from.Length).TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
                    try
                    {
                        if (File.Exists(to))
                        {
                            if (Path.GetFileName(file) == ".signlink-stock") File.Delete(file);   // the new folder's own list wins
                            else kept++;
                            continue;
                        }
                        Directory.CreateDirectory(Path.GetDirectoryName(to));
                        File.Copy(file, to);
                        File.SetAttributes(file, FileAttributes.Normal);
                        File.Delete(file);
                        moved++;
                    }
                    catch (Exception e)
                    {
                        kept++;
                        Plugin.Log.LogWarning($"images: {file} could not be moved to {Images}: {e.Message}");
                    }
                }
                RemoveEmpty(from);
                string parent = Path.GetDirectoryName(from);
                if (Directory.Exists(parent) && Directory.GetFileSystemEntries(parent).Length == 0) Directory.Delete(parent);
            }
            catch (Exception e) { Plugin.Log.LogWarning($"images: the old images folder could not be moved: {e.Message}"); }
            if (moved + kept > 0)
                Plugin.Log.LogInfo($"images: {moved} file(s) moved from {from} to {Images}" +
                                   (kept > 0 ? $", {kept} left there (the new folder has a file of that name, or it could not be moved)" : ""));
        }

        static void RemoveEmpty(string dir)
        {
            foreach (string sub in Directory.GetDirectories(dir)) RemoveEmpty(sub);
            if (Directory.GetFileSystemEntries(dir).Length == 0) Directory.Delete(dir);
        }
    }
}
