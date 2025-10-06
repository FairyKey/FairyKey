namespace FairyKey.Models
{
    public class SheetFolder
    {
        public string Path { get; set; }
        public string Name => string.IsNullOrEmpty(Path) ? "(Unknown)" : System.IO.Path.GetFileName(Path);

        public bool IsExpanded { get; set; } = false;
        public List<Sheet> Sheets { get; set; } = new();
    }
}