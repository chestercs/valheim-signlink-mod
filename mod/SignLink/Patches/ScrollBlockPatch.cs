using HarmonyLib;
using SignLink.UI;
using UnityEngine;

namespace SignLink.Patches
{
    /// <summary>The mouse wheel over the SignLink window scrolls its lists, so the game reads no wheel there: no camera
    /// zoom (GameCamera.cs:254), no piece rotation. Away from the window it is the game's again: the camera zooms
    /// (0.20.166, the author: zooming with the wheel outside the addon should work); a slider or field being dragged
    /// keeps it too. While the right button turns the view the wheel is the game's wherever the locked cursor sits
    /// (RightLook: the screen's middle, maybe over the window; 1.1.0, review). TextInput.IsVisible does not cover these
    /// (docs/mod-standards.md, GUI).</summary>
    [HarmonyPatch(typeof(ZInput), nameof(ZInput.GetMouseScrollWheel))]
    internal static class ScrollBlockPatch
    {
        static void Postfix(ref float __result)
        {
            if (SignLinkWindow.IsOpen && !RightLook.Active && (SignLinkWindow.PointerOver || GUIUtility.hotControl != 0)) __result = 0f;
        }
    }
}
