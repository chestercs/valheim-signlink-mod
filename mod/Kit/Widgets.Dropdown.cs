using System.Collections.Generic;
using UnityEngine;

namespace PetyusPolisz.Kit
{
    /// <summary>A drop-down list (1.1.0, the author: Theme and Language as drop-down lists; there was no such part yet): a
    /// button with the choice and a down arrow; a click opens its list under it, over the window; a click on a row picks
    /// it, a click anywhere else, the mouse wheel or the button again closes it. The list is drawn last in the window
    /// function (KitWindow.EndDraw: DropList), so it lies over everything, and takes the clicks first (KitWindow.BeginDraw:
    /// DropInput), before the controls under it see them. One list is open at a time.</summary>
    internal static partial class Widgets
    {
        static string dropOpen;                                             // the open list's key, or null
        static string[] dropNames;
        static int dropSelected, dropSeen;
        static Vector2 dropAt;                                              // the button's lower left, in screen space (scroll views moved)
        static float dropWidth, dropButtonH;
        static Rect dropRect;                                               // the list, in the window's own space (last laid out)
        static readonly Dictionary<string, int> dropPicked = new Dictionary<string, int>();
        static readonly GUIContent dropLabel = new GUIContent();         // a row's words, one content for all

        /// <summary>A settings row: the label, then the drop-down at most `most` wide (the rest of the row free).</summary>
        public static int DropChoice(string rowLabel, string key, int selected, string[] names, float most = 0f)
        {
            using (Horizontal())
            {
                GUILayout.Label(rowLabel, Theme.Label, W(LabelW));
                int pick = Dropdown(key, selected, names, W(most > 0f ? Mathf.Min(most, RestW) : RestW));
                GUILayout.FlexibleSpace();
                return pick;
            }
        }

        /// <summary>The drop-down's button; returns the row picked from its list (in the event of the click), else selected.</summary>
        public static int Dropdown(string key, int selected, string[] names, params GUILayoutOption[] options)
        {
            int result = selected;                                          // the button drawn in the pick's event too: the
            if (dropPicked.TryGetValue(key, out int picked))                // layout saw it (1.1.0, review)
            {
                dropPicked.Remove(key);
                if (picked >= 0 && picked < names.Length) result = picked;
            }
            string shown = selected >= 0 && selected < names.Length ? names[selected] : "";
            Texture2D arrow = Theme.FoldArrow;
            bool hit = Button(arrow == null ? shown + "  ▾" : shown, Theme.Drop, options);
            Rect r = GUILayoutUtility.GetLastRect();
            if (Event.current.type == EventType.Repaint)
            {
                if (arrow != null)                                          // pointing down, in the room at the right
                {
                    bool over = GUI.enabled && r.Contains(Event.current.mousePosition);
                    float room = Theme.Drop.padding.right;
                    DrawArrow(arrow, r.xMax - room + (room - arrow.width) / 2f - U(2), r.y + (r.height - arrow.height) / 2f, 90f,
                              over ? Theme.Drop.hover.textColor : Theme.Drop.normal.textColor);
                }
                if (dropOpen == key) Place(r);
            }
            if (dropOpen == key) dropSeen = Time.frameCount;
            if (hit)
            {
                dropOpen = key;
                dropNames = names;
                dropSelected = selected;
                dropSeen = Time.frameCount;
                Place(r);
            }
            return result;
        }

        static void Place(Rect button)
        {
            dropAt = GUIUtility.GUIToScreenPoint(new Vector2(button.x, button.yMax));
            dropWidth = button.width;
            dropButtonH = button.height;
        }

        static float DropRow => U(32);

        /// <summary>First in the window function: a click in the open list picks its row, a click elsewhere (or the wheel)
        /// closes it; either way the controls under it do not get it.</summary>
        public static void DropInput()
        {
            if (dropOpen == null) return;
            Event e = Event.current;
            if (e.type == EventType.ScrollWheel)
            {
                dropOpen = null;                                            // the wheel scrolls on; the list does not follow
                return;
            }
            if (e.type != EventType.MouseDown) return;
            if (dropRect.Contains(e.mousePosition))
            {
                int row = Mathf.FloorToInt((e.mousePosition.y - dropRect.y - Theme.DropList.padding.top) / DropRow);
                if (row >= 0 && row < dropNames.Length) dropPicked[dropOpen] = row;
            }
            dropOpen = null;
            e.Use();
        }

        /// <summary>Last in the window function: the open list over everything, under its button, or over it when the
        /// window's bottom (windowHeight) would cut it and there is room above (1.1.0, review: a short window hid the last rows).
        /// Closes when its button was not drawn (another tab, a closed group).</summary>
        public static void DropList(float windowHeight)
        {
            if (dropOpen == null) return;
            if (Time.frameCount - dropSeen > 1)
            {
                dropOpen = null;
                return;
            }
            GUIStyle box = Theme.DropList;
            float h = DropRow * dropNames.Length + box.padding.vertical;
            Vector2 at = GUIUtility.ScreenToGUIPoint(dropAt);
            float y = at.y + U(2);
            if (y + h > windowHeight && at.y - dropButtonH - U(2) - h >= 0f) y = at.y - dropButtonH - U(2) - h;
            dropRect = new Rect(at.x, y, dropWidth, h);
            if (Event.current.type != EventType.Repaint) return;
            Color was = GUI.color;
            GUI.color = Theme.PopupBack;                                    // solid under the list: the window's opacity would show
            GUI.DrawTexture(dropRect, Texture2D.whiteTexture);              // what lies under it through it
            GUI.color = was;
            box.Draw(dropRect, false, false, false, false);
            Vector2 mouse = Event.current.mousePosition;
            for (int i = 0; i < dropNames.Length; i++)
            {
                var row = new Rect(dropRect.x + box.padding.left, dropRect.y + box.padding.top + i * DropRow,
                                   dropRect.width - box.padding.horizontal, DropRow);
                dropLabel.text = dropNames[i];
                Theme.Item.Draw(row, dropLabel, row.Contains(mouse), false, i == dropSelected, false);
            }
        }

        /// <summary>The list closes (the window closed).</summary>
        public static void DropClose() => dropOpen = null;
    }
}
