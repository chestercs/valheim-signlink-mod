using HarmonyLib;
using SignLink.UI;
using UnityEngine;

namespace SignLink.Patches
{
    /// <summary>The mouse wheel over the SignLink window scrolls its lists, so the game reads no wheel there: no camera
    /// zoom (GameCamera.cs:254), no piece rotation. Away from the window it is the game's again: the camera zooms
    /// (0.20.166, the author: zooming with the wheel outside the addon should work); a slider or field being dragged
    /// keeps it too. TextInput.IsVisible does not cover these (docs/mod-standards.md, GUI).</summary>
    [HarmonyPatch(typeof(ZInput), nameof(ZInput.GetMouseScrollWheel))]
    internal static class ScrollBlockPatch
    {
        static void Postfix(ref float __result)
        {
            if (SignLinkWindow.IsOpen && (SignLinkWindow.PointerOver || GUIUtility.hotControl != 0)) __result = 0f;
        }
    }
}
