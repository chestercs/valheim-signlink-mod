using HarmonyLib;
using SignLink.Signs;

namespace SignLink.Patches
{
    /// <summary>Every sign registers itself when it awakes, so SignFinder.Loaded() is a list, not a scan of every loaded
    /// object (FindObjectsByType walks all of them: with four callers on their own clocks that was several scans a
    /// second in a big base). Destroyed signs drop out when the list is read.</summary>
    [HarmonyPatch(typeof(Sign), "Awake")]
    internal static class SignRegistryPatch
    {
        static void Postfix(Sign __instance) => SignFinder.Register(__instance);
    }
}
