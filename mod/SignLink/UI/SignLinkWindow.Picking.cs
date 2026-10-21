using System;
using System.Collections.Generic;
using BepInEx.Configuration;
using SignLink.Pictures;
using SignLink.Signs;
using UnityEngine;
using PetyusPolisz.Kit;

namespace SignLink.UI
{
    /// <summary>Select sign: the window hides, the sign under the cursor wears a frame (white board), a click selects it.</summary>
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
        /// <summary>Looks at most this often while the mouse or the camera moves (1.1.0, review: walking or the right button
        /// turning the view looked every frame, every loaded sign each time, in a large base).</summary>
        const float LookMoving = 0.05f;

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
            if (now - hoverLookAt < LookMoving) return;
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
                Ray ray = PointerRay(cam, screen);
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

        /// <summary>The click while picking: the sign under the cursor by a ray from the camera (its picture, its board, or
        /// where a lying sign stood) is selected and the window shows again; a click off any sign keeps picking (1.1.0, the
        /// author: Pick in view should not stop when I miss a sign), until a sign, Esc or the window's key.</summary>
        void PickAt(Vector3 screen)
        {
            Camera cam = Camera.main;
            Sign s = null;
            if (cam)
            {
                Ray ray = PointerRay(cam, screen);
                s = AlongRay(ray.origin, ray.direction, out _);
            }
            if (!s)
            {
                missedAt = Time.unscaledTime;                               // the hint says so for a moment (PickHint)
                return;
            }
            Picking = false;
            KeepOpen();                                                     // the player's own pick: no put closes it now
            SelectSign(s);
            Say(Selected(s));
        }

        /// <summary>The ray from the camera through a point of the screen (Input.mousePosition, screen pixels). The game draws
        /// its world camera into a smaller texture when its render scale is under 100 % (3070 x 1656 on a 3840 x 2071 screen),
        /// and ScreenPointToRay counts the camera's own pixels: a click landed further off the further it was from the
        /// screen's lower left corner (1.1.0, the author: Pick in view hardly took a click). A camera drawing on the screen
        /// itself takes screen pixels as they are (its viewport's place counted by ScreenPointToRay).</summary>
        static Ray PointerRay(Camera cam, Vector3 screen) =>
            cam.targetTexture == null ? cam.ScreenPointToRay(new Vector3(screen.x, screen.y, 0f))
            : cam.ScreenPointToRay(new Vector3(screen.x * cam.pixelWidth / Mathf.Max(1, Screen.width),
                                               screen.y * cam.pixelHeight / Mathf.Max(1, Screen.height), 0f));

        /// <summary>When a click while picking last met no sign (the hint says so for MissShown seconds).</summary>
        float missedAt = -100f;
        const float MissShown = 1.5f;
        string hintKey, hintLang, hintClick, hintMissed;                    // the hint's words, made once per key and language
        readonly GUIContent hintContent = new GUIContent();

        /// <summary>A click in the world while the window is open: the sign under it is selected, as Select sign selects it;
        /// off any sign nothing happens (no message: a click in the world is not always meant for a sign).</summary>
        void ClickAt(Vector3 screen)
        {
            Camera cam = Camera.main;
            if (!cam) return;
            Ray ray = PointerRay(cam, screen);
            Sign s = AlongRay(ray.origin, ray.direction, out _);
            if (!s || Target && ReferenceEquals(SignFinder.Root(s), SignFinder.Root(Target))) return;   // already the selected one
            if (ShowAll.On) SetShowAll(false);                              // as Select sign does (1.1.0, the author: with Show
            KeepOpen();                                                     // all on a click picked nothing)
            SelectSign(s);
            Say(Selected(s));
        }

        /// <summary>While picking: one line in the upper middle of the screen, the window hidden.</summary>
        void PickHint()
        {
            string key = PluginConfig.MenuKeyText();                        // the window's key ends it too (1.1.0)
            string lang = Lang.Current;                                     // a language changed meanwhile: its words (review)
            if (!ReferenceEquals(key, hintKey) || lang != hintLang)
            {
                hintKey = key;
                hintLang = lang;
                // whole sentences for the translations (docs/mod-standards.md)
                hintClick = key.Length > 0 ? Lang.F("Click a sign  ·  Esc / {0} cancels", key)
                                           : Lang.T("Click a sign  ·  Esc cancels");
                hintMissed = key.Length > 0 ? Lang.F("No sign there  ·  Esc / {0} cancels", key)
                                            : Lang.T("No sign there  ·  Esc cancels");
            }
            string text = Time.unscaledTime - missedAt < MissShown ? hintMissed : hintClick;
            hintContent.text = text;                                        // one content, not one per event
            Vector2 size = Theme.Lead.CalcSize(hintContent);
            size.x += Widgets.U(6);                                         // a few pixels to spare: drawn in its exact width it wrapped
            float pad = Widgets.U(24);
            var box = new Rect((Screen.width - size.x - 2 * pad) / 2f, Screen.height * 0.12f, size.x + 2 * pad, size.y + pad);
            GUI.Box(box, GUIContent.none, Theme.Section);
            GUI.Label(new Rect(box.x + pad, box.y + pad / 2, size.x, size.y), text, Theme.Lead);
        }
    }
}
