using System;
using System.Diagnostics;
using HarmonyLib;
using UnityEngine;

namespace SignLink.Patches
{
    /// <summary>Times the game's work on a sign that SignLink cannot see from outside: its hover text (every frame the
    /// crosshair is on it) and its text refresh (every 2 s, Sign.UpdateText). Only while 9 - Developer API / LogSlowSigns
    /// is on: it is a diagnostic, not a warning (a big picture takes seconds to build on every visitor's game by
    /// design). Anything over 30 ms goes to the log at Info with the sign, how much text it holds and whether SignLink's
    /// own hover line handled it (the measurement wraps SignLink's prefix too), at most every 10 s. No object per call
    /// (Stopwatch.GetTimestamp); an error inside switches the reporting off.</summary>
    internal static class SlowSign
    {
        static float lastLog = -100f;
        static bool off;

        /// <summary>The clock at the start, or 0 when the log is off (then nothing is measured).</summary>
        public static long Now() => !off && PluginConfig.LogSlowSigns.Value ? Stopwatch.GetTimestamp() : 0L;

        public static void Report(Sign s, string what, long started)
        {
            if (off || started == 0L) return;
            try
            {
                double ms = (Stopwatch.GetTimestamp() - started) * 1000.0 / Stopwatch.Frequency;
                if (ms < 30 || Time.unscaledTime - lastLog < 10f) return;
                lastLog = Time.unscaledTime;
                Plugin.Log.LogInfo($"slow sign {what}: {ms:0} ms on {Signs.SignFinder.Id(s)} ({Signs.SignText.SavedText(s).Length} characters, " +
                                   $"SignLink's hover line {(PictureSign.Hovers(s) ? "on" : "off")})");
            }
            catch (Exception e)
            {
                off = true;
                Plugin.Log.LogWarning($"SlowSignPatch is off after an error: {e.Message}");
            }
        }
    }

    [HarmonyPatch(typeof(Sign), nameof(Sign.GetHoverText))]
    internal static class SlowHoverPatch
    {
        [HarmonyPriority(Priority.First)]
        static void Prefix(out long __state) => __state = SlowSign.Now();

        [HarmonyPriority(Priority.Last)]
        static void Postfix(Sign __instance, long __state) => SlowSign.Report(__instance, "hover text", __state);
    }

    [HarmonyPatch(typeof(Sign), "UpdateText")]
    internal static class SlowUpdatePatch
    {
        static void Prefix(out long __state) => __state = SlowSign.Now();

        static void Postfix(Sign __instance, long __state) => SlowSign.Report(__instance, "text refresh", __state);
    }
}
