using System.Collections.Generic;

namespace PetyusPolisz.Kit
{
    /// <summary>The Text editor's Undo and Redo (0.20.62, the author: the text editor has no undo, no Ctrl+Z / Ctrl+Y
    /// and no buttons for them): the text is seen once a frame; a burst of typing (changes under Group seconds apart)
    /// is one step, a pause starts the next, and a change made at once (a template, the light, Load, Clear) is a step
    /// of its own. A new change drops what Redo had; at most Most steps back. No Unity here (mod/SignLink.Tests).</summary>
    internal sealed class TextHistory
    {
        public const int Most = 100;
        public const double Group = 1.0;

        readonly List<string> undo = new List<string>(), redo = new List<string>();
        string current;
        double lastAt = double.NegativeInfinity;
        bool typing;

        /// <summary>The text as the history has it now.</summary>
        public string Current => current ?? "";
        public bool CanUndo => undo.Count > 0;
        public bool CanRedo => redo.Count > 0;

        /// <summary>The editor's text at this moment (seconds); discrete: changed at once, not typed.</summary>
        public void Seen(string text, double now, bool discrete = false)
        {
            text = text ?? "";
            if (current == null)
            {
                current = text;
                return;
            }
            if (text == current) return;
            if (discrete || !typing || now - lastAt > Group)
            {
                undo.Add(current);                                          // the text before this step
                if (undo.Count > Most) undo.RemoveAt(0);
            }
            current = text;                                                 // a typing step grows until a pause
            redo.Clear();
            lastAt = now;
            typing = !discrete;
        }

        /// <summary>One step back: the text before it (the current one when there is none).</summary>
        public string Undo()
        {
            if (!CanUndo) return Current;
            redo.Add(Current);
            current = undo[undo.Count - 1];
            undo.RemoveAt(undo.Count - 1);
            typing = false;                                                 // the next keystroke starts a step
            return current;
        }

        /// <summary>One step forward again.</summary>
        public string Redo()
        {
            if (!CanRedo) return Current;
            undo.Add(Current);
            current = redo[redo.Count - 1];
            redo.RemoveAt(redo.Count - 1);
            typing = false;
            return current;
        }

        /// <summary>Another sign's text (or the player's own back): nothing to go back to.</summary>
        public void Reset(string text)
        {
            undo.Clear();
            redo.Clear();
            current = text ?? "";
            typing = false;
        }
    }
}
