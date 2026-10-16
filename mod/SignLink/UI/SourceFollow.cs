namespace SignLink.UI
{
    /// <summary>Which source 2 Content's strip shows as the choice slots change (ContentTab.Follow): the player's own
    /// source is kept while their own slot shows, and comes back with it. A sign's slot loads into its source
    /// (ContentTab.LoadInto: Leaving first); a slot with nothing loaded (Session.Blank, 0.20.89) leaves the strip where it
    /// is. The own source was noted only at a load before (0.20.90, review): after a blank slot the strip went back to the
    /// source of an older load, Text at first, instead of the player's own. No Unity here (mod/SignLink.Tests).</summary>
    internal sealed class SourceFollow
    {
        object shownFor;

        /// <summary>The source the player's own slot was last shown on.</summary>
        public int OwnAt { get; private set; }

        /// <summary>A sign's slot is loading into a source: the slot left (editing: a sign's own) gives the own source
        /// when it is the player's own.</summary>
        public void Leaving(bool editing, int at)
        {
            if (!editing) OwnAt = at;
        }

        /// <summary>Once a Layout: the source the strip shows for this slot (chosen: the session's choice slot,
        /// editing: a sign's own, at: the strip's source now).</summary>
        public int Follow(object chosen, bool editing, int at)
        {
            if (ReferenceEquals(chosen, shownFor))
            {
                if (!editing) OwnAt = at;                                   // the own slot: its source as the player has it
                return at;
            }
            shownFor = chosen;
            return editing ? at : OwnAt;                                    // the own slot back: its own source with it
        }
    }
}
