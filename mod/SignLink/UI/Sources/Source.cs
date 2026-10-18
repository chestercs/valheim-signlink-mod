using SignLink.Pictures;
using SignLink.Signs;
using UnityEngine;

namespace SignLink.UI.Sources
{
    /// <summary>A kind of content the player can put on a sign (2 Content): a list to pick from, the picked item's size
    /// and cost, and the work the footer's button does. One instance each, owned by the Content tab; the Place tab asks
    /// the current one for the rows it shows. A source writes what it picked into Session.Chosen (its recipe, its light)
    /// in Layout events only, and never touches the sign except in Act.</summary>
    internal abstract class Source
    {
        public abstract string Title { get; }                 // "Art", "Text", "Picture"
        public abstract SignRecipe.Kinds Kind { get; }

        /// <summary>The window opened: reload lists.</summary>
        public virtual void OnOpen() { }

        /// <summary>The up and down arrows step the pick to the list's previous or next item (0.20.169, the author: let the
        /// list pickers step with the arrow keys too); false: this source has no list.</summary>
        public virtual bool Step(Session s, int dir) => false;

        /// <summary>Left and right arrows close and open the group of the pick (0.20.170, the author: the left and right
        /// arrows open and close the group); false: this source's list has no groups.</summary>
        public virtual bool Fold(Session s, bool open) => false;

        /// <summary>The window closed, or another source was picked: drop buffers, disarm questions.</summary>
        public virtual void OnClose() { }

        /// <summary>Another source was picked (by the player, or a selected sign's own): OnClose unless the source keeps
        /// work that may go on (Picture: a placement running).</summary>
        public virtual void OnLeave() => OnClose();

        /// <summary>The size the picked item really goes on at (the frame in the world shows it): the chosen scale, or
        /// what the put snaps it to.</summary>
        public virtual double ShownScale(Session s) => s.Chosen.Fit.Scale;

        /// <summary>Once per Layout event, from whichever tab shows this source's rows or its button (the lists need not
        /// draw): the keys, the background work and the choice's recipe.</summary>
        public virtual void Tick(Session s, float now) { }

        /// <summary>The list, the quick view, the settings; writes s.Chosen.Recipe/Light (Layout only).</summary>
        public abstract void Draw(Session s, SignLinkWindow w, float now);

        /// <summary>Pick the item r names and put its settings into `into`; false: the item is not here (its file, its
        /// piece). Called from Session when a sign holding something this source made is selected.</summary>
        public abstract bool Load(SignRecipe r, Choice into, Session s);

        /// <summary>The picked item's size rows, drawn on 3 Place under Size (0.20.59, the author: the size in Content ...
        /// goes to 3 Place ... at the very top): a piece's or a text's scale, a picture's width in metres.</summary>
        public virtual void DrawSize(Session s, SignLinkWindow w) { }

        /// <summary>Something of the source's own differs from what the sign holds (another item, a picture's width or
        /// resolution, a typed text): Undo changes takes it back (Revert) along with the fit and the light.</summary>
        public virtual bool Reverts(Session s) => false;

        /// <summary>Back to what the sign holds, as far as this source knows it (Layout, through Later).</summary>
        public virtual void Revert(Session s) { }

        /// <summary>The pick's size differs from the one recipe r gives the same item, and SizeAs takes it back (3 Place's
        /// content Undo, 0.20.179): only a picture keeps its size in its recipe (its metres); the others' size is the
        /// fit's scale.</summary>
        public virtual bool SizeDiffers(Session s, SignRecipe r) => false;

        public virtual void SizeAs(Session s, SignRecipe r) { }

        /// <summary>The picked item's size as made (scale 1) in metres, for the Width/Height rows and the ghost.</summary>
        public abstract bool Size(Session s, out Vector2 metres);

        /// <summary>Where the picked item's middle sits as made (wu from the sign's centre; a picture is made centred),
        /// for the ghost frame; false when unknown.</summary>
        public virtual bool Centre(Session s, out double x, out double y)
        {
            x = y = 0;
            return true;
        }

