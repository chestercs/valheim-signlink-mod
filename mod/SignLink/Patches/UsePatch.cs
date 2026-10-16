using HarmonyLib;
using SignLink.UI;
using UnityEngine;

namespace SignLink.Patches
{
    /// <summary>E (Use) works while the SignLink window is open (0.20.167, the author: crouching and using E
    /// are missing): on what the crosshair is on, as Player.Update does (Player.cs:901-915, a press and the held repeat),
    /// when the game itself would take input (Player.TakeInput asked with the window's block lifted) and no SignLink
    /// field has the keyboard. A sign under the crosshair is selected in the window instead of opening the game's own
    /// editor under it (its ward checked, as Sign.Interact does). A chest or a workbench opens nothing while the window
    /// is open (InventoryBlockPatch), as before.</summary>
    [HarmonyPatch(typeof(Player), "Update")]
    internal static class UsePatch
    {
        static void Postfix(Player __instance)
        {
            if (!SignLinkWindow.IsOpen || !ReferenceEquals(__instance, Player.m_localPlayer)) return;
            if (!__instance.m_nview || !__instance.m_nview.IsValid() || !__instance.m_nview.IsOwner()) return;
            if (GUIUtility.keyboardControl != 0 || Hud.InRadial()) return;   // typing in a SignLink field
            bool press = ZInput.GetButtonDown("Use"), held = !press && ZInput.GetButton("Use");
            if (!press && !held) return;
            bool can;
            InputBlockPatch.Bypass = true;
            try { can = __instance.TakeInput(); }
            finally { InputBlockPatch.Bypass = false; }
            if (!can) return;
            GameObject hovering = __instance.m_hovering;
            if (!hovering)
            {
                if (press && __instance.m_doodadController != null) __instance.StopDoodadControl();
                return;
            }
            Sign sign = hovering.GetComponentInParent<Sign>();
            if (sign)
            {
                if (press && SignLinkWindow.Instance && PrivateArea.CheckAccess(sign.transform.position))
                    SignLinkWindow.Instance.OpenFor(sign);                   // selected, as E on a picture sign does
                return;
            }
            bool alt = ZInput.GetButton("AltPlace");
            __instance.Interact(hovering, hold: held, alt);
        }
    }
}
