using System;

namespace PetyusPolisz.Kit
{
    /// <summary>A window that runs state changes at a later Layout (IMGUI draws every event twice, Layout then the event:
    /// a control that appears or vanishes in between throws). Kit controls that change what is drawn go through it.</summary>
    internal interface IDeferred
    {
        void Later(Action a);
    }
}
