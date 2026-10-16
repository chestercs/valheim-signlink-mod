using System;
using System.Collections.Generic;
using System.Reflection;
using SignLink.UI;

namespace SignLink
{
    /// <summary>The "signlink" command, in the chat (/signlink) and in the F5 console (signlink): opens the window
    /// without PageUp, tells the version that runs, closes the window. A game command of this kind works in the chat when
    /// it is no cheat (Chat.InputText runs "/..." as a command; Terminal.ConsoleCommand registers it for both).</summary>
    internal static class Commands
    {
        public const string Name = "signlink";

        /// <summary>Registers the command without binding the constructor's full signature at compile time (the game
        /// has added parameters to Terminal.ConsoleCommand before, and a changed signature would throw inside Awake):
        /// the first constructor taking (string, string, ConsoleEvent, ...) is filled by type and default. A failure
        /// costs only the command.</summary>
        public static void Register()
        {
            try
            {
                ConstructorInfo ctor = null;
                foreach (ConstructorInfo c in typeof(Terminal.ConsoleCommand).GetConstructors())
                {
                    ParameterInfo[] p = c.GetParameters();
                    if (p.Length >= 3 && p[0].ParameterType == typeof(string) && p[1].ParameterType == typeof(string) &&
                        p[2].ParameterType == typeof(Terminal.ConsoleEvent))
                    {
                        ctor = c;
                        break;
                    }
                }
                if (ctor == null) throw new MissingMethodException("Terminal.ConsoleCommand(string, string, ConsoleEvent, ...)");
                ParameterInfo[] ps = ctor.GetParameters();
                var args = new object[ps.Length];
                for (int i = 0; i < ps.Length; i++)
                {
                    Type t = ps[i].ParameterType;
                    if (i == 0) args[i] = Name;
                    else if (i == 1) args[i] = "SignLink: opens its window; 'signlink version' tells the version, 'signlink close' closes it";
                    else if (t == typeof(Terminal.ConsoleEvent)) args[i] = new Terminal.ConsoleEvent(Run);
                    else if (t == typeof(Terminal.ConsoleOptionsFetcher))
                        args[i] = new Terminal.ConsoleOptionsFetcher(() => new List<string> { "open", "version", "close", "help" });
                    else args[i] = ps[i].HasDefaultValue ? ps[i].DefaultValue : t.IsValueType ? Activator.CreateInstance(t) : null;
                }
                ctor.Invoke(args);
            }
            catch (Exception e)
            {
                Plugin.Log.LogWarning($"the /{Name} command is unavailable (the game's console changed): {e.Message}");
            }
        }

        static void Run(Terminal.ConsoleEventArgs args)
        {
            Terminal t = args.Context;
            string what = args.Length > 1 ? args[1].ToLowerInvariant() : "open";
            SignLinkWindow w = SignLinkWindow.Instance;
            switch (what)
            {
                case "version":
                    t?.AddString($"{Plugin.Name} {Plugin.Version} is running. {(PluginConfig.MenuKeyText().Length > 0 ? PluginConfig.MenuKeyText() + " or " : "")}/{Name} opens its window." +
                                 (w != null && w.Broken != null ? $" Its window failed to load: {w.Broken}" : ""));
                    break;
                case "close":
                    w?.Close();
                    break;
                case "help":
                    t?.AddString($"/{Name}: open the SignLink window · /{Name} version: the version that runs · /{Name} close: close it");
                    break;
                default:
                    if (w == null || w.Broken != null)
                    {
                        t?.AddString($"{Plugin.Name} {Plugin.Version}: its window failed to load ({w?.Broken ?? "no window"}); " +
                                     "BepInEx/LogOutput.log has the details.");
                        break;
                    }
                    w.OpenSoon();
                    t?.AddString($"{Plugin.Name} {Plugin.Version}: the window opens as soon as the chat or console closes.");
                    break;
            }
        }
    }
}
