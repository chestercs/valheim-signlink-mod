using HarmonyLib;
using SignLink.UI;

namespace SignLink.Patches
{
    /// <summary>Tab (or E on a chest) would open the inventory under the SignLink window: InventoryGui.Update checks chat,
    /// console and menu but not TextInput (InventoryGui.cs:526, :573). Showing it waits until the window is closed.</summary>
    [HarmonyPatch(typeof(InventoryGui), nameof(InventoryGui.Show))]
    internal static class InventoryBlockPatch
    {
        static bool Prefix() => !SignLinkWindow.IsOpen;
    }
}
