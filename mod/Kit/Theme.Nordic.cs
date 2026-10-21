using UnityEngine;

namespace PetyusPolisz.Kit
{
    /// <summary>Nordic's colours (1.1.0, the author's Claude Design handoff, option 1c): slate panels, amber accents. The
    /// handoff's tokens (.private/design/handoff); the track's frame is the author's (strips that stand apart).</summary>
    internal static partial class Theme
    {
        static readonly Palette NordicColours = new Palette
        {
            Bg = Hex("#17191B"), Panel = Hex("#24282C"), Track = Hex("#0E0F11"), Control = Hex("#2F3439"),
            ControlHover = Hex("#394047"), ControlHeld = Hex("#23272B"), SegOn = Hex("#41484F"), SegOnHover = Hex("#4A525A"),
            SegHeld = Hex("#262A2E"),
            Line = Hex("#3C434A"), WindowFrame = Hex("#4D545A"), TrackFrame = Hex("#4D545A"), ControlFrame = Hex("#535B63"),
            FrameHover = Hex("#6C757D"), KeyFrame = Hex("#4D545A"), TitleRule = Hex("#454C52"),
            Accent = Hex("#F2AE45"), AccentHover = Hex("#F8C066"), AccentHeld = Hex("#D9932E"), Lip = Hex("#B97A1C"),
            LipHover = Hex("#C98A26"), LipHeld = Hex("#A86C16"), WindowTop = Hex("#F0A83A"),
            Title = Hex("#F2AE45"), Heading = Hex("#F2AE45"), Text = Hex("#ECE7DD"), TextOn = Color.white, TextSeg = Hex("#C9C8C2"),
            Dim = Hex("#A8ADB1"), OnAccent = Hex("#1B1408"),
            Red = Hex("#FF7E69"), RedHover = Hex("#FF9A88"), Green = Hex("#8FD68A"), WarnBg = Hex("#5A2621"), WarnBgHover = Hex("#6A2C26"),
            WarnText = Hex("#FFE3DD"), HeldKey = Hex("#6A4A1A"), ScrollThumb = Hex("#5A6269"), ScrollThumbHover = Hex("#7A838B"),
            DimHex = "#a8adb1", DimOnHex = "#c9c8c2",
        };
    }
}
