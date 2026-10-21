using UnityEngine;

namespace PetyusPolisz.Kit
{
    /// <summary>Classic's colours (1.1.0): the warm look the window always had, in the Claude Design round's "longhouse dark
    /// with gold leaf" (option 1b, its colours read from the design page): near-black smoked wood, a gilt edge on the
    /// controls, gold accents. The shapes are Nordic's (the author: one theme system, only the colours differ).</summary>
    internal static partial class Theme
    {
        static readonly Palette ClassicColours = new Palette
        {
            Bg = Hex("#0F0B08"), Panel = Hex("#221A12"), Track = Hex("#070504"), Control = Hex("#1A140E"),
            ControlHover = Hex("#2A2116"), ControlHeld = Hex("#0B0806"), SegOn = Hex("#3A2E1A"), SegOnHover = Hex("#46371E"),
            SegHeld = Hex("#120E09"),
            Line = Hex("#4E3F25"), WindowFrame = Hex("#8A7038"), TrackFrame = Hex("#5C4B2A"), ControlFrame = Hex("#8A7038"),
            FrameHover = Hex("#D9B25C"), KeyFrame = Hex("#5C4B2A"), TitleRule = Hex("#8A7038"),
            Accent = Hex("#E2B958"), AccentHover = Hex("#EBC66E"), AccentHeld = Hex("#C99A3E"), Lip = Hex("#8E6A28"),
            LipHover = Hex("#9C7630"), LipHeld = Hex("#7A5A20"), WindowTop = Hex("#D9B25C"),
            Title = Hex("#F1CF7C"), Heading = Hex("#E8C46A"), Text = Hex("#EFE4CC"), TextOn = Hex("#F8DC92"), TextSeg = Hex("#CDBF9F"),
            Dim = Hex("#B2A487"), OnAccent = Hex("#1A1206"),
            Red = Hex("#FF8466"), RedHover = Hex("#FF9A80"), Green = Hex("#97DB80"), WarnBg = Hex("#46120A"), WarnBgHover = Hex("#561A10"),
            WarnText = Hex("#FFB8A4"), HeldKey = Hex("#4A3A1C"), ScrollThumb = Hex("#6B5630"), ScrollThumbHover = Hex("#8A7038"),
            DimHex = "#b2a487", DimOnHex = "#cdbf9f",
        };
    }
}
