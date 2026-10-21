using System;
using System.Collections.Generic;
using BepInEx.Configuration;
using SignLink.Pictures;
using SignLink.Signs;
using UnityEngine;
using PetyusPolisz.Kit;

namespace SignLink.UI
{
    /// <summary>Outside the window: the button over the game's own sign editor, and the badge in the main menu when the window could not load.</summary>
    internal partial class SignLinkWindow
    {
        /// <summary>A button above the game's own sign editor while it shows for a sign: "Open in SignLink" hides the
        /// editor (nothing written) and opens the window on that sign a frame later (the editor counts as visible until
        /// then). The game's editor cuts a text to 50 characters; SignLink takes any length, with templates and a
        /// preview. Placed over the panel's top edge in screen pixels (its canvas may be an overlay or a camera's).</summary>
        void EditorButton()
        {
            if (Broken != null) return;
            TextInput ti = TextInput.instance;
            if (!ti || !ti.m_panel || !ti.m_panel.activeSelf) return;
            Sign sign = ti.m_queuedSign as Sign;
            if (!sign) return;
            Layout();                                                       // the styles at the window's text scale
            float top = Screen.height * 0.4f, centreX = Screen.width / 2f;
            if (ti.m_panel.transform is RectTransform rt)
            {
                Canvas canvas = ti.m_panel.GetComponentInParent<Canvas>();
                Camera cam = canvas && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
                var corners = new Vector3[4];
                rt.GetWorldCorners(corners);                                 // bottom-left, top-left, top-right, bottom-right
                Vector2 bl = RectTransformUtility.WorldToScreenPoint(cam, corners[0]);
                Vector2 tr = RectTransformUtility.WorldToScreenPoint(cam, corners[2]);
                top = Screen.height - Mathf.Max(bl.y, tr.y);                 // the panel's top edge; IMGUI counts from the top
                centreX = (bl.x + tr.x) / 2f;
            }
            string label = Lang.T("Open in SignLink");
            Vector2 size = Theme.Button.CalcSize(new GUIContent(label));
            float bh = size.y + Widgets.U(8);
            var box = new Rect(centreX - size.x / 2f - Widgets.U(8), Mathf.Max(0f, top - Widgets.U(10) - bh), size.x + Widgets.U(16), bh);   // above the panel
            if (GUI.Button(box, label, Theme.Button))
            {
                ti.m_queuedSign = null;                                     // the editor writes nothing when it goes
                ti.Hide();
                OpenSoonFor(sign);
            }
        }

        /// <summary>Only when the window failed to load: a line in the main menu's bottom right corner that says so (the
        /// version is in the chat: /signlink version).</summary>
        void Badge()
        {
            if (Broken == null || !FejdStartup.instance || Player.m_localPlayer) return;
            if (badge == null)
                badge = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.LowerRight, fontSize = Mathf.Max(12, Screen.height / 75) };
            badge.normal.textColor = new Color(1f, 0.45f, 0.35f);
            GUI.Label(new Rect(0, 0, Screen.width - 12, Screen.height - 8),
                      Lang.F("SignLink {0} could not load: see BepInEx/LogOutput.log", Plugin.Version), badge);
        }
    }
}