        /// <summary>Where the picked item's middle shows on the sign in a fit (text-local, wu), as the put leaves it: made
        /// with its middle at Centre and moved by the fit (Fit.Centre), or as made when the put cannot move it (still: the
        /// sign turns, the texts stay). The frame draws there (0.20.157, review: frames turned the content round its own
        /// middle where the put turns it round the sign's).</summary>
        public virtual bool Middle(Session s, Fit fit, bool still, out double x, out double y)
        {
            bool known = Centre(s, out double c0x, out double c0y);
            if (!known) c0x = c0y = 0;
            if (still)
            {
                x = c0x;
                y = c0y;
            }
            else fit.Centre(c0x, c0y, out x, out y);
            return known;
        }

        /// <summary>The picked item as the put would leave it, for the live preview (ContentPreview, 0.20.129), or null:
        /// what the sign holds is then previewed as Refit would move it (Refit, Apply), else only the frame shows.</summary>
        public virtual PreviewContent Preview(Session s) => null;

        /// <summary>Why the button is grey ("Pick a picture above"), or null.</summary>
        public abstract string Problem(Session s);

        /// <summary>The picked item for the red-level question: "This picture (800 x 600 pixels)".</summary>
        public virtual string What(Session s) => "This";

        /// <summary>How to make it lighter, under the red-level question, or null.</summary>
        public virtual string Lighter(Session s) => null;

        /// <summary>The red level (SaveCost.Heavy): what everyone near it pays; saveBytes -1 when not counted yet.</summary>
        public abstract bool Heavy(Session s, out double loadSeconds, out long saveBytes, out long largest);

        /// <summary>Why the picked item may not go on in a multiplayer world (SaveCost.Allowed: over the sign limit, or
        /// over the area limit with what the signs around hold; with how to make it lighter), or null: single player,
        /// not counted yet (Problem says so), or it fits.</summary>
        public string Limit(Session s)
        {
            if (!SignText.Shared() || !s.HasTarget) return null;
            Heavy(s, out _, out long save, out _);
            if (save < 0 || SaveCost.Allowed(save, s.SaveBytes, s.AreaOthers, true, out string why)) return null;
            string lighter = Lighter(s);
            return char.ToUpper(why[0]) + why.Substring(1) + (lighter != null ? ". " + lighter : ".");
        }

        /// <summary>The bytes the picked item would store (SaveCost.Bytes), or -1 while not counted.</summary>
        public long Bytes(Session s)
        {
            Heavy(s, out _, out long save, out _);
            return save;
        }

        /// <summary>The button's work (through w.Act or a job).</summary>
        public abstract void Act(Session s, SignLinkWindow w, Verb v);

        /// <summary>The plugin unloads: what it holds (quick views, work running) goes (0.20.135, the author: now let us do the OnDestroy solution mentioned).</summary>
        public virtual void Dispose() { }

        /// <summary>Its job is running (the button says "Working ...").</summary>
        public virtual bool Busy => false;

        /// <summary>It still reads or counts what the button needs: a grey button says "Calculating ..." then (0.20.134, the
        /// author: whenever the button is grey because the mod is still calculating, the button should say Calculating).</summary>
        public virtual bool Calculating(Session s) => false;

        /// <summary>Its size is a scale of its texts (pieces, text: SizeRows on the source, applied on 3 Place); a picture's
        /// Width re-encodes it instead, so its scale is always 1 (the frame in the world).</summary>
        public virtual bool Resizable => true;

        /// <summary>The picked item's texts can be resized and moved.</summary>
        public virtual bool Movable(Session s) => true;

        /// <summary>A line under the source's Size rows (a piece's scale snapping, why it cannot change), or null.</summary>
        public virtual string FitHint(Session s) => null;


        /// <summary>The rule of 2 Content (0.20.18): without a sign you can look, not set. The light, the sizes, a
        /// picture's pixel options and the text box are for the selected sign, so they are greyed until there is one,
        /// and while a ward keeps it from you; the lists stay open to browse.</summary>
        public static bool Ready(Session s) => s.HasTarget && s.CanEdit;

        /// <summary>The line under greyed rows: why they are grey.</summary>
        public static string NotReady(Session s) =>
            !s.HasTarget ? "No sign" : s.Reason ?? "Locked";
    }
}
