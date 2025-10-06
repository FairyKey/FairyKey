using System.Windows.Media;

namespace FairyKey
{
    public static class UIStyles
    {
        public static readonly int NotesFontSize = 20;
        public static readonly FontFamily NotesFont = new("Consolas");
        public static readonly Brush FontColor = (Brush)new BrushConverter().ConvertFrom("#202227");
        public static readonly Brush FontColorFaded = (Brush)new BrushConverter().ConvertFrom("#ABABAB");
        public static readonly Brush HighlightColor = (Brush)new BrushConverter().ConvertFrom("#d65b5e");
        public static readonly Brush AltHighlightColor = (Brush)new BrushConverter().ConvertFrom("#67bed9");
        public static readonly Brush ButtonBg = (Brush)new BrushConverter().ConvertFrom("#e0e2e6");
        public static readonly Brush ButtonFg = (Brush)new BrushConverter().ConvertFrom("#2e3033");
        public static readonly Brush UnderTitleFontColor = (Brush)new BrushConverter().ConvertFrom("#666666");
    }
}