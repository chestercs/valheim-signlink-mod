using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace PetyusPolisz.Kit
{
    /// <summary>List rows: a thumbnail with its label, an empty list.</summary>
    internal static partial class Widgets
    {
        /// <summary>A list row with a thumbnail (a 56 x 44 box; empty, with boxText in it, while there is none) and the
        /// label; true when clicked while not selected, or clicked at all with openWhenSelected (a row that opens something,
        /// highlighted because the arrows stand on it, still opens by a click).</summary>
        public static bool PictureItem(bool selected, Texture2D thumb, string label, float width, string boxText = "", bool openWhenSelected = false)
        {
            bool hit;
            using (Horizontal())
            {
                Rect box = GUILayoutUtility.GetRect(U(56), U(44), W(56), H(44));
                if (thumb) GUI.DrawTexture(box, thumb, ScaleMode.ScaleToFit);
                else
                {
                    GUI.Box(box, GUIContent.none, Theme.Section);
                    if (boxText.Length > 0) GUI.Label(box, boxText, Theme.BoxText);
                }
                Space(6);                                                   // air between the picture and the row (1.1.0, the author)
                bool now = GUILayout.Toggle(selected, label, Theme.Item, W(width - 68), H(44));   // a click gives the other value
                hit = now != selected && (openWhenSelected || !selected);
            }
            return hit;
        }

        /// <summary>A list with nothing in it (0.20.71, the author: the "none nearby" texts under Playing bigger and
        /// readable, so one can tell this would be a list if it had items): a box in the list's place, its
        /// words in the middle at the labels' size.</summary>
        public static void EmptyList(string text)
        {
            using (Vertical(Theme.Section))
                GUILayout.Label(text, Theme.ListEmpty);
        }
    }
}
