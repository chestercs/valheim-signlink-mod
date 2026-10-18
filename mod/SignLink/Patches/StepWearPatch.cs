using System;
using HarmonyLib;
using SignLink.Signs;

namespace SignLink.Patches
{
    /// <summary>The game leaves a sign's wear and support alone while it has steps not applied (3 Place's moves and turns,
    /// SignMove.Paused): they move it in this game only, where the game would weigh it and could break it before the
    /// player applies or undoes them (1.0.2, the author: let the sliders go where it would have no support; only the
    /// apply is refused). Only the game that owns the sign weighs it (WearNTear.UpdateWear, client/WearNTear.cs:529), and
    /// the others see it where it stands for everyone. An error inside switches the patch off (the game's own code runs).</summary>
    [HarmonyPatch(typeof(WearNTear), nameof(WearNTear.UpdateWear))]
    internal static class StepWearPatch
    {
        internal static bool Off;

        static bool Prefix(WearNTear __instance)
        {
            if (Off) return true;
            try { return !SignMove.Paused(__instance); }
            catch (Exception e)
            {
                Off = true;
                Plugin.Log.LogWarning($"StepWearPatch is off after an error: {e.Message}");
                return true;
            }
        }
    }
}
