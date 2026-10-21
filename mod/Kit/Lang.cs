using System;
using System.Collections.Generic;

namespace PetyusPolisz.Kit
{
    /// <summary>The window's words in the player's language (1.1.0, the author: get ready for more languages; a Language
    /// setting whose Game default takes the game's language, English where there is no table). English is the key and the
    /// fallback, gettext style, so the code reads as it did: T("Close"), F("Loading {0} ...", name). A table is the lines
    /// "english&lt;TAB&gt;translation" (# comments, \n and \t written so); the mod hands its tables in (Add) and says where the
    /// setting and the game's language come from (Setting, Game). No pure C# here but strings: the tests run it.
    /// Rules for the texts (docs/mod-standards.md): whole sentences with {0} places, never glued pieces.</summary>
    internal static class Lang
    {
        public const string GameDefault = "Game default";                  // lang:keys (the setting list shows it through T)
        public const string English = "English";                           // a language by its own name: not translated

        /// <summary>The setting's value (Game default, English or a table's language); null: Game default.</summary>
        public static Func<string> Setting;

        /// <summary>The game's language by its own name ("English", "Hungarian", ...); null: not known.</summary>
        public static Func<string> Game;

        static readonly SortedDictionary<string, Func<string>> sources = new SortedDictionary<string, Func<string>>(StringComparer.Ordinal);
        static string[] choices;
        static string lastSetting, lastGame, tableFor;
        static Dictionary<string, string> table;

        /// <summary>A language's table, its text read when the language is first used.</summary>
        public static void Add(string language, Func<string> text)
        {
            if (string.IsNullOrEmpty(language) || text == null || language == GameDefault || language == English) return;
            sources[language] = text;
            choices = null;
            names.Remove(language);
            tableFor = null;
        }

        /// <summary>The order the tables' languages come in after Game default and English (the mod's own choice; any other
        /// after them by name): 1.1.0, the author: Game default, English, Hungarian, Chinese, Russian.</summary>
        public static string[] Order = new string[0];

        /// <summary>The setting's choices: Game default first (the default, and what a value not in the list becomes), English,
        /// then every language a table came for, in Order.</summary>
        public static string[] Choices
        {
            get
            {
                if (choices != null) return choices;
                var all = new List<string> { GameDefault, English };
                foreach (string l in Order) if (sources.ContainsKey(l)) all.Add(l);
                foreach (string l in sources.Keys) if (!all.Contains(l)) all.Add(l);
                return choices = all.ToArray();
            }
        }

        /// <summary>The key a table names its language with, in that language ("Magyar", "中文"): what the setting's list
        /// shows. A table without it shows the game's name for the language.</summary>
        public const string NameKey = "@language";

        static readonly Dictionary<string, string> names = new Dictionary<string, string>(StringComparer.Ordinal);

        /// <summary>A choice as the list shows it: Game default in the words in use, English, a table's language by its own
        /// name (NameKey), read once.</summary>
        public static string Name(string choice)
        {
            if (choice == GameDefault) return T(GameDefault);
            if (choice == null || choice == English || !sources.ContainsKey(choice)) return choice;
            if (names.TryGetValue(choice, out string n)) return n;
            n = choice;
            try
            {
                if (Parse(sources[choice]()).TryGetValue(NameKey, out string own)) n = own;
            }
            catch (Exception) { }                                           // an unreadable table: its game name
            return names[choice] = n;
        }

        /// <summary>The language the words are in: the setting's, Game default as the game's; English where no table is.</summary>
        public static string Resolve(string setting, string game)
        {
            string want = string.IsNullOrEmpty(setting) || setting == GameDefault ? game : setting;
            return want != null && sources.ContainsKey(want) ? want : English;
        }

        /// <summary>The language in use now.</summary>
        public static string Current => Resolve(Setting?.Invoke(), Game?.Invoke());

        /// <summary>The words in the language in use; the English as given where its table has none.</summary>
        public static string T(string english)
        {
            if (english == null || sources.Count == 0) return english;      // no table: nothing to look up
            Dictionary<string, string> t = Table();
            return t != null && t.TryGetValue(english, out string said) ? said : english;
        }

        /// <summary>A word that means two things in English: its table key is "context|english" (gettext's context), so a
        /// language can say each otherwise ("Frames" of an animation and the box-drawing "Frames" of the palette); the
        /// English as given where the table has none.</summary>
        public static string C(string context, string english)
        {
            string key = context + "|" + english, said = T(key);
            return said == key ? english : said;
        }

        /// <summary>What a list's items are joined with (", "; a full-width comma in Chinese).</summary>
        public static string Comma => Lang.C("list", ", ");                 // Lang.: the key finder reads it

        /// <summary>T, then string.Format with the values: F("{0} signs", n). A translation whose braces do not fit (a
        /// literal "{", a {1} for one value) falls back to the English: it threw in OnGUI on every event otherwise.</summary>
        public static string F(string englishFormat, params object[] args)
        {
            string said = T(englishFormat);
            try { return string.Format(said, args); }
            catch (FormatException) when (!ReferenceEquals(said, englishFormat)) { return string.Format(englishFormat, args); }
        }

        /// <summary>The table for the language in use, made again when the setting or the game's language changes (the
        /// providers hand back the same string while nothing changed: a reference test first).</summary>
        static Dictionary<string, string> Table()
        {
            string setting = Setting?.Invoke(), game = Game?.Invoke();
            if (ReferenceEquals(setting, lastSetting) && ReferenceEquals(game, lastGame) && tableFor != null) return table;
            lastSetting = setting;
            lastGame = game;
            string lang = Resolve(setting, game);
            if (lang == tableFor) return table;
            tableFor = lang;
            table = null;
            if (sources.TryGetValue(lang, out Func<string> text))
            {
                try { table = Parse(text()); }
                catch (Exception) { table = null; }                         // an unreadable table: English
            }
            return table;
        }

        /// <summary>The lines "english&lt;TAB&gt;translation"; blank lines and # comments skipped, \n and \t unescaped, an
        /// empty translation left out (English shows).</summary>
        public static Dictionary<string, string> Parse(string text)
        {
            var got = new Dictionary<string, string>(StringComparer.Ordinal);
            if (string.IsNullOrEmpty(text)) return got;
            foreach (string raw in text.Split('\n'))
            {
                string line = raw.TrimEnd('\r');
                if (line.Length == 0 || line[0] == '#') continue;
                int tab = line.IndexOf('\t');
                if (tab <= 0) continue;
                string key = Unescape(line.Substring(0, tab)), said = Unescape(line.Substring(tab + 1));
                if (said.Length > 0) got[key] = said;
            }
            return got;
        }

        /// <summary>A table's \n, \t and \\ back to a newline, a tab and one backslash, left to right (a key with a backslash of
        /// its own, "\\n" in a message about escapes, stays one).</summary>
        static string Unescape(string s)
        {
            if (s.IndexOf('\\') < 0) return s;
            var sb = new System.Text.StringBuilder(s.Length);
            for (int i = 0; i < s.Length; i++)
            {
                char c = s[i];
                if (c == '\\' && i + 1 < s.Length && (s[i + 1] == 'n' || s[i + 1] == 't' || s[i + 1] == '\\'))
                {
                    char n = s[++i];
                    sb.Append(n == 'n' ? '\n' : n == 't' ? '\t' : '\\');
                }
                else sb.Append(c);
            }
            return sb.ToString();
        }
    }
}
