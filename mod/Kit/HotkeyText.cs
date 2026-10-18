using System;
using System.Collections.Generic;
using System.Text;

namespace PetyusPolisz.Kit
{
    /// <summary>A hotkey as people write it (0.20.82, the author: BepInEx wrote "PageUp + LeftShift"; write these the
    /// other way round, as is usual): the modifiers first, in Ctrl, Alt, Shift order, each once, by their short names,
    /// then the key ("Shift + Page Up"). Works on Unity's KeyCode names as strings: no Unity here (mod/SignLink.Tests).</summary>
    internal static class HotkeyText
    {
        static readonly string[] Order = { "Ctrl", "Alt", "Shift", "Cmd", "Win" };

        /// <summary>The keys of a combination, the modifiers first; empty for no key.</summary>
        public static string[] Parts(string main, IEnumerable<string> modifiers)
        {
            if (string.IsNullOrEmpty(main) || main == "None") return new string[0];
            var mods = new List<string>();
            foreach (string m in modifiers ?? new string[0])
            {
                string n = Name(m);
                if (n.Length > 0 && !mods.Contains(n)) mods.Add(n);
            }
            mods.Sort((a, b) => Rank(a).CompareTo(Rank(b)));
            mods.Add(Name(main));
            return mods.ToArray();
        }

        /// <summary>The combination in one line: "Ctrl + Shift + F5"; "" for no key.</summary>
        public static string Combo(string main, IEnumerable<string> modifiers) => string.Join(" + ", Parts(main, modifiers));

        static int Rank(string name)
        {
            int i = Array.IndexOf(Order, name);
            return i < 0 ? Order.Length : i;
        }

        /// <summary>One key's short name: Shift, Ctrl, Alt; 7 for Alpha7, Num 5 for Keypad5, Enter, Esc, Up; else the
        /// KeyCode's name with spaces between its words ("Page Up", "Scroll Lock").</summary>
        public static string Name(string key)
        {
            if (string.IsNullOrEmpty(key) || key == "None") return "";
            switch (key)
            {
                case "LeftShift": case "RightShift": return "Shift";
                case "LeftControl": case "RightControl": return "Ctrl";
                case "LeftAlt": case "RightAlt": case "AltGr": return "Alt";
                case "LeftCommand": case "RightCommand": case "LeftApple": case "RightApple": return "Cmd";
                case "LeftWindows": case "RightWindows": return "Win";
                case "Return": return "Enter";
                case "KeypadEnter": return "Num Enter";
                case "Escape": return "Esc";
                case "BackQuote": return "`";
                case "UpArrow": return "Up";
                case "DownArrow": return "Down";
                case "LeftArrow": return "Left";
                case "RightArrow": return "Right";
            }
            if (key.StartsWith("Alpha", StringComparison.Ordinal) && key.Length == 6 && char.IsDigit(key[5])) return key.Substring(5);
            if (key.StartsWith("Keypad", StringComparison.Ordinal) && key.Length > 6) return "Num " + Spaced(key.Substring(6));
            return Spaced(key);
        }

        /// <summary>"PageUp" as "Page Up": a space before each capital that follows a small letter.</summary>
        static string Spaced(string s)
        {
            var sb = new StringBuilder(s.Length + 4);
            for (int i = 0; i < s.Length; i++)
            {
                if (i > 0 && char.IsUpper(s[i]) && char.IsLower(s[i - 1])) sb.Append(' ');
                sb.Append(s[i]);
            }
            return sb.ToString();
        }
    }
}
