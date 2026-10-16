using HarmonyLib;
using SignLink.UI;
using UnityEngine;

namespace SignLink.Patches
{
    /// <summary>The character walks while the SignLink window is open (0.20.166, the author: WASD should work too): WASD,
    /// Run (Shift, or its toggle), Jump, Crouch (0.20.167) and AutoRun, as the game reads them (PlayerController.FixedUpdate);
    /// E is UsePatch's.
    /// InputBlockPatch keeps the game's input off while the window is open (TextInput.IsVisible), so FixedUpdate has just
    /// set no controls; this sets the walk after it when the game itself would take input (PlayerController.TakeInput
    /// asked with the window's block lifted: chat, the console, the menu, the game's own text input and map search still
    /// stop it) and no SignLink field has the keyboard (typed letters stay in the field). Attacks, blocking, the hotbar,
    /// dodge and the mouse look stay off: a click on the window must not swing a weapon, and the cursor is the
    /// window's. Keyboard only, as SignLink is.</summary>
    [HarmonyPatch(typeof(PlayerController), "FixedUpdate")]
    internal static class WalkPatch
    {
        static bool lastJump, lastRun, toggledRun, lastCrouch;

        static void Postfix(PlayerController __instance)
        {
            if (!SignLinkWindow.IsOpen)
            {
                lastJump = lastRun = toggledRun = lastCrouch = false;
                return;
            }
            Player p = __instance.m_character;
            if (!p || __instance.m_nview && !__instance.m_nview.IsOwner()) return;
            if (GUIUtility.keyboardControl != 0) return;                     // typing in a SignLink field
            bool can;
            InputBlockPatch.Bypass = true;
            try { can = __instance.TakeInput(); }
            finally { InputBlockPatch.Bypass = false; }
            if (!can) return;

            Vector3 move = Vector3.zero;
            if (ZInput.GetButton("Forward")) move.z += 1f;
            if (ZInput.GetButton("Backward")) move.z -= 1f;
            if (ZInput.GetButton("Left")) move.x -= 1f;
            if (ZInput.GetButton("Right")) move.x += 1f;
            if (move.magnitude > 1f) move.Normalize();
            bool jumpHeld = ZInput.GetButton("Jump"), jump = jumpHeld && !lastJump;
            lastJump = jumpHeld;
            bool runHeld = ZInput.GetButton("Run");
            if (ZInput.ToggleRun)
            {
                if (runHeld && !lastRun) toggledRun = !toggledRun;
            }
            else toggledRun = false;
            lastRun = runHeld;
            bool run = (ZInput.ToggleRun ? toggledRun : runHeld) && p.GetStamina() > 0f;
            bool autoRun = ZInput.GetButton("AutoRun");
            bool crouchHeld = ZInput.GetButton("Crouch"), crouch = crouchHeld && !lastCrouch;   // a press toggles it, as the game's
            lastCrouch = crouchHeld;
            if (move.sqrMagnitude < 1e-4f && !jump && !autoRun && !crouch) return;   // nothing asked: the game's stop stands
            p.SetControls(move, attack: false, attackHold: false, secondaryAttack: false, secondaryAttackHold: false, block: false,
                          blockHold: false, jump: jump, crouch: crouch, run: run, autoRun: autoRun);
        }
    }
}
