using System;
using HarmonyLib;
using SignLink.UI;
using UnityEngine;
using UnityEngine.InputSystem;

namespace SignLink.Patches
{
    /// <summary>Looking around with the window open (1.1.0, the author: while the right mouse button is held outside the
    /// window, the camera turns): a press of the right button off the window starts it; until the button is let go the
    /// cursor is locked and hidden (the game frees it every frame while the window is open: LookCursorPatch) and the mouse
    /// turns the view as in play (the player's mouse sensitivity and inverted Y, PlayerController.LateUpdate); let go, the
    /// cursor comes back where it was. Not while a SignLink field has the keys, a control is held, nor when the game would
    /// not take input anyway (chat, the console, the menu, the map: PlayerController.TakeInput with the window's block
    /// lifted, as WalkPatch asks it). The button is read every frame, so a release the game missed (Alt+Tab) ends it too.</summary>
    internal static class RightLook
    {
        /// <summary>The right button turns the view now (the cursor locked and hidden).</summary>
        public static bool Active { get; private set; }

        static Vector2 from;                                                // where the cursor was (screen pixels, from the lower left)
        static int warpIn = -1;                                             // frames until it goes back there (once the lock is let go)
        static int settle;                                                  // frames whose move is the lock's, not the mouse's

        /// <summary>Every frame for the local player (PlayerController.LateUpdate): starts, keeps or ends the look; the turn
        /// for this frame (zero: none).</summary>
        public static Vector2 Step(PlayerController pc)
        {
            if (warpIn >= 0 && --warpIn < 0 && SignLinkWindow.IsOpen && Mouse.current != null) Mouse.current.WarpCursorPosition(from);
            if (Active)
            {
                if (Input.GetMouseButton(1) && SignLinkWindow.IsOpen && Wanted(pc)) return Turn();
                End();
                return Vector2.zero;
            }
            // not without the cursor's patch: the game would free and lock the cursor every frame, the view spinning (1.1.0,
            // review: a game update could leave this patch on and that one off)
            if (!SignLinkWindow.IsOpen || !Input.GetMouseButtonDown(1) || SignLinkWindow.PointerOver || GUIUtility.hotControl != 0 ||
                GUIUtility.keyboardControl != 0 || !Plugin.Patched(typeof(LookCursorPatch)) || !Wanted(pc))
                return Vector2.zero;
            Active = true;                                                  // the lock takes hold this frame
            from = Input.mousePosition;
            warpIn = -1;
            settle = 2;                                                     // the lock moves the cursor to the middle: not a turn
            return Vector2.zero;
        }

        /// <summary>Ends the look: the game frees the cursor at its next capture, then it goes back where it was.</summary>
        static void End()
        {
            Active = false;
            warpIn = 2;                                                     // after the game's next UpdateMouseCapture frees it
        }

        /// <summary>The game would turn the view but for the window: it takes input, and none of its own panels (inventory,
        /// map, a trader's store, the piece list; PlayerController.LateUpdate), a radial menu or a popup is up (1.1.0, review: E on a
        /// trader opens the store, and the view spun behind it).</summary>
        static bool Wanted(PlayerController pc) =>
            InputBlockPatch.GameTakes(pc, c => c.TakeInput(look: true)) && !pc.InInventoryEtc() && !Hud.InRadial() && !UnifiedPopup.IsVisible();

        /// <summary>The cursor is to stay locked now: a look the window or the button no longer holds (Step not called, the
        /// player gone) never keeps it.</summary>
        public static bool Holding => Active && SignLinkWindow.IsOpen && Input.GetMouseButton(1) && Player.m_localPlayer;

        /// <summary>Stops a look at once (LookPatch switched off after an error).</summary>
        public static void Stop()
        {
            if (Active) End();
        }

        /// <summary>The mouse's move as the game turns the view with it (PlayerController.LateUpdate).</summary>
        static Vector2 Turn()
        {
            if (settle > 0)
            {
                settle--;
                return Vector2.zero;
            }
            Vector2 d = ZInput.GetMouseDelta() * PlayerController.m_mouseSens;
            if (PlayerController.m_invertMouse) d.y *= -1f;
            return d;
        }
    }

    /// <summary>The turn, after the game set none for the window's block.</summary>
    [HarmonyPatch(typeof(PlayerController), "LateUpdate")]
    internal static class LookPatch
    {
        static bool off;

        static void Postfix(PlayerController __instance)
        {
            if (off) return;
            try
            {
                Player p = __instance.m_character;
                if (!p || !ReferenceEquals(p, Player.m_localPlayer)) return;
                Vector2 turn = RightLook.Step(__instance);
                if (RightLook.Active) p.SetMouseLook(turn);
            }
            catch (Exception e)
            {
                off = true;
                RightLook.Stop();
                Plugin.Log.LogWarning($"LookPatch is off after an error: {e.Message}");
            }
        }
    }

    /// <summary>The cursor locked and hidden while the right button turns the view, and the game's own capture skipped:
    /// it frees the cursor every frame while the window is open, and a lock after a free recentres it each time, a jump
    /// the mouse never made (in the first try the view spun, and a release was missed).</summary>
    [HarmonyPatch(typeof(GameCamera), "UpdateMouseCapture")]
    internal static class LookCursorPatch
    {
        static bool Prefix()
        {
            if (!RightLook.Holding) return true;
            ZCursor.LockState = CursorLockMode.Locked;
            ZCursor.Hide();
            return false;
        }
    }
}
