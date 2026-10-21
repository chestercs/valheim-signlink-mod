using UnityEngine;

namespace PetyusPolisz.Kit
{
    /// <summary>A button for an action that cannot be undone (Delete, Clear, Paste over a sign that holds something):
    /// the first click arms it for four seconds and relabels it "Sure? Click again"; the second click within that time
    /// is the action. One button is armed at a time; closing the window disarms it.</summary>
    internal static class Armed
    {
        const float Seconds = 4f;
        static string armed;
        static float at;

        /// <summary>True on the second click. key: what this button does (and to what: the row's path), so another row
        /// or another action starts over.</summary>
        public static bool Button(IDeferred w, string key, string label, string sure, GUIStyle style, params GUILayoutOption[] opts)
        {
            bool isArmed = armed == key && Time.unscaledTime - at < Seconds;
            if (!Widgets.Button(isArmed ? sure : label, isArmed ? Theme.Armed(style) : style, opts)) return false;   // armed: red
            if (isArmed)
            {
                armed = null;
                return true;
            }
            w.Later(() =>
            {
                armed = key;
                at = Time.unscaledTime;
            });
            return false;
        }

        public static void Disarm() => armed = null;
    }
}
