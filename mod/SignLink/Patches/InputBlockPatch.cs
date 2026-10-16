using HarmonyLib;
using SignLink.UI;

namespace SignLink.Patches
{
    /// <summary>While the SignLink window is open the game behaves as if a text field were open, as Jotunn's and
    /// ComfyMods' windows do: one postfix on TextInput.IsVisible stops movement and look (PlayerController.TakeInput),
    /// attacks and the hotbar (Player.TakeInput), frees the cursor (GameCamera.UpdateMouseCapture) and keeps chat,
    /// Esc's pause menu and the map from opening (Chat.cs:191, Menu.cs:388, Minimap.cs:695).</summary>
    [HarmonyPatch(typeof(TextInput), nameof(TextInput.IsVisible))]
    internal static class InputBlockPatch
    {
        /// <summary>Set for one call by WalkPatch, which asks the game whether it would take input but for the window.</summary>
        internal static bool Bypass;

        static void Postfix(ref bool __result)
        {
            if (SignLinkWindow.IsOpen && !Bypass) __result = true;
        }
    }
}
