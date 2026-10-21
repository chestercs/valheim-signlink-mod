namespace SignLink.UI
{
    /// <summary>One tab of the SignLink window: a scrolling body and a footer that always shows (the main button and
    /// its status). State changes from clicks go through Window.Act or Window.Later (next Layout event).</summary>
    internal abstract class Tab
    {
        protected readonly SignLinkWindow Window;

        protected Tab(SignLinkWindow window) => Window = window;

        public abstract string Title { get; }

        /// <summary>Shows the target sign bar above the body.</summary>
        public virtual bool UsesTarget => false;

        public virtual void OnOpen() { }

        /// <summary>The window closed: disarm questions, drop buffers.</summary>
        public virtual void OnClose() { }

        /// <summary>The plugin unloads: what the tab holds goes (0.20.135, the author: now let us do the OnDestroy solution mentioned).</summary>
        public virtual void OnDestroy() { }

        /// <summary>Drawn under the target bar and above the scrolling body: what must stay in view while the body
        /// scrolls (2 Content's source strip).</summary>
        public virtual void Pinned() { }

        public abstract void Body();

        public virtual void Footer() { }

        /// <summary>Draws a footer (1.1.0, the author: Playing and Settings showed an empty bar): without one, no box under
        /// the body, the status line alone.</summary>
        public virtual bool HasFooter => false;
    }
}
