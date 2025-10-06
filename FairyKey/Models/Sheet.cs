using System.IO;

namespace FairyKey.Models
{
    public class Sheet
    {
        public string FilePath { get; set; }
        public string Title { get; set; } = "Untitled";
        public string Artist { get; set; } = "";
        public string Creator { get; set; } = "";
        public List<string> Notes { get; set; } = new();

        public static Sheet LoadFromFile(string filePath)
        {
            if (!File.Exists(filePath))
                return null!;

            var lines = File.ReadAllLines(filePath);
            if (lines.Length == 0)
                return new Sheet { Title = Path.GetFileNameWithoutExtension(filePath) };

            string title = Path.GetFileNameWithoutExtension(filePath);
            string artist = "";
            string creator = "";
            int contentStart = 0;

            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i].Trim();

                if (line.StartsWith("title=", StringComparison.OrdinalIgnoreCase))
                    title = line.Substring("title=".Length).Trim();
                else if (line.StartsWith("artist=", StringComparison.OrdinalIgnoreCase))
                    artist = line.Substring("artist=".Length).Trim();
                else if (line.StartsWith("creator=", StringComparison.OrdinalIgnoreCase))
                    creator = line.Substring("creator=".Length).Trim();
                else
                {
                    contentStart = i;
                    break;
                }
            }

            var notes = lines
                        .Skip(contentStart)
                        .Select(l => l.Trim())
                        .Where(l => !string.IsNullOrEmpty(l))
                        .ToList();

            return new Sheet
            {
                Title = string.IsNullOrWhiteSpace(title) ? Path.GetFileNameWithoutExtension(filePath) : title,
                Artist = artist,
                Creator = creator,
                Notes = notes
            };
        }
    }
}