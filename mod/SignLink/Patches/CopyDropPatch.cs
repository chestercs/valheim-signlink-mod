using System;
using HarmonyLib;
using SignLink.Signs;

namespace SignLink.Patches
{
    /// <summary>The copies of a stack (SignBuilder.CopyKey) cost nothing to make, so taking one down gives nothing:
    /// vanilla drops a third of the recipe, at least one of each, for any piece no player placed (Piece.DropResources).
    /// The drop is decided by the game that owns the copy's data (the hammer sends RPC_Remove to the owner), so this
    /// holds where that owner runs SignLink; a copy owned by a vanilla player's game still drops its wood.
    /// Only signs: any game can set the keys on another piece's data, which then dropped nothing (0.20.132, review).
    /// An error inside switches the patch off (the game's own code runs) instead of stopping every piece's removal.</summary>
    [HarmonyPatch(typeof(Piece), nameof(Piece.DropResources))]
    internal static class CopyDropPatch
    {
        static bool off;

        static bool Prefix(Piece __instance)
        {
            if (off) return true;
            try
            {
                ZNetView nv = __instance ? __instance.m_nview : null;
                ZDO zdo = nv != null && nv.IsValid() ? nv.GetZDO() : null;
                if (zdo == null || (zdo.GetInt(SignBuilder.CopyKey, 0) == 0 && zdo.GetInt(SignBuilder.FreeKey, 0) == 0)) return true;
                return !__instance.GetComponent<Sign>();                    // a copy's keys count on a sign alone
            }
            catch (Exception e)
            {
                off = true;
                Plugin.Log.LogWarning($"CopyDropPatch is off after an error: {e.Message}");
                return true;
            }
        }
    }
}
