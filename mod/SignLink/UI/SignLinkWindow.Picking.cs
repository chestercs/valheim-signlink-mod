using System;
using System.Collections.Generic;
using BepInEx.Configuration;
using SignLink.Pictures;
using SignLink.Signs;
using UnityEngine;
using PetyusPolisz.Kit;

namespace SignLink.UI
{
    /// <summary>Pick in view: the window hides, the sign under the cursor wears a frame (white board), a click selects it.</summary>
    internal partial class SignLinkWindow
    {
        void StartPicking()
        {
            Picking = true;
            pickingAt = Time.unscaledTime;
            showNearby = false;
            LookAgain();
            Widgets.DropFocus();
        }

        // what the hover frame was last worked out for (0.20.132, review: two raycasts and two or three passes over every
        // loaded sign each frame while picking, the mouse and the camera still): the mouse and the camera's pose for
        // HoverAt, the stack's first sign for ShowHover, and when; looked at again at least every LookEvery seconds (a sign
        // that appeared or changed under a still cursor), and at once when the window's state changes (LookAgain)
        Vector3 hoverMouse, hoverCamPos;
        Quaternion hoverCamRot;
        float hoverLookAt = -100f, hoverForAt = -100f;
        Sign hoverFor, hoverFirst, hoverHost;
        List<Sign> hoverStack;
        const float LookEvery = 0.5f;

        /// <summary>The hover frame is worked out again at the next look (another sign selected, picking started, the
        /// frame hidden).</summary>
        void LookAgain()
        {
            hoverLookAt = hoverForAt = -100f;
            hoverFor = null;
        }

        /// <summary>While picking, every frame: the sign the click would select gets the hover frame (its content and
        /// its board, in cool white), so the target shows before the click; nothing under the cursor hides it. With the
        /// mouse and the camera still, the last look stands for LookEvery seconds.</summary>
        void HoverAt(Vector3 screen)
        {
            Camera cam = Camera.main;
            float now = Time.unscaledTime;
            Vector3 camPos = cam ? cam.transform.position : Vector3.zero;
            Quaternion camRot = cam ? cam.transform.rotation : Quaternion.identity;
            if (now - hoverLookAt < LookEvery && screen.Equals(hoverMouse) && camPos.Equals(hoverCamPos) && camRot.Equals(hoverCamRot)) return;
            hoverLookAt = now;
            hoverMouse = screen;
            hoverCamPos = camPos;
            hoverCamRot = camRot;
            Sign root = null;
            List<Sign> stack = null;
            if (cam)
            {
                Ray ray = cam.ScreenPointToRay(screen);
                root = AlongRay(ray.origin, ray.direction, out stack);
            }
            if (!root)
            {
                if (hovering) hover.Hide();
                hovering = false;
                hoverFor = null;
                return;
            }
            ShowHover(root, stack);
        }

        /// <summary>The hover frame on this sign's stack (its first sign; the stack when the caller found it already); none
        /// on the selected stack while it wears the selection's frame (0.20.92, the author: the already selected sign has a
        /// layer ... there are 2 on the sign). The same first sign within LookEvery seconds keeps the frame as it is.</summary>
        void ShowHover(Sign root, List<Sign> stack = null)
        {
            float now = Time.unscaledTime;
            if (ReferenceEquals(root, hoverFor) && now - hoverForAt < LookEvery)
            {
                // the stack as found: the frame still follows the camera (its side; ShowStack is cheap when nothing changed)
                if (hovering && hoverFirst)
                {
                    try { hover.ShowStack(hoverFirst, hoverHost, hoverStack); }
                    catch (Exception e) { Failed(e); }
                }
                return;
            }
            hoverFor = root;
            hoverForAt = now;
            if (stack == null) stack = SignFinder.Stack(root);
            Sign first = stack.Count > 0 ? stack[0] : root;
            if (ghost.Frames(first))
            {
                if (hovering) hover.Hide();
                hovering = false;
                return;
            }
            Sign host = stack.Count > 0 ? stack[stack.Count - 1] : root;
            hoverFirst = first;
            hoverHost = host;
            hoverStack = stack.Count > 0 ? stack : new List<Sign> { root };
            try { hover.ShowStack(first, host, hoverStack); }
            catch (Exception e) { Failed(e); }
            hovering = true;
        }

        /// <summary>The click while picking: the sign under the cursor by a ray from the camera (its board, or where a
        /// lying sign stood); the window shows again either way.</summary>
        void PickAt(Vector3 screen)
        {
            Picking = false;
            Camera cam = Camera.main;
            Sign s = null;
            if (cam)
            {
                Ray ray = cam.ScreenPointToRay(screen);
                s = AlongRay(ray.origin, ray.direction, out _);
            }
            if (s)
            {
                KeepOpen();                                                 // the player's own pick: no put closes it now
                SelectSign(s);
                Say($"selected {Called(s)}.");
            }
            else
            {
                Session.Touch();
                Say("error: no sign there. Click a sign's board, or where a lying sign stood.");
            }
        }

        /// <summary>A click in the world while the window is open: the sign under it is selected, as Pick in view selects it;
        /// off any sign nothing happens (no message: a click in the world is not always meant for a sign).</summary>
        void ClickAt(Vector3 screen)
        {
            Camera cam = Camera.main;
            if (!cam) return;
            Ray ray = cam.ScreenPointToRay(screen);
            Sign s = AlongRay(ray.origin, ray.direction, out _);
            if (!s || Target && ReferenceEquals(SignFinder.Root(s), SignFinder.Root(Target))) return;   // already the selected one
            KeepOpen();
            SelectSign(s);
            Say($"selected {Called(s)}.");
        }

        /// <summary>While picking: one line in the upper middle of the screen, the window hidden.</summary>
        void PickHint()
        {
            const string text = "Click a sign  ·  Esc cancels";
            Vector2 size = Theme.Header.CalcSize(new GUIContent(text));
            float pad = Widgets.U(24);
            var box = new Rect((Screen.width - size.x - 2 * pad) / 2f, Screen.height * 0.12f, size.x + 2 * pad, size.y + pad);
            GUI.Box(box, GUIContent.none, Theme.Section);
            GUI.Label(new Rect(box.x + pad, box.y + pad / 2, size.x, size.y), text, Theme.Header);
        }
    }
}
